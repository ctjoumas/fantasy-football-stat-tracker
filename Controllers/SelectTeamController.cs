namespace FantasyFootballStatTracker.Controllers
{
    using Azure.Core;
    using Azure.Identity;
    using FantasyFootballStatTracker.Models;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Linq;

    public class SelectTeamController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            SqlConnection sqlConnection = GetSqlConnection();

            sqlConnection.Open();

            PlayerModel player = new PlayerModel();
            //player.PlayerSelectedListOwner1 = new List<EspnPlayer>();
            //player.PlayerSelectedListOwner2 = new List<EspnPlayer>();
            //player.PlayerUnSelectedList = GetAllPlayers(sqlConnection);
            player.PlayerSelectedListOwner1 = new List<SelectListItem>();
            player.PlayerSelectedListOwner2 = new List<SelectListItem>();
            player.PlayerUnselectedList = GetAllPlayers(sqlConnection);


            sqlConnection.Close();

            return View(player);
        }

        [HttpPost]
        public IActionResult Index(PlayerModel _playerModel)
        {
            SqlConnection sqlConnection = GetSqlConnection();

            sqlConnection.Open();

            //List<EspnPlayer> allPlayers = GetAllPlayers(sqlConnection);
            List<SelectListItem> allPlayers = GetAllPlayers(sqlConnection);

            //List<EspnPlayer> playerListOwner1 = allPlayers.Where(m => _playerModel.SelectedEspnPlayerNamesOwner1.Contains(m.PlayerName)).ToList();
            //List<EspnPlayer> playerListOwner2 = allPlayers.Where(m => _playerModel.SelectedEspnPlayerNamesOwner2.Contains(m.PlayerName)).ToList();
            List<SelectListItem> playerListOwner1 = allPlayers.Where(m => _playerModel.SelectedEspnPlayerNamesOwner1.Contains(m.Value)).ToList();
            List<SelectListItem> playerListOwner2 = allPlayers.Where(m => _playerModel.SelectedEspnPlayerNamesOwner2.Contains(m.Value)).ToList();

            UpdateCurrentRosterWithSelectedPlayers(sqlConnection, playerListOwner1, playerListOwner2);

            sqlConnection.Close();

            // the current rosters are updated so we can now display the scoreboard
            return RedirectToAction("Index", "Scoreboard");
        }

        //private void UpdateCurrentRosterWithSelectedPlayers(SqlConnection sqlConnection, List<EspnPlayer> playerListOwner1, List<EspnPlayer> playerListOwner2)
        private void UpdateCurrentRosterWithSelectedPlayers(SqlConnection sqlConnection, List<SelectListItem> playerListOwner1, List<SelectListItem> playerListOwner2)
        {
            int week = GetWeekToSetRostersFor(sqlConnection);

            int dashIndex;
            string position;

            // TODO: Get the Owenr IDs into this page rather than hardcoding as it is set below
            //foreach (EspnPlayer player in playerListOwner1)
            foreach (SelectListItem player in playerListOwner1)
            {
                // extract the position from the player text (it is in the format "Player Name - PK")
                dashIndex = player.Text.IndexOf("-");
                position = player.Text.Substring(dashIndex + 2);

                // the kicker position is stored initially (from ESPN rosters) as PK and we need to change this to K
                if (position.Equals("PK"))
                {
                    position = "K";
                }

                string sql = "insert into CurrentRoster (OwnerID, Week, PlayerName, Position, GameEnded, FinalPoints, FinalPointsString) " +
                             "values ('1', '" + week + "', '" + player.Value + "', '" + position + "', 0, 0, '')";

                using (SqlCommand command = new SqlCommand(sql, sqlConnection))
                {
                    command.ExecuteNonQuery();
                }
            }

            //foreach (EspnPlayer player in playerListOwner2)
            foreach (SelectListItem player in playerListOwner2)
            {
                // extract the position from the player text (it is in the format "Player Name - PK")
                dashIndex = player.Text.IndexOf("-");
                position = player.Text.Substring(dashIndex + 2);

                // the kicker position is stored initially (from ESPN rosters) as PK and we need to change this to K
                if (position.Equals("PK"))
                {
                    position = "K";
                }

                string sql = "insert into CurrentRoster (OwnerID, Week, PlayerName, Position, GameEnded, FinalPoints, FinalPointsString) " +
                             "values ('2', '" + week + "', '" + player.Value + "', '" + position + "', 0, 0, '')";

                using (SqlCommand command = new SqlCommand(sql, sqlConnection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Gets all players from the Players table who are playing (not on bye) in the current week.
        /// </summary>
        /// <returns>List of all players playing in the given week.</returns>
        //private List<EspnPlayer> GetAllPlayers(SqlConnection sqlConnection)
        private List<SelectListItem> GetAllPlayers(SqlConnection sqlConnection)
        {
            //List<EspnPlayer> players = new List<EspnPlayer>();
            List<SelectListItem> playerList = new List<SelectListItem>();

            int week = GetWeekToSetRostersFor(sqlConnection);

            string sql = "select p.EspnPlayerId, p.PlayerName, p.Position from Players p " +
                         "join TeamsSchedule ts on p.TeamId = ts.TeamId " +
                         "where ts.Week = " + week.ToString();

            string playerName;
            string position;

            using (SqlCommand command = new SqlCommand(sql, sqlConnection))
            {
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        /*players.Add(new EspnPlayer
                        {
                            EspnPlayerId = int.Parse(reader["EspnPlayerId"].ToString()),
                            PlayerName = reader["PlayerName"].ToString(),
                            Position= reader["Position"].ToString()
                        });*/
                        playerName = reader["PlayerName"].ToString();
                        position = reader["Position"].ToString();
                        playerList.Add(new SelectListItem() { Value = playerName, Text = playerName + " - " + position });
                    }
                }
            }

            //players = players.OrderBy(x => x.PlayerName).ToList();
            playerList = playerList.OrderBy(x => x.Value).ToList();

            //return players;
            return playerList;
        }

        /// <summary>
        /// Gets the last week selected in the CurrentRoster table and returns that value plus one, since this
        /// is the week we are selecting a roster for.
        /// </summary>
        /// <returns>The week we are selecting a roster for</returns>
        private int GetWeekToSetRostersFor(SqlConnection sqlConnection)
        {
            int week;

            string sql = "select max(Week) from CurrentRoster";

            using (SqlCommand command = new SqlCommand(sql, sqlConnection))
            {
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    reader.Read();

                    // there is only one value returned, so we just need to grab the first value
                    week = int.Parse(reader.GetValue(0).ToString()) + 1;
                }
            }

            return week;
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