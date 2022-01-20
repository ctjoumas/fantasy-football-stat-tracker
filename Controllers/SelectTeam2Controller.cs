namespace FantasyFootballStatTracker.Controllers
{
    using Azure.Core;
    using Azure.Identity;
    using FantasyFootballStatTracker.Models;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Linq;

    public static class SessionExtensions
    {
        public static void SetObjectAsJson(this ISession session, string key, object value)
        {
            session.SetString(key, JsonConvert.SerializeObject(value));
        }

        public static T GetObjectFromJson<T>(this ISession session, string key)
        {
            string value = session.GetString(key);
            return value == null ? default(T) : JsonConvert.DeserializeObject<T>(value);
        }
    }

    public class SelectTeam2Controller : Controller
    {
        /// <summary>
        /// Session key for the Azure SQL Access token
        /// </summary>
        public const string SessionKeyAzureSqlAccessToken = "_Token";

        public IActionResult Index(int week, int ownerId)
        {
            // If one owner has already selected their team, the Players session variable may still be set; we want to
            // clear this out so we can update the list of players to not include the players the other owner selected
            HttpContext.Session.Clear();

            ViewData["Week"] = week;
            ViewData["OwnerId"] = ownerId;

            List <EspnPlayer> players = GetAllPlayers();

            HttpContext.Session.SetObjectAsJson("Players", players);

            return View();
        }

        [HttpPost]
        public List<EspnPlayer> GetAllPlayers()
        {
            List<EspnPlayer> players = HttpContext.Session.GetObjectFromJson<List<EspnPlayer>>("Players");

            if (players == null)
            {
                players = new List<EspnPlayer>();

                int week = (int)ViewData["Week"];
                int ownerId = (int)ViewData["OwnerId"];

                // we want to look for the other owner when excluding their players; since there are only two owners (id 1 and id 2),
                // we can just check the number and set the other owner's id accordingly
                string otherOwnerId = "1";
                
                if (ownerId == 1)
                {
                    otherOwnerId = "2";
                }

                string sql = "select p.EspnPlayerId, p.PlayerName, p.Position from Players p " +
                             "join TeamsSchedule ts on p.TeamId = ts.TeamId " +
                             "where ts.Week = " + week.ToString() + " and p.EspnPlayerId not in " +
                             "  (select EspnPlayerId from CurrentRoster where OwnerID = " + otherOwnerId + " and Week = " + week.ToString() + ")";

                SqlConnection sqlConnection = GetSqlConnection();

                sqlConnection.Open();

                using (SqlCommand command = new SqlCommand(sql, sqlConnection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            players.Add(
                                new EspnPlayer()
                                {
                                    EspnPlayerId = int.Parse(reader["EspnPlayerId"].ToString()),
                                    PlayerName = reader["PlayerName"].ToString(),
                                    Position = reader["Position"].ToString()
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
        /// <returns>True if the roster is valid, false otherwise.</returns>
        private bool IsValidRoster(int[] selectedPlayerIds)
        {
            bool isValidRoster = false;

            // only check if there are 9 players, which make up a full roster
            if (selectedPlayerIds.Length == 9)
            {
                List<EspnPlayer> players = HttpContext.Session.GetObjectFromJson<List<EspnPlayer>>("Players");

                // this shouldn't happen, but in case the session variable is null, make the check and re-populate it
                if (players == null)
                {
                    players = GetAllPlayers();
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
            if (!IsValidRoster(selectedPlayerIds))
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

                    string sql = "insert into CurrentRoster (OwnerID, Week, PlayerName, Position, GameEnded, FinalPoints, FinalPointsString, EspnPlayerId) " +
                                 "values ('" + ownerId + "', '" + week + "', '" + player.PlayerName + "', '" + position + "', 0, 0, '', '" + player.EspnPlayerId + "')";

                    using (SqlCommand command = new SqlCommand(sql, sqlConnection))
                    {
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