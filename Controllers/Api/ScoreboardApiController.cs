using FantasyFootballStatTracker.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Collections;
using FantasyFootballStatTracker.Infrastructure;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Globalization;
using Azure.Core;
using Azure.Identity;

namespace FantasyFootballStatTracker.Controllers.Api
{
    [ApiController]
    [Route("api/scoreboard")]
    public class ScoreboardApiController : ControllerBase
    {
        private readonly ILogger<ScoreboardApiController> _logger;

        /// <summary>
        /// Session key for the Azure SQL Access token
        /// </summary>
        public const string SessionKeyAzureSqlAccessToken = "_Token";

        /// <summary>
        /// Stores the owner logos
        /// </summary>
        private List<byte[]> OwnerLogos;

        public ScoreboardApiController(ILogger<ScoreboardApiController> logger)
        {
            _logger = logger;
        }

        [HttpGet("teams/{week}")]
        public async Task<ActionResult<List<TeamDto>>> GetTeams(int week)
        {
            try
            {
                // Get the teams for the specified week
                var teams = await GetTeamsForWeek(week.ToString());
                
                // Convert to DTOs for API response
                var teamDtos = teams.Select(team => new TeamDto
                {
                    OwnerId = team.OwnerId,
                    Week = team.Week,
                    TotalFantasyPoints = team.TotalFantasyPoints,
                    OwnerLogo = Convert.ToBase64String(team.OwnerLogo),
                    Players = team.Players.Select(player => new PlayerDto
                    {
                        Name = player.Name,
                        Headshot = player.Headshot,
                        TruePosition = player.TruePosition.ToString(),
                        Position = player.Position.ToString(),
                        TeamAbbreviation = player.TeamAbbreviation,
                        OpponentAbbreviation = player.OpponentAbbreviation,
                        HomeOrAway = player.HomeOrAway,
                        Points = player.Points,
                        GameInProgress = player.GameInProgress,
                        GameEnded = player.GameEnded,
                        GameCanceled = player.GameCanceled,
                        GameTime = player.GameTime,
                        TimeRemaining = player.TimeRemaining,
                        CurrentScoreString = player.CurrentScoreString,
                        FinalScoreString = player.FinalScoreString
                    }).ToList()
                }).ToList();

                return Ok(teamDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting teams for week {Week}", week);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("weeks")]
        public async Task<ActionResult<List<WeekDto>>> GetAvailableWeeks()
        {
            try
            {
                var weeks = await GetGameWeeks(null);
                var weekDtos = weeks.Select(w => new WeekDto
                {
                    Value = w.Value,
                    Text = w.Text,
                    Selected = w.Selected
                }).ToList();

                return Ok(weekDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available weeks: {Message}", ex.Message);
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        [HttpPost("week/{week}")]
        public ActionResult UpdateWeek(int week)
        {
            try
            {
                // Store week in session
                HttpContext.Session.SetString("_Week", week.ToString());
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating week to {Week}", week);
                return StatusCode(500, "Internal server error");
            }
        }

        // Implementation methods copied from ScoreboardController
        private async Task<List<Team>> GetTeamsForWeek(string week)
        {
            List<Team> teams = new List<Team>();
            
            // Get the Hashtable which will store players grouped by espn game id
            Hashtable playersHashTable = await createPlayersHashtable(week);

            var tasks = new Task[playersHashTable.Keys.Count];
            int i = 0;

            // Process players for each game
            foreach (string key in playersHashTable.Keys)
            {
                List<SelectedPlayer> playersInGame = (List<SelectedPlayer>)playersHashTable[key];
                tasks[i] = Task.Factory.StartNew(() => scrapeStatsFromGame(key, playersInGame));
                i++;
            }

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

            // Flatten the players list
            List<List<SelectedPlayer>> listOfPlayerLists = playersHashTable.Values.OfType<List<SelectedPlayer>>().ToList();
            List<SelectedPlayer> players = listOfPlayerLists.SelectMany(x => x).ToList();

            // Create Team 1
            List<SelectedPlayer> teamOnePlayers = players.Where(x => x.OwnerId == 1).ToList();
            teamOnePlayers = teamOnePlayers.OrderBy(x => (int)(x.Position)).ToList();
            double team1Points = teamOnePlayers.Select(x => x.Points).Sum();

            Team team1 = new Team
            {
                OwnerId = 1,
                Week = int.Parse(week),
                OwnerLogo = OwnerLogos[0],
                TotalFantasyPoints = Math.Round(team1Points, 2),
                Players = teamOnePlayers
            };

            teams.Add(team1);

            // Create Team 2
            List<SelectedPlayer> teamTwoPlayers = players.Where(x => x.OwnerId == 2).ToList();
            teamTwoPlayers = teamTwoPlayers.OrderBy(x => (int)x.Position).ToList();
            double team2Points = teamTwoPlayers.Select(x => x.Points).Sum();

            Team team2 = new Team
            {
                OwnerId = 2,
                Week = int.Parse(week),
                OwnerLogo = OwnerLogos[1],
                TotalFantasyPoints = Math.Round(team2Points, 2),
                Players = teamTwoPlayers
            };

            teams.Add(team2);

            return teams;
        }

        private async Task<Hashtable> createPlayersHashtable(string selectedWeek)
        {
            Hashtable playersHashTable = new Hashtable();

            var connectionStringBuilder = new SqlConnectionStringBuilder
            {
                DataSource = "tcp:playersandschedulesdetails.database.windows.net,1433",
                InitialCatalog = "PlayersAndSchedulesDetails",
                TrustServerCertificate = false,
                Encrypt = true
            };

            string azureSqlToken = HttpContext.Session.GetString(SessionKeyAzureSqlAccessToken);

            if (azureSqlToken == null)
            {
                azureSqlToken = await GetAzureSqlAccessToken();
                HttpContext.Session.SetString(SessionKeyAzureSqlAccessToken, azureSqlToken);
            }

            SqlConnection sqlConnection = new SqlConnection(connectionStringBuilder.ConnectionString);
            sqlConnection.AccessToken = azureSqlToken;

            await sqlConnection.OpenAsync();

            // Get owner logos
            OwnerLogos = HttpContext.Session.GetObjectFromJson<List<byte[]>>(Infrastructure.SessionExtensions.SessionKeyLogos);

            if (OwnerLogos == null)
            {
                OwnerLogos = new List<byte[]>();
                
                using (SqlCommand command = new SqlCommand("GetOwnerLogos", sqlConnection))
                {
                    command.CommandType = System.Data.CommandType.StoredProcedure;

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            OwnerLogos.Add((byte[])reader.GetValue(reader.GetOrdinal("Logo")));
                        }

                        HttpContext.Session.SetObjectAsJson(Infrastructure.SessionExtensions.SessionKeyLogos, OwnerLogos);
                    }
                }
            }

            // Get players for the specified week
            using (SqlCommand command = new SqlCommand("GetTeamsForGivenWeek", sqlConnection))
            {
                command.CommandType = System.Data.CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@week", System.Data.SqlDbType.Int) { Value = selectedWeek });

                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    int rosterOneWrCount = 0, rosterTwoWrCount = 0;
                    int rosterOneRbCount = 0, rosterTwoRbCount = 0;
                    int rosterOneTeCount = 0, rosterTwoTeCount = 0;

                    while (await reader.ReadAsync())
                    {
                        // Extract player data
                        int ownerId = (int)reader.GetValue(reader.GetOrdinal("OwnerID"));
                        string? ownerName = reader.GetValue(reader.GetOrdinal("OwnerName")).ToString();
                        string? playerName = reader.GetValue(reader.GetOrdinal("PlayerName")).ToString();
                        Position position = (Position)Enum.Parse(typeof(Position), reader.GetValue(reader.GetOrdinal("Position")).ToString().Trim());
                        bool gameEnded = (bool)reader.GetValue(reader.GetOrdinal("GameEnded"));
                        bool gameCanceled = (bool)reader.GetValue(reader.GetOrdinal("GameCanceled"));
                        double finalPoints = (double)reader.GetValue(reader.GetOrdinal("FinalPoints"));
                        string? finalScoreString = reader.GetValue(reader.GetOrdinal("FinalScoreString")).ToString();
                        string? espnPlayerId = reader.GetValue(reader.GetOrdinal("EspnPlayerId")).ToString();
                        string? headshotUrl = reader.GetValue(reader.GetOrdinal("HeadshotUrl")).ToString();
                        string? espnGameId = ((int)reader.GetValue(reader.GetOrdinal("EspnGameId"))).ToString();
                        string? homeOrAway = reader.GetValue(reader.GetOrdinal("HomeOrAway")).ToString();
                        string? teamName = reader.GetValue(reader.GetOrdinal("TeamName")).ToString();
                        string? teamAbbreviation = reader.GetValue(reader.GetOrdinal("TeamAbbreviation")).ToString();
                        string? opponentAbbreviation = reader.GetValue(reader.GetOrdinal("OpponentAbbreviation")).ToString();
                        DateTime gameDate = DateTime.Parse((reader.GetValue(reader.GetOrdinal("GameDate")).ToString()));

                        // Determine position type (including FLEX logic)
                        Position positionType = DeterminePositionType(position, ownerId, ref rosterOneWrCount, ref rosterTwoWrCount, 
                            ref rosterOneRbCount, ref rosterTwoRbCount, ref rosterOneTeCount, ref rosterTwoTeCount);

                        // Handle defense name formatting
                        if (position == Position.DEF)
                        {
                            playerName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(playerName);
                        }

                        SelectedPlayer player = new SelectedPlayer
                        {
                            Name = playerName ?? string.Empty,
                            Headshot = headshotUrl ?? string.Empty,
                            TruePosition = position,
                            Position = positionType,
                            EspnGameId = espnGameId,
                            GameTime = gameDate,
                            EspnPlayerId = espnPlayerId ?? string.Empty,
                            TeamName = teamName ?? string.Empty,
                            TeamAbbreviation = teamAbbreviation ?? string.Empty,
                            OpponentAbbreviation = opponentAbbreviation ?? string.Empty,
                            HomeOrAway = homeOrAway ?? string.Empty,
                            OwnerId = ownerId,
                            OwnerName = ownerName ?? string.Empty,
                            GameEnded = gameEnded,
                            GameCanceled = gameCanceled,
                            Points = finalPoints,
                            FinalScoreString = finalScoreString ?? string.Empty,
                            Week = int.Parse(selectedWeek)
                        };

                        addPlayerToHashtable(playersHashTable, espnGameId, player);
                    }
                }
            }

            await sqlConnection.CloseAsync();
            return playersHashTable;
        }

        private Position DeterminePositionType(Position position, int ownerId, ref int rosterOneWrCount, ref int rosterTwoWrCount,
            ref int rosterOneRbCount, ref int rosterTwoRbCount, ref int rosterOneTeCount, ref int rosterTwoTeCount)
        {
            switch (position)
            {
                case Position.QB:
                case Position.K:
                case Position.DEF:
                    return position;

                case Position.RB:
                    if (ownerId == 1)
                    {
                        if (rosterOneRbCount == 2) return Position.FLEX;
                        rosterOneRbCount++;
                        return Position.RB;
                    }
                    else
                    {
                        if (rosterTwoRbCount == 2) return Position.FLEX;
                        rosterTwoRbCount++;
                        return Position.RB;
                    }

                case Position.WR:
                    if (ownerId == 1)
                    {
                        if (rosterOneWrCount == 2) return Position.FLEX;
                        rosterOneWrCount++;
                        return Position.WR;
                    }
                    else
                    {
                        if (rosterTwoWrCount == 2) return Position.FLEX;
                        rosterTwoWrCount++;
                        return Position.WR;
                    }

                case Position.TE:
                    if (ownerId == 1)
                    {
                        if (rosterOneTeCount == 1) return Position.FLEX;
                        rosterOneTeCount++;
                        return Position.TE;
                    }
                    else
                    {
                        if (rosterTwoTeCount == 1) return Position.FLEX;
                        rosterTwoTeCount++;
                        return Position.TE;
                    }

                default:
                    return Position.FLEX;
            }
        }

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

        private async Task scrapeStatsFromGame(string espnGameId, List<SelectedPlayer> players)
        {
            // Check the date time of the game for the first player (it is the same for all players in this list
            // since they belong to the same game) and if it hasn't started, just set the points equal to 0
            // and don't load the document.
            SelectedPlayer player = players[0];

            // Get current EST time - If this is run on a machine with a differnet local time, DateTime.Now will not return the proper time
            TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            DateTime currentEasterStandardTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, easternZone);
            TimeSpan difference = player.GameTime.Subtract(currentEasterStandardTime);

            // if the game hasn't started or the game has ended or is canceled, don't load the HtmlDoc to parse stats since we've already done that
            if ((difference.TotalDays < 0) && !player.GameEnded && !player.GameCanceled)
            {
                EspnHtmlScraper scraper = new EspnHtmlScraper(espnGameId);

                // calculate points for each of these players
                foreach (SelectedPlayer p in players)
                {
                    // set flag to true to indicate this player's game is in progress
                    p.GameInProgress = true;

                    p.Points += scraper.parseGameTrackerPage(espnGameId, p.EspnPlayerId, p.Position, p.TeamName, p.TeamAbbreviation, p.OpponentAbbreviation);
                    p.Points += scraper.parseOffensivePlayerFumbleRecoveryForTouchdown(p.Name);
                    p.Points += scraper.parseTwoPointConversionsForPlayer(p.Name);
                    p.TimeRemaining = scraper.parseTimeRemaining();
                    p.CurrentScoreString = scraper.parseCurrentScore(p.TeamAbbreviation);

                    // calculate kicker FGs if this player is a kicker
                    if (p.Position == Position.K)
                    {
                        p.Points += scraper.parseFieldGoals(p.Name);
                    }

                    // check the scraper to see if the game has ended and update this player row
                    if (scraper.GameEnded || scraper.GameCanceled)
                    {
                        // set the flag to false since this game is no longer in progress
                        p.GameInProgress = false;

                        // Sets the appropriate flag to true depending on the situation.  If the game ended, the view will check this and
                        // update the final score string (a game can be not started - therefore not in progress - and also not ended, so we
                        // wouldn't want to display the final score in this case); if the game was canceled, the view will not print the
                        // final score string since there will not be a score for a canceled game.
                        p.GameEnded = scraper.GameEnded;
                        p.GameCanceled = scraper.GameCanceled;

                        // Get the final score string (such as "(W) 45 - 30") and store this in the database
                        string finalScoreString = scraper.parseFinalScore(p.TeamAbbreviation);

                        p.FinalScoreString = finalScoreString;

                        await updateCurrentRosterWithFinalScore(p.GameEnded, p.GameCanceled, p.OwnerId, p.EspnPlayerId, p.Points, finalScoreString, p.Week);
                    }
                }
            }
        }

        private async Task updateCurrentRosterWithFinalScore(bool gameEnded, bool gameCanceled, int ownerId, string espnPlayerId, double playerFinalScore, string finalScoreString, int week)
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder
            {
                DataSource = "tcp:playersandschedulesdetails.database.windows.net,1433",
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

            using (SqlCommand command = new SqlCommand("UpdatePlayerFinalScore", sqlConnection))
            {
                command.CommandType = System.Data.CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@GameEnded", System.Data.SqlDbType.Bit) { Value = gameEnded });
                command.Parameters.Add(new SqlParameter("@GameCanceled", System.Data.SqlDbType.Bit) { Value = gameCanceled });
                command.Parameters.Add(new SqlParameter("@PlayerFinalScore", System.Data.SqlDbType.Float) { Value = playerFinalScore });
                command.Parameters.Add(new SqlParameter("@FinalScoreString", System.Data.SqlDbType.NVarChar) { Value = finalScoreString });
                command.Parameters.Add(new SqlParameter("@OwnerId", System.Data.SqlDbType.Int) { Value = ownerId });
                command.Parameters.Add(new SqlParameter("@EspnPlayerId", System.Data.SqlDbType.Int) { Value = int.Parse(espnPlayerId) });
                command.Parameters.Add(new SqlParameter("@Week", System.Data.SqlDbType.Int) { Value = week });

                command.ExecuteNonQuery();
            }

            sqlConnection.Close();
        }

        private async Task<List<SelectListItem>> GetGameWeeks(string selectedWeek)
        {
            List<SelectListItem> weeks = new List<SelectListItem>();

            var connectionStringBuilder = new SqlConnectionStringBuilder
            {
                DataSource = "tcp:playersandschedulesdetails.database.windows.net,1433",
                InitialCatalog = "PlayersAndSchedulesDetails",
                TrustServerCertificate = false,
                Encrypt = true
            };

            string azureSqlToken = HttpContext.Session.GetString(SessionKeyAzureSqlAccessToken);

            if (azureSqlToken == null)
            {
                azureSqlToken = await GetAzureSqlAccessToken();
                HttpContext.Session.SetString(SessionKeyAzureSqlAccessToken, azureSqlToken);
            }

            SqlConnection sqlConnection = new SqlConnection(connectionStringBuilder.ConnectionString);
            sqlConnection.AccessToken = azureSqlToken;

            await sqlConnection.OpenAsync();

            string week = "0";

            using (SqlCommand command = new SqlCommand("GetAllWeeksPlayed", sqlConnection))
            {
                command.CommandType = System.Data.CommandType.StoredProcedure;

                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        week = reader["week"].ToString();

                        bool selected = selectedWeek == null ? true : week.Equals(selectedWeek);
                        weeks.Add(new SelectListItem(week, week, selected));
                    }
                }
            }

            sqlConnection.Close();
            return weeks;
        }

