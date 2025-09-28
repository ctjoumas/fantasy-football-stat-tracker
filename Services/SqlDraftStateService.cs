using Azure.Core;
using Azure.Identity;
using FantasyFootballStatTracker.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using FantasyFootballStatTracker.Infrastructure;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace FantasyFootballStatTracker.Services
{
    public class SqlDraftStateService : IDraftStateService
    {
        private readonly ILogger<SqlDraftStateService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private const string SessionKeyAzureSqlAccessToken = "_Token";

        public SqlDraftStateService(ILogger<SqlDraftStateService> logger, IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
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

            var session = _httpContextAccessor.HttpContext?.Session;
            string? azureSqlToken = null;

            if (session != null)
            {
                azureSqlToken = Microsoft.AspNetCore.Http.SessionExtensions.GetString(session, SessionKeyAzureSqlAccessToken);
            }

            // if we haven't retrieved the token yet, retrieve it and set it in the session (at this point though, we should have the token)
            if (azureSqlToken == null)
            {
                azureSqlToken = await GetAzureSqlAccessToken();

                if (session != null)
                {
                    Microsoft.AspNetCore.Http.SessionExtensions.SetString(session, SessionKeyAzureSqlAccessToken, azureSqlToken);
                }
            }

            SqlConnection sqlConnection = new SqlConnection(connectionStringBuilder.ConnectionString);
            sqlConnection.AccessToken = azureSqlToken;

            return sqlConnection;
        }

        private static async Task<string> GetAzureSqlAccessToken()
        {
            // See https://docs.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/services-support-managed-identities#azure-sql
            var tokenRequestContext = new TokenRequestContext(new[] { "https://database.windows.net//.default" });
            var tokenRequestResult = await new DefaultAzureCredential().GetTokenAsync(tokenRequestContext);

            return tokenRequestResult.Token;
        }

        public async Task<DraftState?> GetDraftStateAsync(string draftId)
        {
            try
            {
                SqlConnection sqlConnection = await GetSqlConnection();

                using var command = new SqlCommand("GetDraftState", sqlConnection);
                command.CommandType = System.Data.CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@DraftId", draftId);

                using var reader = await command.ExecuteReaderAsync();
                
                // Read draft session info
                if (!await reader.ReadAsync())
                    return null;

                var draftState = new DraftState
                {
                    DraftId = reader["DraftId"].ToString() ?? string.Empty,
                    Week = int.Parse(reader["Week"].ToString()),
                    CurrentPickOwnerId = int.Parse(reader["CurrentPickOwnerId"].ToString()),
                    FirstPickOwnerId = int.Parse(reader["FirstPickOwnerId"].ToString()),
                    PickNumber = int.Parse(reader["PickNumber"].ToString()),
                    TotalPicks = int.Parse(reader["TotalPicks"].ToString()),
                    IsComplete = bool.Parse(reader["IsComplete"].ToString()),
                    CreatedAt = DateTime.Parse(reader["CreatedAt"].ToString()),
                    LastUpdated = DateTime.Parse(reader["LastUpdated"].ToString()),
                    Owners = new List<Owner>(),
                    Owner1Roster = new List<DraftedPlayer>(),
                    Owner2Roster = new List<DraftedPlayer>()
                };

                // Read owners
                await reader.NextResultAsync();
                while (await reader.ReadAsync())
                {
                    draftState.Owners.Add(new Owner
                    {
                        OwnerId = int.Parse(reader["OwnerId"].ToString()),
                        OwnerName = reader["OwnerName"].ToString()
                    });
                }

                // Read drafted players
                await reader.NextResultAsync();
                while (await reader.ReadAsync())
                {
                    var player = new DraftedPlayer
                    {
                        EspnPlayerId = int.Parse(reader["EspnPlayerId"].ToString()),
                        PlayerName = reader["PlayerName"].ToString(),
                        Position = reader["Position"].ToString(),
                        TeamAbbreviation = reader["TeamAbbreviation"].ToString(),
                        PickNumber = int.Parse(reader["PickNumber"].ToString()),
                        OwnerId = int.Parse(reader["OwnerId"].ToString())
                    };

                    if (player.OwnerId == 1)
                        draftState.Owner1Roster.Add(player);
                    else
                        draftState.Owner2Roster.Add(player);
                }

                await sqlConnection.CloseAsync();

                return draftState;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting draft state for {DraftId}", draftId);
                return null;
            }
        }

        public async Task SaveDraftStateAsync(DraftState draftState)
        {
            try
            {
                SqlConnection sqlConnection = await GetSqlConnection();

                using var command = new SqlCommand("SaveDraftState", sqlConnection);
                command.CommandType = System.Data.CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@DraftId", draftState.DraftId);
                command.Parameters.AddWithValue("@Week", draftState.Week);
                command.Parameters.AddWithValue("@CurrentPickOwnerId", draftState.CurrentPickOwnerId);
                command.Parameters.AddWithValue("@FirstPickOwnerId", draftState.FirstPickOwnerId);
                command.Parameters.AddWithValue("@PickNumber", draftState.PickNumber);
                command.Parameters.AddWithValue("@TotalPicks", draftState.TotalPicks);
                command.Parameters.AddWithValue("@IsComplete", draftState.IsComplete);

                await command.ExecuteNonQueryAsync();
                await sqlConnection.CloseAsync();
                
                _logger.LogInformation("Draft state saved for {DraftId}", draftState.DraftId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving draft state for {DraftId}", draftState.DraftId);
                throw;
            }
        }

        public async Task<bool> DraftExistsAsync(string draftId)
        {
            try
            {
                SqlConnection sqlConnection = await GetSqlConnection();

                using var command = new SqlCommand("DraftExists", sqlConnection);
                command.CommandType = System.Data.CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@DraftId", draftId);

                var result = await command.ExecuteScalarAsync();
                await sqlConnection.CloseAsync();
                
                return Convert.ToBoolean(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if draft exists for {DraftId}", draftId);
                return false;
            }
        }

        public async Task DeleteDraftAsync(string draftId)
        {
            try
            {
                SqlConnection sqlConnection = await GetSqlConnection();

                using var command = new SqlCommand("DELETE FROM DraftSessions WHERE DraftId = @DraftId", sqlConnection);
                command.Parameters.AddWithValue("@DraftId", draftId);

                await command.ExecuteNonQueryAsync();
                await sqlConnection.CloseAsync();
                
                _logger.LogInformation("Draft deleted: {DraftId}", draftId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting draft {DraftId}", draftId);
                throw;
            }
        }        

        public async Task<string> CreateNewDraftAsync(int week, List<Owner> owners, int firstPickOwnerId)
        {
            var draftId = $"draft_week_{week}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
            
            try
            {
                SqlConnection sqlConnection = await GetSqlConnection();

                using var command = new SqlCommand("CreateNewDraft", sqlConnection);
                command.CommandType = System.Data.CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@DraftId", draftId);
                command.Parameters.AddWithValue("@Week", week);
                command.Parameters.AddWithValue("@FirstPickOwnerId", firstPickOwnerId);

                await command.ExecuteNonQueryAsync();
                await sqlConnection.CloseAsync();
                
                return draftId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating new draft for week {Week}", week);
                throw;
            }
        }

        public async Task AddDraftedPlayerAsync(string draftId, DraftedPlayer player)
        {
            try
            {
                SqlConnection sqlConnection = await GetSqlConnection();

                using var command = new SqlCommand("AddDraftedPlayer", sqlConnection);
                command.CommandType = System.Data.CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@DraftId", draftId);
                command.Parameters.AddWithValue("@EspnPlayerId", player.EspnPlayerId);
                command.Parameters.AddWithValue("@PlayerName", player.PlayerName);
                command.Parameters.AddWithValue("@Position", player.Position);
                command.Parameters.AddWithValue("@TeamAbbreviation", player.TeamAbbreviation);
                command.Parameters.AddWithValue("@PickNumber", player.PickNumber);
                command.Parameters.AddWithValue("@OwnerId", player.OwnerId);

                await command.ExecuteNonQueryAsync();
                await sqlConnection.CloseAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding drafted player to {DraftId}", draftId);
                throw;
            }
        }
    }
}