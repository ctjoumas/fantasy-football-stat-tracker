namespace FantasyFootballStatTracker.Controllers
{
    using Azure.Core;
    using Azure.Identity;
    using FantasyFootballStatTracker.Infrastructure;
    using FantasyFootballStatTracker.Models;
    using Microsoft.AspNetCore.Mvc;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Threading.Tasks;

    public class StandingsController : Controller
    {
        /// <summary>
        /// Session key for the Azure SQL Access token
        /// </summary>
        public const string SessionKeyAzureSqlAccessToken = "_Token";

        /// <summary>
        /// The owner stats object which will be updated from the database and dispalyed on the page
        /// </summary>
        private List<OwnerStats> ownerStats = new List<OwnerStats>(2);

        private static async Task<string> GetAzureSqlAccessToken()
        {
            // See https://docs.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/services-support-managed-identities#azure-sql
            var tokenRequestContext = new TokenRequestContext(new[] { "https://database.windows.net//.default" });
            var tokenRequestResult = await new DefaultAzureCredential().GetTokenAsync(tokenRequestContext);

            return tokenRequestResult.Token;
        }

        public async Task<IActionResult> Index()
        {
            // add new OwnerStats for each owner to the ownerStats list
            ownerStats.Add(new OwnerStats());
            ownerStats.Add(new OwnerStats());

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

            // populate the owner logos
            PopulateOwnerLogos(sqlConnection);

            // get the distinct weeks that have been played
            ArrayList weeksPlayed = GetWeeksCompleted(sqlConnection);

            UpdateStandingsForAllWeeksPlayed(sqlConnection, weeksPlayed);

            // sort the display so it's by wins or, if tied, total points
            ownerStats = ownerStats.OrderByDescending(x => x.Wins)
                      .ThenByDescending(x => x.TotalPoints)
                      .ToList();

            sqlConnection.Close();

            return View(ownerStats);
        }

        /// <summary>
        /// Populates the owner logos for display in the standings page.
        /// </summary>
        /// <param name="sqlConnection">Connection to the SQL database.</param>
        private void PopulateOwnerLogos(SqlConnection sqlConnection)
        {
            List<byte[]> OwnerLogos = HttpContext.Session.GetObjectFromJson<List<byte[]>>(SessionExtensions.SessionKeyLogos);

            // if we haven't retrieved the owner logos yet, retrieve it and store it in the session
            if (OwnerLogos == null)
            {
                using (SqlCommand command = new SqlCommand("GetOwnerLogos", sqlConnection))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        int i = 0;
                        while (reader.Read())
                        {
                            ownerStats[i].OwnerLogo = (byte[])reader.GetValue(reader.GetOrdinal("Logo"));
                            i++;
                        }
                    }
                }
            }
            else
            {
                for (int i=0; i<ownerStats.Count; i++)
                {
                    ownerStats[i].OwnerLogo = OwnerLogos[i];
                }
            }
        }

        /// <summary>
        /// Returns a list of all weeks which have been completed. If a week is still in progress, this week is not returned so
        /// that it is not counted in the standings.
        /// </summary>
        /// <param name="sqlConnection">Connection to the datbase.</param>
        /// <returns></returns>
        private ArrayList GetWeeksCompleted(SqlConnection sqlConnection)
        {
            ArrayList weeksPlayed = new ArrayList();

            using (SqlCommand command = new SqlCommand("GetWeeksCompleted", sqlConnection))
            {
                command.CommandType = CommandType.StoredProcedure;

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        weeksPlayed.Add((int)reader.GetValue(reader.GetOrdinal("week")));
                    }
                }
            }

            return weeksPlayed;
        }

        /// <summary>
        /// Updates the standings for a given week, including total points, wins, losses, and win/loss streak for each owner.
        /// </summary>
        /// <param name="sqlConnection">Connection to the database</param>
        /// <param name="weeks">All weeks we are updating the standings for</param>
        private void UpdateStandingsForAllWeeksPlayed(SqlConnection sqlConnection, ArrayList weeks)
        {
            OwnerStats ownerOneStats = ownerStats[0];
            OwnerStats ownerTwoStats = ownerStats[1];

            // create a datatable to store the week numbers in
            DataTable dataTable = new DataTable();
            dataTable.Columns.Add(new DataColumn("Week", typeof(int)));

            // populate datatable from the list
            foreach (int week in weeks)
            {
                dataTable.Rows.Add(week);
            }

            using (SqlCommand command = new SqlCommand("GetTotalPointsForEachWeek", sqlConnection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add((new SqlParameter("@Weeks", SqlDbType.Structured) { Value = dataTable }));

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    // go through all players on both rosters and tallyup the score
                    while (reader.Read())
                    {
                        int week = (int)reader.GetValue(reader.GetOrdinal("Week"));
                        int ownerId = (int)reader.GetValue(reader.GetOrdinal("OwnerID"));
                        double score = (double)reader.GetValue(reader.GetOrdinal("FinalPoints"));

                        // add the score to the appropriate owner's stats object
                        if (ownerId == 1)
                        {
                            ownerOneStats.WeeklyScores.Add(week, score);
                            ownerOneStats.TotalPoints += score;
                        }
                        else
                        {
                            ownerTwoStats.WeeklyScores.Add(week, score);
                            ownerTwoStats.TotalPoints += score;
                        }
                    }
                }
            }

            double ownerOneScore, ownerTwoScore;

            // Now that we have all scores for each week for both owners, let's go tally up the stats.
            // We're starting off with week 1 and we'll pull each key based on the week, so we'll start
            // i at 1
            for (int i = 1; i<=ownerStats[0].WeeklyScores.Count; i++)
            {
                ownerOneScore = (double) ownerOneStats.WeeklyScores[i];
                ownerTwoScore = (double) ownerTwoStats.WeeklyScores[i];

                bool ownerOneWeekWinner = ownerOneScore > ownerTwoScore;

                // update wins/losses for each owner
                if (ownerOneWeekWinner)
                {
                    ownerOneStats.Wins += 1;
                    ownerTwoStats.Losses += 1;

                    if (ownerOneStats.Streak.Equals(String.Empty))
                    {
                        ownerOneStats.Streak = "W1";
                        ownerTwoStats.Streak = "L1";
                    }
                    else
                    {
                        // pull out the streak and update accordingly
                        if (ownerOneStats.Streak.StartsWith('W'))
                        {
                            // owner one has a win streak, so let's pull out the last part of this and increment it
                            int streak = int.Parse(ownerOneStats.Streak.Substring(1));
                            streak++;

                            ownerOneStats.Streak = "W" + streak.ToString();

                            // owner two is on a losing streak, so increment their losing streak
                            streak = int.Parse(ownerTwoStats.Streak.Substring(1));
                            streak++;

                            ownerTwoStats.Streak = "L" + streak.ToString();
                        }
                        else
                        {
                            // owner one has a losing streak and just won, so update to W1 (and owner two to L1)
                            ownerOneStats.Streak = "W1";
                            ownerTwoStats.Streak = "L1";
                        }
                    }
                }
                else
                {
                    ownerOneStats.Losses += 1;
                    ownerTwoStats.Wins += 1;

                    if (ownerOneStats.Streak.Equals(String.Empty))
                    {
                        ownerOneStats.Streak = "L1";
                        ownerTwoStats.Streak = "W1";
                    }
                    else
                    {
                        // pull out the streak and update accordingly
                        if (ownerTwoStats.Streak.StartsWith('W'))
                        {
                            // owner two has a win streak, so let's pull out the last part of this and increment it
                            int streak = int.Parse(ownerTwoStats.Streak.Substring(1));
                            streak++;

                            ownerTwoStats.Streak = "W" + streak.ToString();

                            // owner one is on a losing streak, so increment their losing streak
                            streak = int.Parse(ownerOneStats.Streak.Substring(1));
                            streak++;

                            ownerOneStats.Streak = "L" + streak.ToString();
                        }
                        else
                        {
                            // owner two has a losing streak and just won, so update to W1 (and owner one to L1)
                            ownerTwoStats.Streak = "W1";
                            ownerOneStats.Streak = "L1";
                        }
                    }
                }
            }

            // add back the updated owner stats objects
            ownerStats[0] = ownerOneStats;
            ownerStats[1] = ownerTwoStats;
        }
    }
}