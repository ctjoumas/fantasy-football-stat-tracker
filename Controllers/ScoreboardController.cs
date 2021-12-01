namespace YahooFantasyFootball.Controllers
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
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using System.Xml.Linq;
    using System.Xml.Serialization;
    using YahooFantasyFootball.Configuration;
    using YahooFantasyFootball.Infrastructure;
    using YahooFantasyFootball.Models;

    public class ScoreboardController : Controller
    {
        private static HttpClient client = new HttpClient();

        public const string SessionKeyWeek = "_Week";

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
            Hashtable testPlayers = new Hashtable();

            var connectionStringBuilder = new SqlConnectionStringBuilder
            {
                DataSource = "tcp:playersandscheduledetails.database.windows.net,1433",
                InitialCatalog = "PlayersAndSchedulesDetails",
                TrustServerCertificate = false,
                Encrypt = true
            };

            await using var sqlConnection = new SqlConnection(connectionStringBuilder.ConnectionString)
            {
                AccessToken = await GetAzureSqlAccessToken()
            };

            await sqlConnection.OpenAsync();

            List<RosterPlayer> rosterPlayers = new List<RosterPlayer>();

            // if the form is first loaded, week will be null so we will select the max week
            //string weekSelection = week;
            
            // if the week was not selected in the form (the form was redirected to), we'll select the latest week
            if (selectedWeek == null)
            {
                selectedWeek = "max(Week)";
            }

            // get all players for each team's roster for this week
            // TODO: this currently selects the latest week in the CurrentRoster; when a dropdown list for the week is added, this query will use that
            // to pull the scoreboard for that week
            string sql = "select o.OwnerName, o.Logo, cr.Week, cr.PlayerName, cr.Position, cr.GameEnded, cr.FinalPoints, cr.FinalPointsString " +
                        "from CurrentRoster cr " +
                        "join Owners o on cr.OwnerID = o.OwnerID " +
                        "where cr.Week in (select " + selectedWeek + " from CurrentRoster)";

            using (SqlCommand command = new SqlCommand(sql, sqlConnection))
            {
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string owner = reader.GetValue(reader.GetOrdinal("OwnerName")).ToString();
                        byte[] logo = (byte[])reader.GetValue(reader.GetOrdinal("Logo"));
                        int week = (int)reader.GetValue(reader.GetOrdinal("Week"));
                        string playerName = reader.GetValue(reader.GetOrdinal("PlayerName")).ToString();
                        string position = reader.GetValue(reader.GetOrdinal("Position")).ToString().Trim();
                        bool gameEnded = (bool)reader.GetValue(reader.GetOrdinal("GameEnded"));
                        double finalPoints = (double)reader.GetValue(reader.GetOrdinal("FinalPoints"));
                        string finalScoreString = reader.GetValue(reader.GetOrdinal("FinalPointsString")).ToString();

                        // add this player to the current roster
                        rosterPlayers.Add(new RosterPlayer()
                        {
                            Owner = owner,
                            Logo = logo,
                            Week = week,
                            PlayerName = playerName,
                            Position = (Position)Enum.Parse(typeof(Position), position),
                            GameEnded = gameEnded,
                            FinalPoints = finalPoints,
                            FinalScoreString = finalScoreString
                        });
                    }
                }
            }

            // keep track of number of WRs, RBs, and TEs, so we know if we are adding a FLEX or not
            int rosterOneWrCount = 0;
            int rosterTwoWrCount = 0;
            int rosterOneRbCount = 0;
            int rosterTwoRbCount = 0;
            int rosterOneTeCount = 0;
            int rosterTwoTeCount = 0;

            // go through each player on the roster and create the player and add them to the hashtable
            // TODO: once this is put in the UI where players are selected from a dropdown, we'll put the player ID in the CurrentRoster
            // table and use that for searching below
            foreach (RosterPlayer rosterPlayer in rosterPlayers)
            {
                sql = "SELECT ts.EspnGameId, p.EspnPlayerId, ts.HomeOrAway, p.PlayerName, p.TeamAbbreviation, ts.OpponentAbbreviation, p.Position, ts.GameDate " +
                        "from Players p " +
                        "join TeamsSchedule ts on ts.TeamId = p.TeamId " +
                        //"where p.PlayerName = '" + rosterPlayer.PlayerName.Replace("'", "''") + "' and p.Position = '" + rosterPlayer.Position + "' and ts.Week = " + rosterPlayer.Week;
                        "where p.PlayerName = '" + rosterPlayer.PlayerName.Replace("'", "''") + "' and ts.Week = " + rosterPlayer.Week;

                using (SqlCommand command = new SqlCommand(sql, sqlConnection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string espnGameId = ((int)reader.GetValue(reader.GetOrdinal("EspnGameId"))).ToString();
                            string espnPlayerId = ((int)reader.GetValue(reader.GetOrdinal("EspnPlayerId"))).ToString();
                            string homeOrAway = reader.GetValue(reader.GetOrdinal("HomeOrAway")).ToString();
                            string teamAbbreviation = reader.GetValue(reader.GetOrdinal("TeamAbbreviation")).ToString();
                            string opponentAbbreviation = reader.GetValue(reader.GetOrdinal("OpponentAbbreviation")).ToString();
                            DateTime gameDate = DateTime.Parse((reader.GetValue(reader.GetOrdinal("GameDate")).ToString()));

                            // we need to save the full player name into a search string so we don't modify the full name. This is mostly
                            // due to a defense such as "los angeles rams" and "los angeles chargers" only able to be searched by "los angeles",
                            // so we cannot lose the full name. We will cut off the "rams" or "chargers" part in the DEF case below
                            string playerNameSearchString = rosterPlayer.PlayerName;

                            SelectedPlayer player;

                            Position positionType = Position.FLEX;

                            switch (rosterPlayer.Position)
                            {
                                case Position.QB:
                                    positionType = Position.QB;
                                    break;

                                case Position.RB:
                                    if (rosterPlayer.Owner.Equals("Liz"))
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
                                    else if (rosterPlayer.Owner.Equals("Chris"))
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
                                    if (rosterPlayer.Owner.Equals("Liz"))
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
                                    else if (rosterPlayer.Owner.Equals("Chris"))
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
                                    if (rosterPlayer.Owner.Equals("Liz"))
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
                                    else if (rosterPlayer.Owner.Equals("Chris"))
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
                                    // if this is a defense, we need to strip off the last part of the team name (so buffalo instead of buffalo bills)
                                    int lastSpaceIndex = playerNameSearchString.LastIndexOf(" ");
                                    playerNameSearchString = playerNameSearchString.Substring(0, lastSpaceIndex);

                                    positionType = Position.DEF;
                                    break;

                                default:
                                    // TODO: ADD SOME ERROR STATE
                                    positionType = Position.FLEX;
                                    break;
                            }

                            // if the name contains an apostraphe, replace all occurences with the URL encoding of an apostrophe (%27),
                            // then encode the result, which will change the %27 into %2527
                            playerNameSearchString = HttpUtility.UrlEncode(playerNameSearchString.Replace("'", HttpUtility.UrlEncode("'")));

                            player = await CreatePlayer("https://fantasysports.yahooapis.com/fantasy/v2/league/406.l.244561/players;search=" + playerNameSearchString + "/stats", rosterPlayer.Position, positionType, espnPlayerId, espnGameId, gameDate, homeOrAway, rosterPlayer.PlayerName, teamAbbreviation, opponentAbbreviation, rosterPlayer.Owner, rosterPlayer.Logo, rosterPlayer.GameEnded, rosterPlayer.FinalPoints, rosterPlayer.FinalScoreString);

                            addPlayerToHashtable(testPlayers, espnGameId, player);
                        }
                    }
                }
            }

            await sqlConnection.CloseAsync();

            return testPlayers;
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
            SessionExtensions.SetString(HttpContext.Session, SessionKeyWeek, week);

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
            string week = SessionExtensions.GetString(HttpContext.Session, SessionKeyWeek);

            // we need to first check to make sure the token isn't null (if the site hasn't been refreshed in a while and
            // is attempted to be refreshed on the scoreboard, it will be null
            if (AuthModel.AccessToken != null)
            {
                // populate the week dropdown with all weeks a matchup has been played
                ViewBag.weeks = GetGameWeeks(week);

                List<Team> teams = new List<Team>();

                // TESTING pulling multiple players back from Yahoo API
                /*client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AuthModel.AccessToken);
                HttpRequestMessage request = new HttpRequestMessage();
                request.RequestUri = new Uri("https://fantasysports.yahooapis.com/fantasy/v2/league/nfl.l.434497/players;player_keys={30123},{30977}");
                request.Method = HttpMethod.Get;
                var response2 = client.GetAsync(request.RequestUri);
                string testResponse = await response2.Result.Content.ReadAsStringAsync();*/

                // TESTING
                //client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.AccessToken);
                //string LoginString = ";use_login=1";
                //request = new HttpRequestMessage();
                //request.RequestUri = new Uri("https://fantasysports.yahooapis.com/fantasy/v2/game/nfl");

                // My league ID, which can only be retrieved manually from going to League-->settings, is 434497
                //request.RequestUri = new Uri("https://fantasysports.yahooapis.com/fantasy/v2/league/nfl.l.434497");

                // Request all teams owned by loggedin user
                // Within a team, there is a <team> element with a <team_key> and <team_id> which can be used later to get information on the team
                //request.RequestUri = new Uri("https://fantasysports.yahooapis.com/fantasy/v2/users;use_login=1/games;game_keys=nfl/teams");

                //request.RequestUri = new Uri("https://fantasysports.yahooapis.com/fantasy/v2/game/nfl");

                // team/<team_key>/roster
                // this will return a list of players and their <player_key>'s and <player_id>'s
                //request.RequestUri = new Uri("https://fantasysports.yahooapis.com/fantasy/v2/team/406.l.244561.t.5/roster");

                // request from the current league <league_id is 406.l.244561> and current player <this is a specific player id>
                //request.RequestUri = new Uri("https://fantasysports.yahooapis.com/fantasy/v2/league/406.l.244561/players;player_keys=406.p.33391/stats");

                // reqeusting player status in a league using a search query for player name (trey lance as an example)
                //request.RequestUri = new Uri("https://fantasysports.yahooapis.com/fantasy/v2/league/406.l.244561/players;search=trey lance/stats");

                // Get the Hashtable which will store players grouped by espn game id so we can parse all players in the same doc, limiting
                // the number of times we need to download the doc
                Hashtable testPlayers = await createPlayersHashtable(week);

                // each thread will need a done event signifying when the thread has completed, so create a list of
                // done events for each thread (for each espn game id)
                //var doneEvents = new ManualResetEvent[testPlayers.Keys.Count];

                // counter for the doneEvents array
                int i = 0;

                var tasks = new Task[testPlayers.Keys.Count];

                // loop through each key (espn game id) and parse the points for each player in that game,
                // adding each SelectedPlayer in the hashtable to the approprate list of teams (team one or team two)
                foreach (string key in testPlayers.Keys)
                {
                    // allocating a new week variable for the thread
                    string tempWeek = week;

                    List<SelectedPlayer> playersInGame = (List<SelectedPlayer>)testPlayers[key];
                    tasks[i] = Task.Factory.StartNew(() => scrapeStatsFromGame(key, playersInGame, tempWeek));
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
                List<List<SelectedPlayer>> listOfPlayerLists = testPlayers.Values.OfType<List<SelectedPlayer>>().ToList();
                List<SelectedPlayer> players = listOfPlayerLists.SelectMany(x => x).ToList();

                // Pull out the players for team one and sort by position
                List<SelectedPlayer> teamOnePlayers = players.Where(x => x.Owner.Equals("Liz")).ToList();
                teamOnePlayers = teamOnePlayers.OrderBy(x => (int)(x.Position)).ToList();

                // Total up the scores from this team
                List<double> pointsList = teamOnePlayers.Select(x => x.Points).ToList();
                double points = pointsList.Sum();

                Team team = new Team
                {
                    Owner = "Liz",
                    OwnerLogo = teamOnePlayers[0].OwnerLogo,
                    TotalFantasyPoints = Math.Round(points, 2),
                    Players = teamOnePlayers
                };

                teams.Add(team);

                // Pull out the players for team two and sort by position
                List<SelectedPlayer> teamTwoPlayers = players.Where(x => x.Owner.Equals("Chris")).ToList();
                teamTwoPlayers = teamTwoPlayers.OrderBy(x => (int)(x.Position)).ToList();

                // Total up the scores from this team
                pointsList = teamTwoPlayers.Select(x => x.Points).ToList();
                points = pointsList.Sum();

                team = new Team
                {
                    Owner = "Chris",
                    OwnerLogo = teamTwoPlayers[0].OwnerLogo,
                    TotalFantasyPoints = Math.Round(points, 2),
                    Players = teamTwoPlayers
                };

                teams.Add(team);

                return View(teams);
            }
            else
            {
                // redirect to the home controller's index action so we can get a new token
                return RedirectToAction("Index", "Home");
            }
        }

        /// <summary>
        /// Scrapes stats for each player playing in a particular game. This is called in a thread so when the work
        /// is done, the thread will report that it has completed by calling the signalThread method
        /// </summary>
        /// <param name="espnGameId">ESPN Game ID for the players on either roster</param>
        /// <param name="players">All players playing in the given ESPN Game ID</param>
        /// <param name="week">The week stats are being scraped for, which is only used when updating the final score in the database</param>
        //private void scrapeStatsFromGame(Object state)
        private void scrapeStatsFromGame(string espnGameId, List<SelectedPlayer> players, string week)
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
                    p.Points += scraper.parseGameTrackerPage(espnGameId, p.EspnPlayerId, p.HomeOrAway, p.OpponentAbbreviation);
                    //p.Points += scraper.parseTwoPointConversionsForPlayer(stateInfo.EspnGameId, p.RawPlayerName);
                    p.Points += scraper.parseTwoPointConversionsForPlayer(p.RawPlayerName);
                    p.TimeRemaining = scraper.parseTimeRemaining();
                    p.CurrentScoreString = scraper.parseCurrentScore(p.HomeOrAway);

                    // calculate kicker FGs if this player is a kicker
                    if (p.Position == Position.K)
                    {
                        //p.Points += scraper.parseFieldGoals(stateInfo.EspnGameId, p.RawPlayerName);
                        p.Points += scraper.parseFieldGoals(p.RawPlayerName);
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

                        updateCurrentRosterWithFinalScore(p.Owner, p.RawPlayerName, p.Points, finalScoreString, week);
                    }
                }
            }

            // all of the work is done, so signal the thread that it's complete so the ThreadPool will be notified
            // stateInfo.DoneEvent.Set();
        }

        /// <summary>
        /// Gets from the CurrentRoster table a list of all weeks a matchup has been played.
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

            var tokenRequestContext = new TokenRequestContext(new[] { "https://database.windows.net//.default" });
            var tokenRequestResult = new DefaultAzureCredential().GetToken(tokenRequestContext);

            // THIS MAY TAKE A LONG TIME (NEED TO TEST FURTHER) - CAN THIS BE STORED SOMEWHERE SO ALL THREADS CAN USE IT?
            sqlConnection.AccessToken = tokenRequestResult.Token;

            sqlConnection.Open();

            string sql = "select distinct week from CurrentRoster";

            using (SqlCommand command = new SqlCommand(sql, sqlConnection))
            {
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string week = reader["week"].ToString();

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

            sqlConnection.Close();

            return weeks;
        }

        /// <summary>
        /// Within the thread that updates the player points, if the scraper determins the game has ended, this
        /// method is called to update the CurrentRoster table for this particular player setting GameEnded to true
        /// and updating the FinalPoints field, so we can grab this the next time the app displays the scores rather
        /// than scrap the gametracker page again.
        /// </summary>
        /// <param name="ownerName">The owner of the team</param>
        /// <param name="playerName">The player whose points we are updating in the CurrentRoster table</param>
        /// <param name="playerFinalScore">The final score the player got in the game</param>
        /// <param name="finalScoreString">The final score string for the player's team, which is displayed in the UI</param>
        /// <param name="week">The week we are updating</param>
        private void updateCurrentRosterWithFinalScore(string ownerName, string playerName, double playerFinalScore, string finalScoreString, string week)
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder
            {
                DataSource = "tcp:playersandscheduledetails.database.windows.net,1433",
                InitialCatalog = "PlayersAndSchedulesDetails",
                TrustServerCertificate = false,
                Encrypt = true
            };

            var sqlConnection = new SqlConnection(connectionStringBuilder.ConnectionString);

            var tokenRequestContext = new TokenRequestContext(new[] { "https://database.windows.net//.default" });
            var tokenRequestResult = new DefaultAzureCredential().GetToken(tokenRequestContext);

            // THIS MAY TAKE A LONG TIME (NEED TO TEST FURTHER) - CAN THIS BE STORED SOMEWHERE SO ALL THREADS CAN USE IT?
            sqlConnection.AccessToken = tokenRequestResult.Token;

            // TODO: When the players are picked from the form, we should be using the player id to update the table
            // update the specific player and add a double apostrophe to the name if there is one in the name (such as in Ja'Marr Chase)
            // TODO: RENAME DB FinalPointsString to FinalScoreString
            string sql = "update CurrentRoster " +
                         "SET GameEnded = 'true', FinalPoints = " + playerFinalScore + ", FinalPointsString = '" + finalScoreString + "' " +
                         "FROM CurrentRoster " +
                         "INNER JOIN Owners on Owners.OwnerName ='" + ownerName + "' and PlayerName = '" + playerName.Replace("'", "''") + "'" + " and Week = '" + week + "'";

            sqlConnection.Open();

            using (SqlCommand command = new SqlCommand(sql, sqlConnection))
            {
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

        /// <summary>
        /// Creates the player based on the api Query for the player. THe other details for espn are supplied
        /// so that we can scrape the espn boxscore and playbyplay pages to get live stats.
        /// </summary>
        /// <param name="apiQuery">Yahoo API query for the player</param>
        /// <param name="truePosition">The true position which will not be a WR/RB/TE changed to a FLEX. This is used so the
        /// players true position can be displayed under their name in the FLEX row of the UI</param>
        /// <param name="position">Position of the player (QB, RB, WR, TE, FLEX, K, DEF), which includes FLEX and is used so the players
        /// can be sorted according to the Position enum and the FLEX player is displayed in the proper row in the UI</param>
        /// <param name="espnPlayerId">Player ID on ESPN so we can parse stats for this player on ESPNs pages</param>
        /// <param name="espnGameId">Game ID on ESPN so we can parse the correct game to get stats for this player</param>
        /// <param name="gameTime">Time the game is starting</param>
        /// <param name="homeOrAway">"home" or "away" game, which is needed to find the correct stats on ESPN for the player</param>
        /// <param name="playerName">Player's name which is only used to search for 2-point conversions in the ESPN play by play page</param>
        /// <param name="teamAbbreviation">Player's team abbreviation</param>
        /// <param name="opponentAbbreviation">If this palyer is a defense, this parameter is the abbreviation of their opponent</param>
        /// <param name="gameEnded">The db will have a GameEnded flag set whether the player's game has ended or not</param>
        /// <param name="finalPoints">If this player's game has ended, they'll have final points, otherwise they'll have 0</param>
        /// <param name="finalScoreString">If this player's game has ended, they'll have a final score string to display such as "(W) 45 - 30")</param>
        /// <returns></returns>
        private async Task<SelectedPlayer> CreatePlayer(string apiQuery, Position truePosition, Position position, string espnPlayerId, string espnGameId, DateTime gameTime, string homeOrAway, string playerName, string teamAbbreviation, string opponentAbbreviation, string owner, byte[] logo, bool gameEnded, double finalPoints, string finalScoreString)
        {
            //HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AuthModel.AccessToken);

            HttpRequestMessage request = new HttpRequestMessage();
            request.RequestUri = new Uri(apiQuery);
            request.Method = HttpMethod.Get;
            var response2 = client.GetAsync(request.RequestUri);
            string testResponse = await response2.Result.Content.ReadAsStringAsync();

            XDocument document = XDocument.Parse(testResponse);

            XmlSerializer serializer = new XmlSerializer(typeof(Player));
            List<XElement> xElements = document.Descendants(YahooXml.XMLNS + "player").ToList();
            //List<XElement> xElements = document.Descendants().ToList();
            //List<Player> collection = new List<Player>();
            // we are only looking at one player, so commenting out for now
            /*foreach (var element in xElements)
            {
                collection.Add((Player)serializer.Deserialize(element.CreateReader()));
            }*/

            // Player object pulled from the Yahoo API request
            Player player = null;

            // if we have a player (or defense, such as Los Angeles) with the same name, we need
            // to check the player name in each xElement to select the right team
            if (xElements.Count > 1)
            {
                foreach (XElement xElement in xElements)
                {
                    if (xElement.Value.ToLower().Contains(playerName.ToLower()))
                    {
                        player = (Player)serializer.Deserialize(xElement.CreateReader());

                        break;
                    }
                }
            }
            else
            {
                player = (Player)serializer.Deserialize(xElements.First().CreateReader());
            }

            // Player we create from the data returned from Yahoo's API as well as fantasy points we scrape from the espn grametracker
            // and play by play pages
            SelectedPlayer selectedPlayer = new SelectedPlayer();
            selectedPlayer.Name = player.Name.First + " " + player.Name.Last;
            selectedPlayer.Headshot = player.Headshot.Url;
            selectedPlayer.TruePosition = truePosition;
            selectedPlayer.Position = position;
            selectedPlayer.EspnGameId = espnGameId;
            selectedPlayer.GameTime = gameTime;
            selectedPlayer.EspnPlayerId = espnPlayerId;
            selectedPlayer.TeamAbbreviation = teamAbbreviation;
            selectedPlayer.OpponentAbbreviation = opponentAbbreviation;
            selectedPlayer.RawPlayerName = playerName;
            selectedPlayer.HomeOrAway = homeOrAway;
            selectedPlayer.Owner = owner;
            selectedPlayer.OwnerLogo = logo;
            selectedPlayer.GameEnded = gameEnded;
            selectedPlayer.Points = finalPoints;
            selectedPlayer.FinalScoreString = finalScoreString;

            return selectedPlayer;
        }

        // Maintain state to pass to the scrapeStatsFromGame method
        public class State
        {
            public string EspnGameId { get; set; }

            public Hashtable players { get; set; }

            List<Team> Teams { get; set; }

            public ManualResetEvent DoneEvent { get; set; }
        }
    }
}