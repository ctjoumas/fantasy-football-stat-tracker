using Azure.Core;
using Azure.Identity;
using FantasyFootballStatTracker.Hubs;
using FantasyFootballStatTracker.Infrastructure;
using FantasyFootballStatTracker.Models;
using FantasyFootballStatTracker.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using System.Data;

namespace FantasyFootballStatTracker.Controllers
{
    public class DraftController : Controller
    {
        private readonly ILogger<DraftController> _logger;
        private readonly IConfiguration _config;

        private readonly IDraftStateService _draftStateService;
        private readonly IHubContext<DraftHub> _hubContext;

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

        public DraftController(ILogger<DraftController> logger, IDraftStateService draftStateService, IHubContext<DraftHub> hubContext, IConfiguration config)
        {
            _logger = logger;
            _draftStateService = draftStateService;
            _hubContext = hubContext;
            _config = config;
        }

        private async Task<SqlConnection> GetSqlConnection()
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

            return sqlConnection;
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
            try { 
                // Get owners
                var owners = await GetOwners();

                // Create new draft in database
                var draftId = await _draftStateService.CreateNewDraftAsync(week, owners, firstPickOwnerId);

                // Store draft ID in session
                HttpContext.Session.SetString("DraftId", draftId);

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting draft for week {Week}", week);

                TempData["Error"] = "Failed to start draft. Please try again.";

                return RedirectToAction("SelectWeek");
            }
    }

        /// <summary>
        /// GET: Draft/Index - Main draft interface
        /// </summary>
        public async Task<IActionResult> Index(int week)
        {
            try
            {
                // Check for existing draft ID in session
                var draftId = HttpContext.Session.GetString("DraftId");
                DraftState? draftState = null;

                if (!string.IsNullOrEmpty(draftId))
                {
                    // Try to load existing draft
                    draftState = await _draftStateService.GetDraftStateAsync(draftId);
                }

                // If no draft found, redirect to start draft page
                if (draftState == null)
                {
                    return RedirectToAction("StartDraft");
                }

                // If draft is complete, redirect to scoreboard
                if (draftState.IsComplete)
                {
                    return RedirectToAction("Index", "Scoreboard", new { week = draftState.Week });
                }

                // Load available players
                var availablePlayers = await GetAllAvailablePlayers(draftState.Week);

                HttpContext.Session.SetObjectAsJson(SessionKeyAvailablePlayers, availablePlayers);

                // Pass data to view
                ViewData["Week"] = draftState.Week.ToString();
                ViewData["DraftState"] = draftState;
                ViewData["AvailablePlayers"] = availablePlayers;
                ViewData["DraftId"] = draftState.DraftId; // For SignalR

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading draft page");
                return RedirectToAction("StartDraft");
            }
        }

        /// <summary>
        /// POST: Draft/MakePick - Process a draft pick
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> MakePick(int playerId)
        {
            try
            {
                // Get draft state from database
                var draftId = HttpContext.Session.GetString("DraftId");
                if (string.IsNullOrEmpty(draftId))
                {
                    return Json(new { success = false, message = "No active draft found" });
                }

                var draftState = await _draftStateService.GetDraftStateAsync(draftId);
                if (draftState == null)
                {
                    return Json(new { success = false, message = "Draft not found" });
                }

                // Get player information
                var availablePlayers = HttpContext.Session.GetObjectFromJson<List<EspnPlayer>>(SessionKeyAvailablePlayers);

                var player = availablePlayers.FirstOrDefault(p => p.EspnPlayerId == playerId);
                if (player == null)
                {
                    return Json(new { success = false, message = "Player not found" });
                }

                // Create drafted player
                var draftedPlayer = new DraftedPlayer
                {
                    EspnPlayerId = player.EspnPlayerId,
                    PlayerName = player.PlayerName,
                    Position = player.Position,
                    TeamAbbreviation = player.TeamAbbreviation,
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

                // Update pick number and current owner
                draftState.PickNumber++;
                if (draftState.PickNumber <= draftState.TotalPicks)
                {
                    // straight alternating picks, so just slip to the other owner
                    draftState.CurrentPickOwnerId = draftState.CurrentPickOwnerId == 1 ? 2 : 1;
                }

                // Check if draft is complete
                if (draftState.PickNumber > draftState.TotalPicks)
                {
                    draftState.IsComplete = true;
                }

                // Save to database
                await _draftStateService.SaveDraftStateAsync(draftState);
                await _draftStateService.AddDraftedPlayerAsync(draftId, draftedPlayer);

                // Save rosters to CurrentRoster table when draft is complete
                if (draftState.IsComplete)
                {
                    await SaveDraftedRosters(draftState);
                }

                // Create draft event for SignalR
                var draftEvent = new DraftEvent
                {
                    EventType = "PICK_MADE",
                    DraftId = draftId,
                    OwnerId = draftedPlayer.OwnerId,
                    PlayerName = draftedPlayer.PlayerName,
                    Position = draftedPlayer.Position,
                    PickNumber = draftedPlayer.PickNumber,
                    EspnPlayerId = draftedPlayer.EspnPlayerId
                };

                // Notify all clients via SignalR
                await _hubContext.Clients.Group(draftId).SendAsync("PickMade", draftEvent);

                // Store player name for highlighting
                HttpContext.Session.SetString("lastDraftedPlayer", draftedPlayer.PlayerName);

                if (draftState.IsComplete)
                {
                    await _hubContext.Clients.Group(draftId).SendAsync("DraftComplete");
                    return Json(new
                    {
                        success = true,
                        draftComplete = true,
                        redirectUrl = Url.Action("Index", "Scoreboard", new { week = draftState.Week })
                    });
                }

                return Json(new { success = true, draftComplete = false });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error making pick for player {PlayerId}", playerId);
                return Json(new { success = false, message = "An error occurred while making the pick" });
            }
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

            using SqlConnection sqlConnection = await GetSqlConnection();

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

            return owners;
        }

        /// <summary>
        /// Gets all available players for the week
        /// </summary>
        private async Task<List<EspnPlayer>> GetAllAvailablePlayers(int week)
        {
            var players = new List<EspnPlayer>();

            using SqlConnection sqlConnection = await GetSqlConnection();

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

            return players.OrderBy(p => p.PlayerName).ToList();
        }

        /// <summary>
        /// Saves the drafted rosters to the database
        /// </summary>
        private async Task SaveDraftedRosters(DraftState draftState)
        {
            using SqlConnection sqlConnection = await GetSqlConnection();

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
                using SqlConnection sqlConnection = await GetSqlConnection();

                // Clear rosters for the specified week
                using (SqlCommand command = new SqlCommand("DELETE FROM CurrentRoster WHERE Week = @week", sqlConnection))
                {
                    command.Parameters.Add(new SqlParameter("@week", SqlDbType.Int) { Value = week });
                    int rowsDeleted = await command.ExecuteNonQueryAsync();

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