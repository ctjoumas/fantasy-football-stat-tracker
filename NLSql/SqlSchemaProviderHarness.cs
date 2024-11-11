// Copyright (c) Microsoft. All rights reserved.
namespace SemanticKernel.Data.Nl2Sql.Harness;

using System.Data.Common;
using System.Data.SqlClient;
using SemanticKernel.Data.Nl2Sql.Library.Schema;

/// <summary>
/// Harness for utilizing <see cref="SqlSchemaProvider"/> to capture live database schema
/// definitions: <see cref="SchemaDefinition"/>.
/// </summary>
public sealed class SqlSchemaProviderHarness
{
    public SqlSchemaProviderHarness()
    {

    }

    //private static string _dbConnection = Environment.GetEnvironmentVariable("DatabaseConnection", EnvironmentVariableTarget.Process) ?? string.Empty;
    private static string _dbDescription = Environment.GetEnvironmentVariable("DatabaseDescription", EnvironmentVariableTarget.Process) ?? string.Empty;

    /*public async Task<string> ReverseEngineerSchemaYAMLAsync(string[] tableNames)
    {
        string dbName = GetDatabaseName();
        var yaml = await this.CaptureSchemaYAMLAsync(dbName, _dbConnection, _dbDescription, tableNames).ConfigureAwait(false);

        return yaml;    
    }*/

    public async Task<string> ReverseEngineerSchemaJSONAsync(string[] tableNames, SqlConnection sqlConnection)
    {
        string dbName = await GetDatabaseName(sqlConnection);
        System.Diagnostics.Debug.WriteLine($"Database name: {dbName}");
        var yaml = await this.CaptureSchemaJSONAsync(dbName, sqlConnection, _dbDescription, tableNames).ConfigureAwait(false);

        return yaml;
    }

    private async Task<string> CaptureSchemaYAMLAsync(string databaseKey, string? connectionString, string? description, params string[] tableNames)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var provider = new SqlSchemaProvider(connection);

        var schema = await provider.GetSchemaAsync(databaseKey, description, tableNames).ConfigureAwait(false);

        await connection.CloseAsync().ConfigureAwait(false);

        var yamlText = await schema.FormatAsync(YamlSchemaFormatter.Instance).ConfigureAwait(false);
        
        return yamlText;

        // If you want to save to a file    
        //await this.SaveSchemaAsync("yaml", databaseKey, yamlText).ConfigureAwait(false);
        //await this.SaveSchemaAsync("json", databaseKey, schema.ToJson()).ConfigureAwait(false);
    }

    private async Task<string> CaptureSchemaJSONAsync(string databaseKey, SqlConnection sqlConnection, string? description, params string[] tableNames)
    {
        var provider = new SqlSchemaProvider(sqlConnection);

        var schema = await provider.GetSchemaAsync(databaseKey, description, tableNames).ConfigureAwait(false);

        System.Diagnostics.Debug.WriteLine($"Schema: {schema}");

        await sqlConnection.CloseAsync().ConfigureAwait(false);

        var yamlText = await schema.FormatAsync(YamlSchemaFormatter.Instance).ConfigureAwait(false);

        return schema.ToJson();

        // If you want to save to a file
        //await this.SaveSchemaAsync("yaml", databaseKey, yamlText).ConfigureAwait(false);
        //await this.SaveSchemaAsync("json", databaseKey, schema.ToJson()).ConfigureAwait(false);
    }

    private async Task<string?> GetDatabaseName(SqlConnection sqlConnection)
    {
        string query = "SELECT DB_NAME() AS CurrentDatabase;";

        using (var command = new SqlCommand(query, sqlConnection))
        {
            var result = await command.ExecuteScalarAsync();
            return result?.ToString();
        }
    }
}