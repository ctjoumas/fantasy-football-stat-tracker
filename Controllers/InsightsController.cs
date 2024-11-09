namespace FantasyFootballStatTracker.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.SemanticKernel.Connectors.OpenAI;
    using Microsoft.SemanticKernel;
    using SemanticKernel.Data.Nl2Sql.Harness;
    using System.Data.SqlClient;
    using Microsoft.SemanticKernel.ChatCompletion;
    using Azure.Core;
    using Azure.Identity;

    public class InsightsController : Controller
    {
        private readonly ILogger<InsightsController> _logger;

        private readonly Kernel _kernel;

        private readonly IChatCompletionService _chat;

        /// <summary>
        /// Session key for the Azure SQL Access token
        /// </summary>
        public const string SessionKeyAzureSqlAccessToken = "_Token";

        private static async Task<string> GetAzureSqlAccessToken()
        {
            // See https://docs.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/services-support-managed-identities#azure-sql
            var tokenRequestContext = new TokenRequestContext(new[] { "https://database.windows.net//.default" });
            var tokenRequestResult = await new DefaultAzureCredential().GetTokenAsync(tokenRequestContext);

            return tokenRequestResult.Token;
        }

        public InsightsController(ILogger<InsightsController> logger, Kernel kernel, IChatCompletionService chat)
        {
            _logger = logger;
            _kernel = kernel;
            _chat = chat;
        }

        public async Task<IActionResult> Index()
        {
            var sqlHarness = new SqlSchemaProviderHarness();

            string[] tableNames = "dbo.Owners,dbo.CurrentRoster,dbo.Players,dbo.TeamsSchedule".Split(",");

            var connectionStringBuilder = new SqlConnectionStringBuilder
            {
                DataSource = "tcp:playersandschedulesdetails.database.windows.net,1433",
                InitialCatalog = "PlayersAndSchedulesDetails",
                TrustServerCertificate = false,
                Encrypt = true
            };

            

            string azureSqlToken = SessionExtensions.GetString(HttpContext.Session, SessionKeyAzureSqlAccessToken);

            // if we haven't retrieved the token yet, retrieve it and set it in the session (at this point though, we should have the token)
            if (azureSqlToken == null)
            {
                var tokenRequestContext = new TokenRequestContext(new[] { "https://database.windows.net//.default" });
                var tokenRequestResult = new DefaultAzureCredential().GetToken(tokenRequestContext);

                azureSqlToken = tokenRequestResult.Token;

                SessionExtensions.SetString(HttpContext.Session, SessionKeyAzureSqlAccessToken, azureSqlToken);
            }

            SqlConnection sqlConnection = new SqlConnection(connectionStringBuilder.ConnectionString);
            sqlConnection.AccessToken = azureSqlToken;

            await sqlConnection.OpenAsync();

            var jsonSchema = await sqlHarness.ReverseEngineerSchemaJSONAsync(tableNames, sqlConnection);

            var systemPrompt = $@"You are responsible for generating and executing a SQL query.
                                Only target the tables described in the given database schema. The database stores
                                information about two owners who are playing a weekly fantasy football game against each other.
                                Your job is to analyze all of the weekly matchups which have taken place to date, using the Season year
                                that is present in the tables. You will provide an overview of which owner is doing better, how they
                                are faring in selecting players each week, adding humorous context to the analysis.

                                Perform each of the following steps:
                                1. Generate a query that is always entirely based on the targeted database schema.
                                2. Execute the query using the available plugin.
                                3. Determine if the most recent week played is completed or not. If this week is not yet completed, you
                                   will specify that the week is in progress and you will provide commentary on how the week is going
                                   and what each team has left in order to secure a victory.
                                4. Summarize the results to the user.

                                This data will be presented on a webpage, so please format the response in HTML without using
                                any code block markers or markdown formatting.

                                The database schema is described according to the following json schema:
                                {jsonSchema}";

            ChatMessageContent result = await _chat.GetChatMessageContentAsync
                (
                    systemPrompt,
                    executionSettings: new OpenAIPromptExecutionSettings { Temperature = 0.8, TopP = 0.0, ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions },
                    kernel: _kernel
                );

            ViewBag.SeasonInsights = result.Content;

            return View();
        }
    }
}
