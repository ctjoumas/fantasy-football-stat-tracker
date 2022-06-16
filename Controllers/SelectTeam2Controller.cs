namespace FantasyFootballStatTracker.Controllers
{
    using Azure.Core;
    using Azure.Identity;
    using FantasyFootballStatTracker.Configuration;
    using FantasyFootballStatTracker.Infrastructure;
    using FantasyFootballStatTracker.Models;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Options;
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Linq;

    public class SelectTeam2Controller : Controller
    {
        /// <summary>
        /// App setting for the Season.
        /// </summary>
        public const string APP_SETTINGS_SEASON_NAME = "AppConfiguration:Season";

        /// <summary>
        /// Session key for the Azure SQL Access token
        /// </summary>
        public const string SessionKeyAzureSqlAccessToken = "_Token";

        /// <summary>
        /// 
        /// </summary>
        //private readonly IOptions<AppConfiguration> _config;
        private readonly IConfiguration _config;

        //public SelectTeam2Controller(IOptions<AppConfiguration> config)
        public SelectTeam2Controller(IConfiguration config)
        {
            _config = config;
        }

        public IActionResult Index(int week, int ownerId)
        {
            // If one owner has already selected their team, the Players session variable may still be set; we want to
            // clear this out so we can update the list of players to not include the players the other owner selected
            HttpContext.Session.Clear();

            ViewData["Week"] = week;
            ViewData["OwnerId"] = ownerId;

            List<EspnPlayer> players = GetAllPlayers(week, ownerId);

            HttpContext.Session.SetObjectAsJson("Players", players);

            return View();
        }

        [HttpPost]


        /// <summary>
        /// Gets all players who have a game in a particular week and also are not already selected in the given week by the other owner.
        /// This is called by the view via ajax.
        /// </summary>
        /// <param name="week">NFL Game week</param>
        /// <param name="ownerId">ID of the owener whose team is being selected</param>
        /// <returns>The list of all players playing in the current week, excluding any players drafted by the other owner</returns>
        public List<EspnPlayer> GetAllPlayers(int week, int ownerId)
        {
            List<EspnPlayer> players = HttpContext.Session.GetObjectFromJson<List<EspnPlayer>>("Players");

            if (players == null)
            {
                players = new List<EspnPlayer>();

                // we want to look for the other owner when excluding their players; since there are only two owners (id 1 and id 2),
                // we can just check the number and set the other owner's id accordingly
                int otherOwnerId = 1;
                
                if (ownerId == 1)
                {
                    otherOwnerId = 2;
                }
               
                SqlConnection sqlConnection = GetSqlConnection();

                sqlConnection.Open();

                using (SqlCommand command = new SqlCommand("GetAvailablePlayersForWeek", sqlConnection))
                {
                    command.CommandType = System.Data.CommandType.StoredProcedure;
                    command.Parameters.Add(new SqlParameter("@week", System.Data.SqlDbType.Int) { Value = week });
                    command.Parameters.Add(new SqlParameter("@otherOwnerId", System.Data.SqlDbType.Int) { Value = otherOwnerId });

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            players.Add(
                                new EspnPlayer()
                                {
                                    EspnPlayerId = int.Parse(reader["EspnPlayerId"].ToString()),
                                    PlayerName = reader["PlayerName"].ToString(),
                                    Position = reader["Position"].ToString(),
                                    TeamAbbreviation = reader["TeamAbbreviation"].ToString()
                                }
                            );
                        }
                    }
                }

                sqlConnection.Close();

                players.OrderBy(x => x.PlayerName).ToList();
            }

            return players;
        }

        /// <summary>
        /// Checks to see if the players selected make up a valid roster.
        /// </summary>
        /// <param name="selectedPlayerIds">List of ESPN player IDs selected by the user.</param>
        /// <param name="week">NFL Game week</param>
        /// <param name="ownerId">ID of the owener whose team is being selected</param>
        /// <returns>True if the roster is valid, false otherwise.</returns>
        private bool IsValidRoster(int[] selectedPlayerIds, int week, int ownerId)
        {
            bool isValidRoster = false;

            // only check if there are 9 players, which make up a full roster
            if (selectedPlayerIds.Length == 9)
            {
                List<EspnPlayer> players = HttpContext.Session.GetObjectFromJson<List<EspnPlayer>>("Players");

                // this shouldn't happen, but in case the session variable is null, make the check and re-populate it
                if (players == null)
                {
                    players = GetAllPlayers(week, ownerId);
                    HttpContext.Session.SetObjectAsJson("Players", players);
                }

                // get only the players with the selected IDs
                players = players.Where(p => selectedPlayerIds.Contains(p.EspnPlayerId)).ToList();

                int countQb = 0;
                int countRb = 0;
                int countWr = 0;
                int countTe = 0;
                int countK = 0;
                int countDef = 0;

                foreach (EspnPlayer player in players)
                {
                    switch (player.Position)
                    {
                        case "QB":
                            countQb++;
                            break;

                        case "RB":
                            countRb++;
                            break;

                        case "WR":
                            countWr++;
                            break;

                        case "TE":
                            countTe++;
                            break;

                        case "PK":
                            countK++;
                            break;

                        case "DEF":
                            countDef++;
                            break;
                    }
                }

                if (countQb == 1 && countK == 1 && countDef == 1)
                {
                    if (((countRb == 3) && (countWr == 2) && (countTe == 1)) ||
                        ((countWr == 3) && (countRb == 2) && (countTe == 1)) ||
                        ((countTe == 2) && (countWr == 2) && (countRb == 2)))
                    {
                        isValidRoster = true;
                    }
                }
            }

            return isValidRoster;
        }

        [HttpPost]
        public IActionResult SaveRoster(int week, int ownerId, int[] selectedPlayerIds)
        {
            // check if this roster is valid before sumitting the roster
            if (!IsValidRoster(selectedPlayerIds, week, ownerId))
            {
                return Json(new { success = false });
            }
            else
            {
                SqlConnection sqlConnection = GetSqlConnection();

                sqlConnection.Open();

                List<EspnPlayer> players = HttpContext.Session.GetObjectFromJson<List<EspnPlayer>>("Players");

                // get only the players with the selected IDs
                players = players.Where(p => selectedPlayerIds.Contains(p.EspnPlayerId)).ToList();

                string position = "";

                foreach (EspnPlayer player in players)
                {
                    position = player.Position;

                    // the kicker position is stored initially (from ESPN rosters) as PK and we need to change this to K
                    if (position.Equals("PK"))
                    {
                        position = "K";
                    }

                    using (SqlCommand command = new SqlCommand("AddRosterPlayer", sqlConnection))
                    {
                        command.CommandType = System.Data.CommandType.StoredProcedure;
                        command.Parameters.Add(new SqlParameter("@ownerId", System.Data.SqlDbType.Int) { Value = ownerId });
                        command.Parameters.Add(new SqlParameter("@week", System.Data.SqlDbType.Int) { Value = week });
                        command.Parameters.Add(new SqlParameter("@playerName", System.Data.SqlDbType.NVarChar) { Value = player.PlayerName });
                        command.Parameters.Add(new SqlParameter("@position", System.Data.SqlDbType.NChar) { Value = position });
                        command.Parameters.Add(new SqlParameter("@espnPlayerId", System.Data.SqlDbType.Int) { Value = player.EspnPlayerId });
                        command.Parameters.Add(new SqlParameter("@Season", System.Data.SqlDbType.Int) { Value = _config[APP_SETTINGS_SEASON_NAME] });

                        command.ExecuteNonQuery();
                    }
                }

                sqlConnection.Close();

                // the current rosters are updated so we can now display the scoreboard
                return Json(new { success = true, redirectUrl = Url.Action("Index", "Scoreboard") });
            }
        }

        /// <summary>
        /// Gets the SQL connection for our PlayersAndSchedulesDetails database.
        /// </summary>
        /// <returns>The SqlConnection object we will use to read and write data to our database.</returns>
        private SqlConnection GetSqlConnection()
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder
            {
                DataSource = "tcp:playersandscheduledetails.database.windows.net,1433",
                InitialCatalog = "PlayersAndSchedulesDetails",
                TrustServerCertificate = false,
                Encrypt = true
            };

            var sqlConnection = new SqlConnection(connectionStringBuilder.ConnectionString);

            // check to see if the access token has already been retrieved and us it if so
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

            return sqlConnection;
        }
    }
}