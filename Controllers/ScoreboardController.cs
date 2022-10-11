namespace FantasyFootballStatTracker.Controllers
{
    using Azure.Core;
    using Azure.Identity;
    using FantasyFootballStatTracker.Models;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Threading.Tasks;
    using FantasyFootballStatTracker.Infrastructure;
    using Microsoft.Extensions.Logging;
    using System.Globalization;

    public class ScoreboardController : Controller
    {
        private readonly ILogger<ScoreboardController> _logger;

        /// <summary>
        /// Session key for the currently selected scoreboard week
        /// </summary>
        public const string SessionKeyWeek = "_Week";

        /// <summary>
        /// Session key for the Azure SQL Access token
        /// </summary>
        public const string SessionKeyAzureSqlAccessToken = "_Token";

        /// <summary>
        /// Stores the owner logos
        /// </summary>
        private List<byte[]> OwnerLogos;

        /// <summary>
        /// Injecting HttpClientFactory to set the HttpClient used for calling the yahoo API to get data for each
        /// player in the scoreboard.
        /// </summary>
        /// <param name="factory"></param>
        public ScoreboardController(ILogger<ScoreboardController> logger)
        {
            _logger = logger;
        }

        private static async Task<string> GetAzureSqlAccessToken()
        {
            // See https://docs.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/services-support-managed-identities#azure-sql
            var tokenRequestContext = new TokenRequestContext(new[] { "https://database.windows.net//.default" });
            var tokenRequestResult = await new DefaultAzureCredential().GetTokenAsync(tokenRequestContext);

            return tokenRequestResult.Token;
        }

        /// <summary>
        /// Queries the CurrentRoster database to create a hashtable storing players for each owner, grouped by espn game id
        /// so we can parse all players in the same doc, limiting the number of times we need to download the doc 
        /// </summary>
        /// <param name="selectedWeek">The week selected in the form; if this is null, we'll select the latest week</param>
        /// <returns>Hashtable of all players, grouped by espn game ids</returns>
        public async Task<Hashtable> createPlayersHashtable(string selectedWeek)
        {
            // Hashtable to store players grouped by espn game id so we can parse all players in the same doc, limiting
            // the number of times we need to download the doc
            Hashtable playersHashTable = new Hashtable();

            var connectionStringBuilder = new SqlConnectionStringBuilder
            {
                DataSource = "tcp:playersandscheduledetails.database.windows.net,1433",
                InitialCatalog = "PlayersAndSchedulesDetails",
                TrustServerCertificate = false,
                Encrypt = true
            };

            string azureSqlToken = Microsoft.AspNetCore.Http.SessionExtensions.GetString(HttpContext.Session, SessionKeyAzureSqlAccessToken);

            // if we haven't retrieved the token yet, retrieve it and set it in the session (at this point though, we should have the token)
            if (azureSqlToken == null)
            {
                azureSqlToken = await GetAzureSqlAccessToken();

                Microsoft.AspNetCore.Http.SessionExtensions.SetString(HttpContext.Session, SessionKeyAzureSqlAccessToken, azureSqlToken);
            }

            SqlConnection sqlConnection = new SqlConnection(connectionStringBuilder.ConnectionString);
            sqlConnection.AccessToken = azureSqlToken;

            await sqlConnection.OpenAsync();

            OwnerLogos = HttpContext.Session.GetObjectFromJson<List<byte[]>>(Infrastructure.SessionExtensions.SessionKeyLogos);

            // if we haven't retrieved the owner logos yet, retrieve it and store it in the session
            if (OwnerLogos == null)
            {
                OwnerLogos = new List<byte[]>();

                // call stored procedure to get the owner logos
                using (SqlCommand command = new SqlCommand("GetOwnerLogos", sqlConnection))
                {
                    command.CommandType = System.Data.CommandType.StoredProcedure;

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            OwnerLogos.Add((byte[])reader.GetValue(reader.GetOrdinal("Logo")));
                        }

                        HttpContext.Session.SetObjectAsJson(Infrastructure.SessionExtensions.SessionKeyLogos, OwnerLogos);
                    }
                }
            }            

            // call stored procedure to get all players for each team's roster for this week
            using (SqlCommand command = new SqlCommand("GetTeamsForGivenWeek", sqlConnection))
            {
                command.CommandType = System.Data.CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@week", System.Data.SqlDbType.Int) { Value = selectedWeek });

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    // keep track of number of WRs, RBs, and TEs, so we know if we are adding a FLEX or not
                    int rosterOneWrCount = 0;
                    int rosterTwoWrCount = 0;
                    int rosterOneRbCount = 0;
                    int rosterTwoRbCount = 0;
                    int rosterOneTeCount = 0;
                    int rosterTwoTeCount = 0;

                    while (reader.Read())
                    {
                        int ownerId = (int)reader.GetValue(reader.GetOrdinal("OwnerID"));
                        string ownerName = reader.GetValue(reader.GetOrdinal("OwnerName")).ToString();
                        string playerName = reader.GetValue(reader.GetOrdinal("PlayerName")).ToString();
                        Position position = (Position)Enum.Parse(typeof(Position),reader.GetValue(reader.GetOrdinal("Position")).ToString().Trim());
                        bool gameEnded = (bool)reader.GetValue(reader.GetOrdinal("GameEnded"));
                        double finalPoints = (double)reader.GetValue(reader.GetOrdinal("FinalPoints"));
                        string finalScoreString = reader.GetValue(reader.GetOrdinal("FinalScoreString")).ToString();
                        string espnPlayerId = reader.GetValue(reader.GetOrdinal("EspnPlayerId")).ToString();
                        string headshotUrl = reader.GetValue(reader.GetOrdinal("HeadshotUrl")).ToString();
                        string espnGameId = ((int)reader.GetValue(reader.GetOrdinal("EspnGameId"))).ToString();
                        string homeOrAway = reader.GetValue(reader.GetOrdinal("HomeOrAway")).ToString();
                        string teamAbbreviation = reader.GetValue(reader.GetOrdinal("TeamAbbreviation")).ToString();
                        string opponentAbbreviation = reader.GetValue(reader.GetOrdinal("OpponentAbbreviation")).ToString();
                        DateTime gameDate = DateTime.Parse((reader.GetValue(reader.GetOrdinal("GameDate")).ToString()));

                        // we need to save the full player name into a search string so we don't modify the full name. This is mostly
                        // due to a defense such as "los angeles rams" and "los angeles chargers" only able to be searched by "los angeles",
                        // so we cannot lose the full name. We will cut off the "rams" or "chargers" part in the DEF case below
                        string playerNameSearchString = playerName;

                        Position positionType = Position.FLEX;

                        switch (position)
                        {
                            case Position.QB:
                                positionType = Position.QB;
                                break;

                            case Position.RB:
                                if (ownerId == 1)
                                {
                                    if (rosterOneRbCount == 2)
                                    {
                                        positionType = Position.FLEX;
                                    }
                                    else
                                    {
                                        positionType = Position.RB;
                                        rosterOneRbCount++;
                                    }
                                }
                                else if (ownerId == 2)
                                {
                                    if (rosterTwoRbCount == 2)
                                    {
                                        positionType = Position.FLEX;
                                    }
                                    else
                                    {
                                        positionType = Position.RB;
                                        rosterTwoRbCount++;
                                    }
                                }

                                break;

                            case Position.WR:
                                if (ownerId == 1)
                                {
                                    if (rosterOneWrCount == 2)
                                    {
                                        positionType = Position.FLEX;
                                    }
                                    else
                                    {
                                        positionType = Position.WR;
                                        rosterOneWrCount++;
                                    }
                                }
                                else if (ownerId == 2)
                                {
                                    if (rosterTwoWrCount == 2)
                                    {
                                        positionType = Position.FLEX;
                                    }
                                    else
                                    {
                                        positionType = Position.WR;
                                        rosterTwoWrCount++;
                                    }
                                }
                                break;

                            case Position.TE:
                                if (ownerId == 1)
                                {
                                    if (rosterOneTeCount == 1)
                                    {
                                        positionType = Position.FLEX;
                                    }
                                    else
                                    {
                                        positionType = Position.TE;
                                        rosterOneTeCount++;
                                    }
                                }
                                else if (ownerId == 2)
                                {
                                    if (rosterTwoTeCount == 1)
                                    {
                                        positionType = Position.FLEX;
                                    }
                                    else
                                    {
                                        positionType = Position.TE;
                                        rosterTwoTeCount++;
                                    }
                                }
                                break;

                            case Position.K:
                                positionType = Position.K;
                                break;

                            case Position.DEF:
                                positionType = Position.DEF;

                                // the defense comes in from ESPN with all lowercase, so change first letters of each word to uppercase
                                playerName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(playerName);

                                break;

                            default:
                                // TODO: ADD SOME ERROR STATE
                                positionType = Position.FLEX;
                                break;
                        }

                        SelectedPlayer player = new SelectedPlayer();
                        player.Name = playerName;
                        player.Headshot = headshotUrl;
                        player.TruePosition = position;
                        player.Position = positionType;
                        player.EspnGameId = espnGameId;
                        player.GameTime = gameDate;
                        player.EspnPlayerId = espnPlayerId;
                        player.TeamAbbreviation = teamAbbreviation;
                        player.OpponentAbbreviation = opponentAbbreviation;
                        player.HomeOrAway = homeOrAway;
                        player.OwnerId = ownerId;
                        player.OwnerName = ownerName;
                        player.GameEnded = gameEnded;
                        player.Points = finalPoints;
                        player.FinalScoreString = finalScoreString;
                        player.Week = int.Parse(selectedWeek);

                        addPlayerToHashtable(playersHashTable, espnGameId, player);
                    }
                }
            }

            await sqlConnection.CloseAsync();

            return playersHashTable;
        }

        /// <summary>
        /// The post action which is called when the week in the dropdown list is selected. This will simply store the
        /// week in a session state variable and redirect to the get action where the week will be pulled out so the
        /// data for that week's matchup is pulled and displayed in the view. This redirect to the get prevents the browser
        /// from popping up a warning if you refresh the page and the dropdown list tries to post again.
        /// </summary>
        /// <param name="week">The week selected in the dropdown list</param>
        /// <returns>A redirect to the GET action</returns>
        [HttpPost]
        public IActionResult Index(string week)
        {
            Microsoft.AspNetCore.Http.SessionExtensions.SetString(HttpContext.Session, SessionKeyWeek, week);

            return RedirectToAction("Index");
        }

        /// <summary>
        /// The GET action which will check for the week in the session state variable. If the week was not selected (the
        /// drop down list stays on the default latest week), it will pull back the latest week, otherwise the week selected
        /// in the dropdown list is used to pull matchup data.
        /// </summary>
        /// <returns>The view with data from the selected week in the dropdown list</returns>
        public async Task<IActionResult> Index()
        {
            string week = Microsoft.AspNetCore.Http.SessionExtensions.GetString(HttpContext.Session, SessionKeyWeek);

            // populate the week dropdown with all weeks a matchup has been played
            ViewBag.weeks = GetGameWeeks(week);

            // there may be a better way of doing this, but the GetGameWeeks call will update the session variable to the latest
            // week if no week was selected, whether it's the latest week or the new week which a team needs to be selected for
            if (week == null)
            {
                week = Microsoft.AspNetCore.Http.SessionExtensions.GetString(HttpContext.Session, SessionKeyWeek);
            }

            List<Team> teams = new List<Team>();

            // Get the Hashtable which will store players grouped by espn game id so we can parse all players in the same doc, limiting
            // the number of times we need to download the doc
            Hashtable playersHashTable = await createPlayersHashtable(week);

            // each thread will need a done event signifying when the thread has completed, so create a list of
            // done events for each thread (for each espn game id)
            //var doneEvents = new ManualResetEvent[testPlayers.Keys.Count];

            // counter for the doneEvents array
            int i = 0;

            var tasks = new Task[playersHashTable.Keys.Count];

            // loop through each key (espn game id) and parse the points for each player in that game,
            // adding each SelectedPlayer in the hashtable to the approprate list of teams (team one or team two)
            foreach (string key in playersHashTable.Keys)
            {
                List<SelectedPlayer> playersInGame = (List<SelectedPlayer>)playersHashTable[key];
                tasks[i] = Task.Factory.StartNew(() => scrapeStatsFromGame(key, playersInGame));
                //scrapeStatsFromGame(key, playersInGame);

                // create the done event for this thread
                /*doneEvents[i] = new ManualResetEvent(false);

                // setup the state parameters which are passed into the method being executed in the thread
                State stateInfo = new State();
                stateInfo.EspnGameId = key;
                stateInfo.players = testPlayers;
                stateInfo.DoneEvent = doneEvents[i];

                ThreadPool.QueueUserWorkItem(scrapeStatsFromGame, stateInfo);*/

                i++;
            }

            // wait for all threads to complete
            Task.WaitAll(tasks);

            // wait for all threads to have reported that they have completed their work
            //WaitHandle.WaitAll(doneEvents);

            // The values of each hashtable are lists of List<SelectedPlayer> so we need to get this list of lists and flatten
            // the list
            List<List<SelectedPlayer>> listOfPlayerLists = playersHashTable.Values.OfType<List<SelectedPlayer>>().ToList();
            List<SelectedPlayer> players = listOfPlayerLists.SelectMany(x => x).ToList();

            // Pull out the players for team one and sort by position
            //List<SelectedPlayer> teamOnePlayers = players.Where(x => x.Owner.Equals("Liz")).ToList();
            List<SelectedPlayer> teamOnePlayers = players.Where(x => x.OwnerId == 1).ToList();
            teamOnePlayers = teamOnePlayers.OrderBy(x => (int)(x.Position)).ToList();

            // Total up the scores from this team
            List<double> pointsList = teamOnePlayers.Select(x => x.Points).ToList();
            double points = pointsList.Sum();

            Team team = new Team
            {
                OwnerId = 1,
                Week = int.Parse(week),
                OwnerLogo = OwnerLogos[0],
                TotalFantasyPoints = Math.Round(points, 2),
                Players = teamOnePlayers
            };

            teams.Add(team);

            // Pull out the players for team two and sort by position
            List<SelectedPlayer> teamTwoPlayers = players.Where(x => x.OwnerId == 2).ToList();
            teamTwoPlayers = teamTwoPlayers.OrderBy(x => (int) x.Position).ToList();

            // Total up the scores from this team
            pointsList = teamTwoPlayers.Select(x => x.Points).ToList();
            points = pointsList.Sum();

            team = new Team
            {
                OwnerId = 2,
                Week = int.Parse(week),
                OwnerLogo = OwnerLogos[1],
                TotalFantasyPoints = Math.Round(points, 2),
                Players = teamTwoPlayers
            };

            teams.Add(team);

            return View(teams);
        }

        /// <summary>
        /// Scrapes stats for each player playing in a particular game. This is called in a thread so when the work
        /// is done, the thread will report that it has completed by calling the signalThread method
        /// </summary>
        /// <param name="espnGameId">ESPN Game ID for the players on either roster</param>
        /// <param name="players">All players playing in the given ESPN Game ID</param>
        private void scrapeStatsFromGame(string espnGameId, List<SelectedPlayer> players)
        {
            //State stateInfo = (State)state;

            //List<SelectedPlayer> selectedPlayers = (List<SelectedPlayer>)stateInfo.players[stateInfo.EspnGameId];

            // Check the date time of the game for the first player (it is the same for all players in this list
            // since they belong to the same game) and if it hasn't started, just set the points equal to 0
            // and don't load the document.
            //SelectedPlayer player = selectedPlayers[0];
            SelectedPlayer player = players[0];

            // Get current EST time - If this is run on a machine with a differnet local time, DateTime.Now will not return the proper time
            TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            DateTime currentEasterStandardTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, easternZone);
            TimeSpan difference = player.GameTime.Subtract(currentEasterStandardTime);

            // Also check if the first player's game has ended, which is set to true in the CurrentRoster table when the scraper
            // determines that the game has ended.
            bool gameEnded = player.GameEnded;

            // if the game hasn't started or the game has ended, don't load the HtmlDoc to parse stats since we've already done that
            if ((difference.TotalDays < 0) && (!gameEnded))
            {
                //EspnHtmlScraper scraper = new EspnHtmlScraper(stateInfo.EspnGameId);
                EspnHtmlScraper scraper = new EspnHtmlScraper(espnGameId);

                // calculate points for each of these players
                //foreach (SelectedPlayer p in selectedPlayers)
                foreach (SelectedPlayer p in players)
                {
                    // set flag to true to indicate this player's game is in progress
                    p.GameInProgress = true;

                    //p.Points += scraper.parseGameTrackerPage(stateInfo.EspnGameId, p.EspnPlayerId, p.HomeOrAway, p.OpponentAbbreviation);
                    p.Points += scraper.parseGameTrackerPage(espnGameId, p.EspnPlayerId, p.Position, p.HomeOrAway, p.OpponentAbbreviation);
                    //p.Points += scraper.parseTwoPointConversionsForPlayer(stateInfo.EspnGameId, p.RawPlayerName);
                    p.Points += scraper.parseTwoPointConversionsForPlayer(p.Name);
                    p.TimeRemaining = scraper.parseTimeRemaining();
                    p.CurrentScoreString = scraper.parseCurrentScore(p.HomeOrAway);

                    // calculate kicker FGs if this player is a kicker
                    if (p.Position == Position.K)
                    {
                        //p.Points += scraper.parseFieldGoals(stateInfo.EspnGameId, p.RawPlayerName);
                        p.Points += scraper.parseFieldGoals(p.Name);
                    }

                    // check the scraper to see if the game has ended and update this player row
                    if (scraper.GameEnded)
                    {
                        // set the flag to false since this game is no longer in progress
                        p.GameInProgress = false;

                        // set flag to true that the game ended, used for the final score string (a game can be not start - therefore
                        // not in progress - and also not ended, so we wouldn't want to display the final score in this case)
                        p.GameEnded = true;

                        // Get the final score string (such as "(W) 45 - 30") and store this in the database
                        string finalScoreString = scraper.parseFinalScore(p.TeamAbbreviation);

                        p.FinalScoreString = finalScoreString;

                        updateCurrentRosterWithFinalScore(p.OwnerId, p.EspnPlayerId, p.Points, finalScoreString, p.Week);
                    }
                }
            }

            // all of the work is done, so signal the thread that it's complete so the ThreadPool will be notified
            // stateInfo.DoneEvent.Set();
        }

        /// <summary>
        /// Gets from the CurrentRoster table a list of all weeks a matchup has been played. A check is then made to see if the
        /// latest week has already been played so the new week can be added and the owners can then select their teams by clicking
        /// a link to redirect to the select team page.
        /// </summary>
        /// <param name="selectedWeek">The week selected from the form; null if page is first loaded</param>
        /// <returns>/A list of all weeks a matchup has been played</returns>
        private List<SelectListItem> GetGameWeeks(string selectedWeek)
        {
            List<SelectListItem> weeks = new List<SelectListItem>();

            var connectionStringBuilder = new SqlConnectionStringBuilder
            {
                DataSource = "tcp:playersandscheduledetails.database.windows.net,1433",
                InitialCatalog = "PlayersAndSchedulesDetails",
                TrustServerCertificate = false,
                Encrypt = true
            };

            var sqlConnection = new SqlConnection(connectionStringBuilder.ConnectionString);

            string azureSqlToken = Microsoft.AspNetCore.Http.SessionExtensions.GetString(HttpContext.Session, SessionKeyAzureSqlAccessToken);

            // if we haven't retrieved the token yet, retrieve it and set it in the session (at this point though, we should have the token)
            if (azureSqlToken == null)
            {
                var tokenRequestContext = new TokenRequestContext(new[] { "https://database.windows.net//.default" });
                var tokenRequestResult = new DefaultAzureCredential().GetToken(tokenRequestContext);

                azureSqlToken = tokenRequestResult.Token;

                Microsoft.AspNetCore.Http.SessionExtensions.SetString(HttpContext.Session, SessionKeyAzureSqlAccessToken, azureSqlToken);
            }

            sqlConnection.AccessToken = azureSqlToken;

            sqlConnection.Open();

            string week = "0";

            using (SqlCommand command = new SqlCommand("GetAllWeeksPlayed", sqlConnection))
            {
                command.CommandType = System.Data.CommandType.StoredProcedure;

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        week = reader["week"].ToString();

                        // if this week is the week which was selected from the form, set this as the selected week in the
                        // drop down list
                        bool selected;

                        // if the selectedweek is null, we'll keep this true so the last item will be selected
                        if (selectedWeek == null)
                        {
                            selected = true;
                        }
                        else
                        {
                            selected = week.Equals(selectedWeek) ? true : false;
                        }

                        weeks.Add(new SelectListItem(week, week, selected));
                    }
                }
            }

            // the latest week is stored in the week variable and we can check this to see if this week has completed and
            // new week needs to be added
            if ((week == "0") || !IsLatestWeekSelectedForEitherOwner(sqlConnection, week))
            {
                int intWeek = int.Parse(week);
                intWeek++;

                week = intWeek.ToString();

                // if the user didn't select a week in the scoreboard, or the user switched to a different week and came back to the
                // latest week where teams haven't yet been selected, select this week; otherwise any other week the user selected from
                // the scoreboard will be selected
                if ((selectedWeek == null) || (selectedWeek.Equals(week)))
                {
                    // select this week so it will provide the owner(s) with the link to select a team
                    weeks.Add(new SelectListItem(week, week, true));
                }
                else
                {
                    // select this week so it will provide the owner(s) with the link to select a team
                    weeks.Add(new SelectListItem(week, week, false));
                }
            }

            // Set the session to hold the latest week so it will select the team for this week, but only update this if
            // the user didn't select a week from the dropdown (if the selectedWeek is null)
            if (selectedWeek == null)
            {
                Microsoft.AspNetCore.Http.SessionExtensions.SetString(HttpContext.Session, SessionKeyWeek, week);
            }

            sqlConnection.Close();

            return weeks;
        }

        /// <summary>
        /// Checks the latest week selected from the CurrentRoster table to see if this week has ended (with at least one
        /// day elapsed) or not.
        /// </summary>
        /// <param name="latestWeek">The latest week selected for either owner in the CurrentRoster table</param>
        /// <returns></returns>
        private bool IsLatestWeekSelectedForEitherOwner(SqlConnection sqlConnection, string latestWeek)
        {
            bool latestWeekSelectedForEitherOwner = true;

            // get the latest game date for a game in the last week we have a roster selected for
            // i.e., if the last week we have a roster selected in CurrentRoster is week 12, we'll get the last date of any
            // game which occurs in week 12 from the TeamSchedule
            // We also need to check to make sure there is a game scheduled the next week; if not, then even if this last week has completed
            // more than a day ago, this is still the latest week selected for the owners. If there isn't a next week, the SQL statement will
            // return null (TODO: there is probably a better way of doing this)
            string nextWeek = (int.Parse(latestWeek) + 1).ToString();

            DateTime lastGameDateForLatestSelectedRosterWeek = new DateTime();

            // flag to determine if there is a next week scheduled or not
            bool isNextWeekScheduled = false;

            using (SqlCommand command = new SqlCommand("GetLastGameDatePlayed", sqlConnection))
            {
                command.CommandType = System.Data.CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@LatestWeek", System.Data.SqlDbType.Int) { Value = int.Parse(latestWeek) });
                command.Parameters.Add(new SqlParameter("@NextWeek", System.Data.SqlDbType.Int) { Value = int.Parse(nextWeek) });

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    reader.Read();

                    // there is only one value returned, so we just need to grab the first value
                    if (!reader.GetValue(0).ToString().Equals(String.Empty))
                    {
                        isNextWeekScheduled = true;
                        lastGameDateForLatestSelectedRosterWeek = DateTime.Parse(reader.GetValue(0).ToString());
                    }
                }
            }

            // if a next week is not scheduled, we will leave the latest week selected for either owner as true by skipping the following code
            // to check the day difference
            if (isNextWeekScheduled)
            {
                // Get current EST time - If this is run on a machine with a differnet local time, DateTime.Now will not return the proper time
                TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                DateTime currentEasterStandardTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, easternZone);
                TimeSpan difference = lastGameDateForLatestSelectedRosterWeek.Subtract(currentEasterStandardTime);

                // we are taking the last game for the selected week minus today's date; so if we selected rosters for week 11
                // and the monday night game in week 11 ended and it's currently tuesday, it would return -1 because we are one day past
                // the last game played in the latest selected roster week; if it's currently wednesday, it would return -2, etc. We can
                //  wait a day before we redirect the user to select rosters (say, tuesday night), so we'll check if the difference is < -1
                if (difference.TotalDays < -1)
                {
                    latestWeekSelectedForEitherOwner = false;
                }
            }

            return latestWeekSelectedForEitherOwner;
        }

        /// <summary>
        /// Within the thread that updates the player points, if the scraper determins the game has ended, this
        /// method is called to update the CurrentRoster table for this particular player setting GameEnded to true
        /// and updating the FinalPoints field, so we can grab this the next time the app displays the scores rather
        /// than scrap the gametracker page again.
        /// </summary>
        /// <param name="ownerId">The owner id of the team</param>
        /// <param name="espnPlayerId">The id of the player whose points we are updating in the CurrentRoster table</param>
        /// <param name="playerFinalScore">The final score the player got in the game</param>
        /// <param name="finalScoreString">The final score string for the player's team, which is displayed in the UI</param>
        /// <param name="week">The week we are updating</param>
        private void updateCurrentRosterWithFinalScore(int ownerId, string espnPlayerId, double playerFinalScore, string finalScoreString, int week)
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder
            {
                DataSource = "tcp:playersandscheduledetails.database.windows.net,1433",
                InitialCatalog = "PlayersAndSchedulesDetails",
                TrustServerCertificate = false,
                Encrypt = true
            };

            var sqlConnection = new SqlConnection(connectionStringBuilder.ConnectionString);

            string azureSqlToken = Microsoft.AspNetCore.Http.SessionExtensions.GetString(HttpContext.Session, SessionKeyAzureSqlAccessToken);

            // if we haven't retrieved the token yet, retrieve it and set it in the session
            if (azureSqlToken == null)
            {
                var tokenRequestContext = new TokenRequestContext(new[] { "https://database.windows.net//.default" });
                var tokenRequestResult = new DefaultAzureCredential().GetToken(tokenRequestContext);

                azureSqlToken = tokenRequestResult.Token;

                Microsoft.AspNetCore.Http.SessionExtensions.SetString(HttpContext.Session, SessionKeyAzureSqlAccessToken, azureSqlToken);
            }

            sqlConnection.AccessToken = azureSqlToken;

            sqlConnection.Open();

            using (SqlCommand command = new SqlCommand("UpdatePlayerFinalScore", sqlConnection))
            {
                command.CommandType = System.Data.CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@PlayerFinalScore", System.Data.SqlDbType.Float) { Value = playerFinalScore });
                command.Parameters.Add(new SqlParameter("@FinalScoreString", System.Data.SqlDbType.NVarChar) { Value = finalScoreString });
                command.Parameters.Add(new SqlParameter("@OwnerId", System.Data.SqlDbType.Int) { Value = ownerId });
                command.Parameters.Add(new SqlParameter("@EspnPlayerId", System.Data.SqlDbType.Int) { Value = int.Parse(espnPlayerId) });
                command.Parameters.Add(new SqlParameter("@Week", System.Data.SqlDbType.Int) { Value = week });

                command.ExecuteNonQuery();
            }

            sqlConnection.Close();
        }

        /// <summary>
        /// Adds a player to the corresponding espnGameId key so all players in the same game will be in a list
        /// of players with the esponGameId being hte key.
        /// </summary>
        /// <param name="playerTable">The hashtable holding all players grouped by espn game ids</param>
        /// <param name="espnGameId">ESPON Game ID this player is playing in</param>
        /// <param name="player">The SelectedPlayer we are adding</param>
        private void addPlayerToHashtable(Hashtable playerTable, string espnGameId, SelectedPlayer player)
        {
            if (playerTable.ContainsKey(espnGameId))
            {
                List<SelectedPlayer> playerList = (List<SelectedPlayer>)playerTable[espnGameId];
                playerList.Add(player);
                playerTable[espnGameId] = playerList;
            }
            else
            {
                List<SelectedPlayer> playerList = new List<SelectedPlayer>();
                playerList.Add(player);
                playerTable.Add(espnGameId, playerList);
            }
        }

        // Maintain state to pass to the scrapeStatsFromGame method
        /*public class State
        {
            public string EspnGameId { get; set; }

            public Hashtable players { get; set; }

            List<Team> Teams { get; set; }

            public ManualResetEvent DoneEvent { get; set; }
        }*/
    }
}