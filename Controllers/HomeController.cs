namespace FantasyFootballStatTracker.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;
    using System.Diagnostics;
    using System.Net.Http;
    using FantasyFootballStatTracker.Models;

    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return RedirectToAction("Index", "Scoreboard");
        }


        /// <summary>
        /// This will request the Access Token from yahoo making an HTTP POST request.
        /// This method should be moved somewhere else; just testing here.
        /// </summary>
        /*private async Task GetAccessToken(string code)
        {
            string consumerKey = _config.Value.ClientId;
            string consumerSecret = _config.Value.ClientSecret;

            string returnUrl = "https://localhost:44376/Home/Login";
            //string returnUrl = "https://tjoumasfantasyfootball.azurewebsites.net/Home/Login";

            //Exchange authorization code for Access Token by sending Post Request
            Uri address = new Uri("https://api.login.yahoo.com/oauth2/get_token");

            //HttpClient client = new HttpClient();

            // Create the web request
            HttpRequestMessage request = new HttpRequestMessage();
            request.RequestUri = address;
            request.Method = HttpMethod.Post;

            var body = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("client_id", consumerKey),
                new KeyValuePair<string, string>("client_secret", consumerSecret),
                new KeyValuePair<string, string>("grant_type", "authorization_code")
            };

            body.Add(new KeyValuePair<string, string>("code", code));
            body.Add(new KeyValuePair<string, string>("redirect_uri", returnUrl));

            var response = client.PostAsync(request.RequestUri, new FormUrlEncodedContent(body)).Result;

            string testResponse = await response.Content.ReadAsStringAsync();

            //if (String.IsNullOrEmpty(testResponse) || String.IsNullOrEmpty(key))
            //    return null;
            try
            {
                // response can be sent in JSON format. Token expires in an hour at which point you'd need to use the refresh token
                AuthModel.AccessToken = JObject.Parse(testResponse).SelectToken("access_token").ToString();
                AuthModel.TokenType = JObject.Parse(testResponse).SelectToken("token_type").ToString();
                AuthModel.ExpiresAt = DateTime.Now.AddSeconds(int.Parse(JObject.Parse(testResponse).SelectToken("expires_in").ToString()));
                AuthModel.RefreshToken = JObject.Parse(testResponse).SelectToken("refresh_token").ToString();
                AuthModel.TokenId = JObject.Parse(testResponse).SelectToken("id_token")?.ToString();
            }
            catch (JsonReaderException)
            {
                // or it can be in "query string" format (param1=val1&param2=val2)
                var collection = System.Web.HttpUtility.ParseQueryString(testResponse);
                //var token = collection["access_token"];
                AuthModel.AccessToken = collection["access_token"];
                AuthModel.TokenType = collection["token_type"];
                AuthModel.ExpiresAt = DateTime.Now.AddSeconds(int.Parse(JObject.Parse(testResponse).SelectToken("expires_in").ToString()));
                AuthModel.RefreshToken = collection["refresh_token"];
            }
        }*/

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}