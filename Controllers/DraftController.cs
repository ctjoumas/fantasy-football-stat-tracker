using Azure.Core;
using Azure.Identity;
using FantasyFootballStatTracker.Infrastructure;
using FantasyFootballStatTracker.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace FantasyFootballStatTracker.Controllers
{
    public class DraftController : Controller
    {
        private readonly ILogger<DraftController> _logger;
        private readonly IConfiguration _config;

        /// <summary>
        /// Session key for draft state
        /// </summary>
        public const string SessionKeyDraftState = "_DraftState";

        /// <summary>
        /// Session key for available players
        /// </summary>
        public const string SessionKeyAvailablePlayers = "_AvailablePlayers";

        /// <summary>
        /// Session key for the Azure SQL Access token
        /// </summary>
        public const string SessionKeyAzureSqlAccessToken = "_Token";

        /// <summary>
        /// App setting for the Season.
        /// </summary>
        public const string APP_SETTINGS_SEASON_NAME = "AppConfiguration:Season";

        public DraftController(ILogger<DraftController> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        private static async Task<string> GetAzureSqlAccessToken()
        {
            // See https://docs.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/services-support-managed-identities#azure-sql
            var tokenRequestContext = new TokenRequestContext(new[] { "https://database.windows.net//.default" });
            var tokenRequestResult = await new DefaultAzureCredential().GetTokenAsync(tokenRequestContext);

            return tokenRequestResult.Token;
        }

        /// <summary>
        /// GET: Draft/Start - Shows the draft order selection page
        /// </summary>
        public async Task<IActionResult> Start(int week)
        {
            // Initialize draft state
            var draftState = new DraftState
            {
                Owners = await GetOwners(),
                Week = week,
                CurrentPickOwnerId = 0, // not set yet; this will be set after selection is made
                FirstPickOwnerId = 0, // not set yet; this will be set after selection is made
                PickNumber = 1,
                TotalPicks = 18, // 9 players per team
                Owner1Roster = new List<DraftedPlayer>(),
                Owner2Roster = new List<DraftedPlayer>(),
                IsComplete = false
            };

            // Store draft state in session
            HttpContext.Session.SetObjectAsJson(SessionKeyDraftState, draftState);

            ViewData["Week"] = week;
            ViewData["DraftState"] = draftState;

            return View();
        }

        /// <summary>
        /// POST: Draft/StartDraft - Begins the draft with selected first pick owner
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> StartDraft(int week, int firstPickOwnerId)
        {
            var draftState = HttpContext.Session.GetObjectFromJson<DraftState>(SessionKeyDraftState);

            draftState.CurrentPickOwnerId = firstPickOwnerId;
            draftState.FirstPickOwnerId = firstPickOwnerId;

            // Store draft state in session
            HttpContext.Session.SetObjectAsJson(SessionKeyDraftState, draftState);

            // Load available players
            var availablePlayers = await GetAllAvailablePlayers(week);
            HttpContext.Session.SetObjectAsJson(SessionKeyAvailablePlayers, availablePlayers);

            return RedirectToAction("Index", new { week = week });
        }

        /// <summary>
        /// GET: Draft/Index - Main draft interface
        /// </summary>
        public IActionResult Index(int week)
        {
            var draftState = HttpContext.Session.GetObjectFromJson<DraftState>(SessionKeyDraftState);
            var availablePlayers = HttpContext.Session.GetObjectFromJson<List<EspnPlayer>>(SessionKeyAvailablePlayers);

            if (draftState == null)
            {
                // Draft not started, redirect to start page
                return RedirectToAction("Start", new { week = week });
            }

            if (draftState.IsComplete)
            {
                // Draft completed, redirect to scoreboard
                return RedirectToAction("Index", "Scoreboard");
            }

            ViewData["Week"] = week;
            ViewData["DraftState"] = draftState;
            ViewData["AvailablePlayers"] = availablePlayers ?? new List<EspnPlayer>();

            return View();
        }

        /// <summary>
        /// POST: Draft/MakePick - Process a draft pick
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> MakePick(int playerId)
        {
            var draftState = HttpContext.Session.GetObjectFromJson<DraftState>(SessionKeyDraftState);
            var availablePlayers = HttpContext.Session.GetObjectFromJson<List<EspnPlayer>>(SessionKeyAvailablePlayers);

            if (draftState == null || availablePlayers == null)
            {
                return Json(new { success = false, message = "Draft state not found" });
            }

            // Find the selected player
            var selectedPlayer = availablePlayers.FirstOrDefault(p => p.EspnPlayerId == playerId);
            if (selectedPlayer == null)
            {
                return Json(new { success = false, message = "Player not found" });
            }

            // Validate position limits
            var currentRoster = draftState.GetRosterForOwner(draftState.CurrentPickOwnerId);
            if (!CanDraftPosition(selectedPlayer.Position, currentRoster))
            {
                return Json(new { success = false, message = $"Cannot draft another {selectedPlayer.Position}. Position limit reached." });
            }

            // Create drafted player
            var draftedPlayer = new DraftedPlayer
            {
                EspnPlayerId = selectedPlayer.EspnPlayerId,
                PlayerName = selectedPlayer.PlayerName,
                Position = selectedPlayer.Position == "PK" ? "K" : selectedPlayer.Position,
                TeamAbbreviation = selectedPlayer.TeamAbbreviation,
                PickNumber = draftState.PickNumber,
                OwnerId = draftState.CurrentPickOwnerId
            };

            // Add to appropriate roster
            if (draftState.CurrentPickOwnerId == 1)
            {
                draftState.Owner1Roster.Add(draftedPlayer);
            }
            else
            {
                draftState.Owner2Roster.Add(draftedPlayer);
            }

            // Remove player from available players
            availablePlayers.Remove(selectedPlayer);

            // Advance the draft
            draftState.PickNumber++;

            // Check if draft is complete
            if (draftState.PickNumber > draftState.TotalPicks)
            {
                draftState.IsComplete = true;
                
                // Save rosters to database
                await SaveDraftedRosters(draftState);

                // Update session
                HttpContext.Session.SetObjectAsJson(SessionKeyDraftState, draftState);
                HttpContext.Session.SetObjectAsJson(SessionKeyAvailablePlayers, availablePlayers);

                return Json(new { 
                    success = true, 
                    draftComplete = true, 
                    redirectUrl = Url.Action("Index", "Scoreboard") 
                });
            }

            // Determine next pick owner (alternating)
            DetermineNextPickOwner(draftState);

            // Update session
            HttpContext.Session.SetObjectAsJson(SessionKeyDraftState, draftState);
            HttpContext.Session.SetObjectAsJson(SessionKeyAvailablePlayers, availablePlayers);

            return Json(new { 
                success = true, 
                draftComplete = false,
                currentPickOwner = draftState.CurrentPickOwnerId,
                pickNumber = draftState.PickNumber,
                playerName = selectedPlayer.PlayerName
            });
        }

        /// <summary>
        /// Validates if a position can be drafted based on current roster
        /// </summary>
        private bool CanDraftPosition(string position, List<DraftedPlayer> currentRoster)
        {
            // Convert PK to K for consistency
            var checkPosition = position == "PK" ? "K" : position;
            
            // Count current positions
            var positionCounts = currentRoster.GroupBy(p => p.Position == "PK" ? "K" : p.Position)
                                            .ToDictionary(g => g.Key, g => g.Count());

            // Position limits
            var limits = new Dictionary<string, int>
            {
                ["QB"] = 1,
                ["K"] = 1,
                ["DEF"] = 1,
                ["RB"] = 3,    // 2 regular + 1 FLEX
                ["WR"] = 3,    // 2 regular + 1 FLEX
                ["TE"] = 2     // 1 regular + 1 FLEX
            };

            var currentCount = positionCounts.ContainsKey(checkPosition) ? positionCounts[checkPosition] : 0;
            var limit = limits.ContainsKey(checkPosition) ? limits[checkPosition] : 0;

            return currentCount < limit;
        }

        /// <summary>
        /// Determines the next pick owner using alternating pattern
        /// </summary>
        private void DetermineNextPickOwner(DraftState draftState)
        {
            draftState.CurrentPickOwnerId = draftState.CurrentPickOwnerId == 1 ? 2 : 1;
        }

        /// <summary>
        /// Gets the owners
        /// </summary>
        private async Task<List<Owner>> GetOwners()
        {
            var owners = new List<Owner>();

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

            using (SqlCommand command = new SqlCommand("GetOwners", sqlConnection))
            {
                command.CommandType = CommandType.StoredProcedure;

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        owners.Add(new Owner
                        {
                            OwnerId = int.Parse(reader["OwnerId"].ToString()),
                            OwnerName = reader["OwnerName"].ToString()
                        });
                    }
                }
            }

            sqlConnection.Close();

            return owners;
        }

        /// <summary>
        /// Gets all available players for the week
        /// </summary>
        private async Task<List<EspnPlayer>> GetAllAvailablePlayers(int week)
        {
            var players = new List<EspnPlayer>();

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

            using (SqlCommand command = new SqlCommand("GetAvailablePlayersForWeek", sqlConnection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@week", SqlDbType.Int) { Value = week });

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        players.Add(new EspnPlayer
                        {
                            EspnPlayerId = int.Parse(reader["EspnPlayerId"].ToString()),
                            PlayerName = reader["PlayerName"].ToString(),
                            Position = reader["Position"].ToString(),
                            TeamAbbreviation = reader["TeamAbbreviation"].ToString()
                        });
                    }
                }
            }

            sqlConnection.Close();
            return players.OrderBy(p => p.PlayerName).ToList();
        }

        /// <summary>
        /// Saves the drafted rosters to the database
        /// </summary>
        private async Task SaveDraftedRosters(DraftState draftState)
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

            // Save Owner 1 roster
            foreach (var player in draftState.Owner1Roster)
            {
                SaveDraftedPlayer(sqlConnection, player, draftState.Week);
            }

            // Save Owner 2 roster
            foreach (var player in draftState.Owner2Roster)
            {
                SaveDraftedPlayer(sqlConnection, player, draftState.Week);
            }

            sqlConnection.Close();
        }

        /// <summary>
        /// Saves a single drafted player to the database
        /// </summary>
        private void SaveDraftedPlayer(SqlConnection sqlConnection, DraftedPlayer player, int week)
        {
            string position = player.Position;

            // Convert PK to K for consistency
            if (position.Equals("PK"))
            {
                position = "K";
            }

            using (SqlCommand command = new SqlCommand("AddRosterPlayer", sqlConnection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@ownerId", SqlDbType.Int) { Value = player.OwnerId });
                command.Parameters.Add(new SqlParameter("@week", SqlDbType.Int) { Value = week });
                command.Parameters.Add(new SqlParameter("@playerName", SqlDbType.NVarChar) { Value = player.PlayerName });
                command.Parameters.Add(new SqlParameter("@position", SqlDbType.NChar) { Value = position });
                command.Parameters.Add(new SqlParameter("@espnPlayerId", SqlDbType.Int) { Value = player.EspnPlayerId });
                command.Parameters.Add(new SqlParameter("@Season", SqlDbType.Int) { Value = _config[APP_SETTINGS_SEASON_NAME] });

                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// POST: Draft/Reset - Resets the current draft (for testing)
        /// </summary>
        [HttpPost]
        public IActionResult Reset()
        {
            HttpContext.Session.Remove(SessionKeyDraftState);
            HttpContext.Session.Remove(SessionKeyAvailablePlayers);
            
            return Json(new { success = true });
        }

        /// <summary>
        /// GET: Draft/Test - Direct access to test the draft for any week (for testing)
        /// </summary>
        public IActionResult Test(int week = 15)
        {
            ViewData["TestMode"] = true;
            return RedirectToAction("Start", new { week = week });
        }

        /// <summary>
        /// GET: Draft/TestTools - Testing tools page
        /// </summary>
        public IActionResult TestTools()
        {
            return View("Test");
        }

        /// <summary>
        /// POST: Draft/ClearRosters - Clears rosters for a specific week (for testing)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ClearRosters(int week)
        {
            try
            {
                var connectionStringBuilder = new SqlConnectionStringBuilder
                {
                    DataSource = "tcp:playersandschedulesdetails.database.windows.net,1433",
                    InitialCatalog = "PlayersAndSchedulesDetails",
                    TrustServerCertificate = false,
                    Encrypt = true
                };

                string azureSqlToken = Microsoft.AspNetCore.Http.SessionExtensions.GetString(HttpContext.Session, SessionKeyAzureSqlAccessToken);

                if (azureSqlToken == null)
                {
                    azureSqlToken = await GetAzureSqlAccessToken();
                    Microsoft.AspNetCore.Http.SessionExtensions.SetString(HttpContext.Session, SessionKeyAzureSqlAccessToken, azureSqlToken);
                }

                SqlConnection sqlConnection = new SqlConnection(connectionStringBuilder.ConnectionString);
                sqlConnection.AccessToken = azureSqlToken;

                await sqlConnection.OpenAsync();

                // Clear rosters for the specified week
                using (SqlCommand command = new SqlCommand("DELETE FROM CurrentRoster WHERE Week = @week", sqlConnection))
                {
                    command.Parameters.Add(new SqlParameter("@week", SqlDbType.Int) { Value = week });
                    int rowsDeleted = command.ExecuteNonQuery();

                    sqlConnection.Close();

                    return Json(new { 
                        success = true, 
                        message = $"Cleared {rowsDeleted} roster entries for week {week}",
                        redirectUrl = Url.Action("Index", "Scoreboard")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing rosters for week {Week}", week);
                return Json(new { success = false, message = "Error clearing rosters: " + ex.Message });
            }
        }
    }
}