﻿namespace FantasyFootballStatTracker.Services
{
    using Dapper;
    using Newtonsoft.Json;
    using System.Data;
    using System.Data.SqlClient;

    public class AzureDbService
    {
        private readonly string _connectionString;

        public AzureDbService(string connectionString) {
            _connectionString = connectionString;
        }

        public async Task<string> GetDbResults(string query)
        {
            using IDbConnection connection = new SqlConnection(_connectionString);

            var dbResult = await connection.QueryAsync<dynamic>(query);
            var jsonString = JsonConvert.SerializeObject(dbResult);

            return jsonString;
        }
    }
}
