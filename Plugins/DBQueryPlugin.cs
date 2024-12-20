﻿namespace FantasyFootballStatTracker.Plugins
{
    using Microsoft.SemanticKernel;
    using FantasyFootballStatTracker.Services;
    using System.ComponentModel;
    using Azure.Core;
    using Azure.Identity;
    using Microsoft.AspNetCore.Http;
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    
    public class DBQueryPlugin
    {
       //private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// Session key for the Azure SQL Access token
        /// </summary>
        public const string SessionKeyAzureSqlAccessToken = "_Token";

        /*public DBQueryPlugin(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }*/

        private static async Task<string> GetAzureSqlAccessToken()
        {
            // See https://docs.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/services-support-managed-identities#azure-sql
            var tokenRequestContext = new TokenRequestContext(new[] { "https://database.windows.net//.default" });
            var tokenRequestResult = await new DefaultAzureCredential().GetTokenAsync(tokenRequestContext);

            return tokenRequestResult.Token;
        }

        [KernelFunction]
        [Description("Executes a SQL query to provide details about the head to head matchups that have taken place through each week of the season.")]
        public async Task<string> GetSeasonMatchupDetails(string query)
        {
            Console.WriteLine($"SQL Query: {query}");

            var connectionStringBuilder = new SqlConnectionStringBuilder
            {
                DataSource = "tcp:playersandschedulesdetails.database.windows.net,1433",
                InitialCatalog = "PlayersAndSchedulesDetails",
                TrustServerCertificate = false,
                Encrypt = true
            };

            /*var session = _httpContextAccessor.HttpContext.Session;
            string azureSqlToken = session.GetString(SessionKeyAzureSqlAccessToken);

            // if we haven't retrieved the token yet, retrieve it and set it in the session (at this point though, we should have the token)
            if (azureSqlToken == null)
            {
                azureSqlToken = await GetAzureSqlAccessToken();

                session.SetString(SessionKeyAzureSqlAccessToken, azureSqlToken);
            }*/

            string azureSqlToken = await GetAzureSqlAccessToken();

            SqlConnection sqlConnection = new SqlConnection(connectionStringBuilder.ConnectionString);
            sqlConnection.AccessToken = azureSqlToken;

            await sqlConnection.OpenAsync();

            //var azureDbService = new AzureDbService(connectionStringBuilder.ConnectionString);
            var azureDbService = new AzureDbService();
            var dbResults = await azureDbService.GetDbResults(sqlConnection, query);
            System.Diagnostics.Debug.WriteLine($"DB Results from plugin: {dbResults}");
            await sqlConnection.CloseAsync();

            string results = dbResults;

            Console.WriteLine($"DB Results:{results}");
            return results;
        }
    }
}