        private static async Task<string> GetAzureSqlAccessToken()
        {
            var tokenRequestContext = new TokenRequestContext(new[] { "https://database.windows.net//.default" });
            var tokenRequestResult = await new DefaultAzureCredential().GetTokenAsync(tokenRequestContext);
            return tokenRequestResult.Token;
        }
    }

    // DTOs for API responses
    public class TeamDto
    {
        public int OwnerId { get; set; }
        public int Week { get; set; }
        public double TotalFantasyPoints { get; set; }
        public string OwnerLogo { get; set; } = string.Empty; // Base64 encoded
        public List<PlayerDto> Players { get; set; } = new();
    }

    public class PlayerDto
    {
        public string Name { get; set; } = string.Empty;
        public string Headshot { get; set; } = string.Empty;
        public string TruePosition { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string TeamAbbreviation { get; set; } = string.Empty;
        public string OpponentAbbreviation { get; set; } = string.Empty;
        public string HomeOrAway { get; set; } = string.Empty;
        public double Points { get; set; }
        public bool GameInProgress { get; set; }
        public bool GameEnded { get; set; }
        public bool GameCanceled { get; set; }
        public DateTime GameTime { get; set; }
        public string TimeRemaining { get; set; } = string.Empty;
        public string CurrentScoreString { get; set; } = string.Empty;
        public string FinalScoreString { get; set; } = string.Empty;
    }

    public class WeekDto
    {
        public string Value { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public bool Selected { get; set; }
    }
}