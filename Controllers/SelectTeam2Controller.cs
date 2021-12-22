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
        public IActionResult Index(int week, int ownerId)
        {
            ViewData["Week"] = week;
            ViewData["OwnerId"] = ownerId;

            List<EspnPlayer> players = GetPlayers("", new string[0]);

            HttpContext.Session.SetObjectAsJson("Players", players);

            return View();
        }

        /// <summary>
        /// Method to display the filtered data.
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        [HttpPost]
        public List<EspnPlayer> SearchPlayers(string playerName, string[] selectedPlayerIds)
        {
            List<EspnPlayer> players = new List<EspnPlayer>();

            if (playerName != null)
            {
                players = GetPlayers(playerName, selectedPlayerIds);
            }

            return players;
        }

        /// <summary>
        /// Gets all players from the Players table who are playing (not on bye) in the current week.
        /// </summary>
        /// <returns>List of all players playing in the given week.</returns>
        private List<EspnPlayer> GetPlayers(string nameFilter, string[] selectedPlayerIds)
        {
            List<EspnPlayer> filteredPlayerList = new List<EspnPlayer>();

            List<EspnPlayer> players = HttpContext.Session.GetObjectFromJson<List<EspnPlayer>>("Players");

            if (players == null)
            {
                int week = (int)ViewData["Week"];

                string sql = "select p.EspnPlayerId, p.PlayerName, p.Position from Players p " +
                             "join TeamsSchedule ts on p.TeamId = ts.TeamId " +
                             "where ts.Week = " + week.ToString() + " and p.PlayerName like '%" + nameFilter + "%'";

                SqlConnection sqlConnection = GetSqlConnection();

                sqlConnection.Open();

                using (SqlCommand command = new SqlCommand(sql, sqlConnection))
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            filteredPlayerList.Add(
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
            }
            else
            {
                foreach (EspnPlayer player in players)
                {
                    // add the player if they match the name filter and they are not already selected
                    if ( player.PlayerName.ToLower().Contains(nameFilter) &&
                         !selectedPlayerIds.Contains(player.EspnPlayerId.ToString()))
                    {
                        filteredPlayerList.Add(player);
                    }
                }
            }

            filteredPlayerList.OrderBy(x => x.PlayerName).ToList();

            return filteredPlayerList;
        }

        [HttpPost]
        public IActionResult SaveRoster(int week, int ownerId, int[] selectedPlayerIds)
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
            //return RedirectToAction("Index", "Scoreboard");
            return Json(new { redirectUrl = Url.Action("Index", "Scoreboard") });
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

            var tokenRequestContext = new TokenRequestContext(new[] { "https://database.windows.net//.default" });
            var tokenRequestResult = new DefaultAzureCredential().GetToken(tokenRequestContext);

            // THIS MAY TAKE A LONG TIME (NEED TO TEST FURTHER) - CAN THIS BE STORED SOMEWHERE SO ALL THREADS CAN USE IT?
            sqlConnection.AccessToken = tokenRequestResult.Token;

            return sqlConnection;
        }
    }    
}