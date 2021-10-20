namespace YahooFantasyFootball.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using System.Xml.Serialization;
    using YahooFantasyFootball.Configuration;
    using YahooFantasyFootball.Infrastructure;
    using YahooFantasyFootball.Models;

    public class ScoreboardController : Controller
    {
        private static HttpClient client = new HttpClient();

        public async Task<IActionResult> Index()
        {
            // we need to first check to make sure the token isn't null (if the site hasn't been refreshed in a while and
            // is attempted to be refreshed on the scoreboard, it will be null
            if (AuthModel.AccessToken != null)
            {
                List<Team> teams = new List<Team>();

                // TESTING pulling multiple players back from Yahoo API
                /*client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AuthModel.AccessToken);
                HttpRequestMessage request = new HttpRequestMessage();
                request.RequestUri = new Uri("https://fantasysports.yahooapis.com/fantasy/v2/league/nfl.l.434497/players;player_keys={30123},{30977}");
                request.Method = HttpMethod.Get;
                var response2 = client.GetAsync(request.RequestUri);
                string testResponse = await response2.Result.Content.ReadAsStringAsync();*/

                // TESTING
                //client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Auth.AccessToken);
                //string LoginString = ";use_login=1";
                //request = new HttpRequestMessage();
                //request.RequestUri = new Uri("https://fantasysports.yahooapis.com/fantasy/v2/game/nfl");

                // My league ID, which can only be retrieved manually from going to League-->settings, is 434497
                //request.RequestUri = new Uri("https://fantasysports.yahooapis.com/fantasy/v2/league/nfl.l.434497");

                // Request all teams owned by loggedin user
                // Within a team, there is a <team> element with a <team_key> and <team_id> which can be used later to get information on the team
                //request.RequestUri = new Uri("https://fantasysports.yahooapis.com/fantasy/v2/users;use_login=1/games;game_keys=nfl/teams");

                //request.RequestUri = new Uri("https://fantasysports.yahooapis.com/fantasy/v2/game/nfl");

                // team/<team_key>/roster
                // this will return a list of players and their <player_key>'s and <player_id>'s
                //request.RequestUri = new Uri("https://fantasysports.yahooapis.com/fantasy/v2/team/406.l.244561.t.5/roster");

                // request from the current league <league_id is 406.l.244561> and current player <this is a specific player id>
                //request.RequestUri = new Uri("https://fantasysports.yahooapis.com/fantasy/v2/league/406.l.244561/players;player_keys=406.p.33391/stats");

                // reqeusting player status in a league using a search query for player name (trey lance as an example)
                //request.RequestUri = new Uri("https://fantasysports.yahooapis.com/fantasy/v2/league/406.l.244561/players;search=trey lance/stats");

                // Hashtable to store players grouped by espn game id so we can parse all players in the same doc, limiting
                // the number of times we need to download the doc
                Hashtable testPlayers = new Hashtable();

                SelectedPlayer player;

                string owner = "Liz";
                List<SelectedPlayer> teamOnePlayers = new List<SelectedPlayer>();

                string espnGameId = "401326422";
                string espnPlayerId = "3918298";
                string homeOrAway = "away";
                string playerName = "josh allen";
                string opponentAbbreviation = "";
                player = await CreatePlayer("https://fantasysports.yahooapis.com/fantasy/v2/league/406.l.244561/players;search=" + playerName + "/stats", Position.QB, espnPlayerId, espnGameId, homeOrAway, playerName, opponentAbbreviation, owner);

                addPlayerToHashtable(testPlayers, espnGameId, player);

                espnGameId = "401326411";
                espnPlayerId = "3068267";
                homeOrAway = "away";
                playerName = "austin ekeler";
                player = await CreatePlayer("https://fantasysports.yahooapis.com/fantasy/v2/league/406.l.244561/players;search=" + playerName + "/stats", Position.RB, espnPlayerId, espnGameId, homeOrAway, playerName, opponentAbbreviation, owner);

                addPlayerToHashtable(testPlayers, espnGameId, player);

                espnGameId = "401326420";
                espnPlayerId = "3051392";
                homeOrAway = "away";
                playerName = "ezekiel elliott";
                player = await CreatePlayer("https://fantasysports.yahooapis.com/fantasy/v2/league/406.l.244561/players;search=" + playerName + "/stats", Position.RB, espnPlayerId, espnGameId, homeOrAway, playerName, opponentAbbreviation, owner);

                addPlayerToHashtable(testPlayers, espnGameId, player);

                espnGameId = "401326422";
                espnPlayerId = "2976212";
                homeOrAway = "away";
                playerName = "stefon diggs";
                player = await CreatePlayer("https://fantasysports.yahooapis.com/fantasy/v2/league/406.l.244561/players;search=" + playerName + "/stats", Position.WR, espnPlayerId, espnGameId, homeOrAway, playerName, opponentAbbreviation, owner);

                addPlayerToHashtable(testPlayers, espnGameId, player);

                espnGameId = "401326412";
                espnPlayerId = "3915416";
                homeOrAway = "home";
                playerName = "dj moore";
                player = await CreatePlayer("https://fantasysports.yahooapis.com/fantasy/v2/league/406.l.244561/players;search=" + playerName + "/stats", Position.WR, espnPlayerId, espnGameId, homeOrAway, playerName, opponentAbbreviation, owner);

                addPlayerToHashtable(testPlayers, espnGameId, player);

                espnGameId = "401326418";
                espnPlayerId = "3059915";
                homeOrAway = "home";
                playerName = "kareem hunt";
                player = await CreatePlayer("https://fantasysports.yahooapis.com/fantasy/v2/league/406.l.244561/players;search=" + playerName + "/stats", Position.FLEX, espnPlayerId, espnGameId, homeOrAway, playerName, opponentAbbreviation, owner);

                addPlayerToHashtable(testPlayers, espnGameId, player);

                espnGameId = "401326411";
                espnPlayerId = "3116365";
                homeOrAway = "home";
                playerName = "mark andrews";
                player = await CreatePlayer("https://fantasysports.yahooapis.com/fantasy/v2/league/406.l.244561/players;search=" + playerName + "/stats", Position.TE, espnPlayerId, espnGameId, homeOrAway, playerName, opponentAbbreviation, owner);

                addPlayerToHashtable(testPlayers, espnGameId, player);

                espnGameId = "401326411";
                espnPlayerId = "15683";
                homeOrAway = "home";
                playerName = "justin tucker";
                player = await CreatePlayer("https://fantasysports.yahooapis.com/fantasy/v2/league/406.l.244561/players;search=" + playerName + "/stats", Position.K, espnPlayerId, espnGameId, homeOrAway, playerName, opponentAbbreviation, owner);

                addPlayerToHashtable(testPlayers, espnGameId, player);

                espnGameId = "401326422";
                espnPlayerId = "";
                homeOrAway = "away";
                playerName = "buffalo";
                opponentAbbreviation = "ten";
                player = await CreatePlayer("https://fantasysports.yahooapis.com/fantasy/v2/league/406.l.244561/players;search=" + playerName + "/stats", Position.DEF, espnPlayerId, espnGameId, homeOrAway, playerName, opponentAbbreviation, owner);

                addPlayerToHashtable(testPlayers, espnGameId, player);

                owner = "Chris";
                List<SelectedPlayer> teamTwoPlayers = new List<SelectedPlayer>();

                espnGameId = "401326417";
                espnPlayerId = "3139477";
                homeOrAway = "away";
                playerName = "patrick mahomes";
                player = await CreatePlayer("https://fantasysports.yahooapis.com/fantasy/v2/league/406.l.244561/players;search=" + playerName + "/stats", Position.QB, espnPlayerId, espnGameId, homeOrAway, playerName, opponentAbbreviation, owner);

                addPlayerToHashtable(testPlayers, espnGameId, player);

                espnGameId = "401326422";
                espnPlayerId = "3043078";
                homeOrAway = "home";
                playerName = "derrick henry";
                player = await CreatePlayer("https://fantasysports.yahooapis.com/fantasy/v2/league/406.l.244561/players;search=" + playerName + "/stats", Position.RB, espnPlayerId, espnGameId, homeOrAway, playerName, opponentAbbreviation, owner);

                addPlayerToHashtable(testPlayers, espnGameId, player);

                espnGameId = "401326415";
                espnPlayerId = "4242335";
                homeOrAway = "home";
                playerName = "jonathan taylor";
                player = await CreatePlayer("https://fantasysports.yahooapis.com/fantasy/v2/league/406.l.244561/players;search=" + playerName + "/stats", Position.RB, espnPlayerId, espnGameId, homeOrAway, playerName, opponentAbbreviation, owner);

                addPlayerToHashtable(testPlayers, espnGameId, player);

                espnGameId = "401326413";
                espnPlayerId = "16800";
                homeOrAway = "away";
                playerName = "davante adams";
                player = await CreatePlayer("https://fantasysports.yahooapis.com/fantasy/v2/league/406.l.244561/players;search=" + playerName + "/stats", Position.WR, espnPlayerId, espnGameId, homeOrAway, playerName, opponentAbbreviation, owner);

                addPlayerToHashtable(testPlayers, espnGameId, player);

                espnGameId = "401326417";
                espnPlayerId = "3116406";
                homeOrAway = "away";
                playerName = "tyreek hill";
                player = await CreatePlayer("https://fantasysports.yahooapis.com/fantasy/v2/league/406.l.244561/players;search=" + playerName + "/stats", Position.WR, espnPlayerId, espnGameId, homeOrAway, playerName, opponentAbbreviation, owner);

                addPlayerToHashtable(testPlayers, espnGameId, player);

                espnGameId = "401326421";
                espnPlayerId = "4241457";
                homeOrAway = "home";
                playerName = "najee harris";
                player = await CreatePlayer("https://fantasysports.yahooapis.com/fantasy/v2/league/406.l.244561/players;search=" + playerName + "/stats", Position.FLEX, espnPlayerId, espnGameId, homeOrAway, playerName, opponentAbbreviation, owner);

                addPlayerToHashtable(testPlayers, espnGameId, player);

                espnGameId = "401326417";
                espnPlayerId = "15847";
                homeOrAway = "away";
                playerName = "travis kelce";
                player = await CreatePlayer("https://fantasysports.yahooapis.com/fantasy/v2/league/406.l.244561/players;search=" + playerName + "/stats", Position.TE, espnPlayerId, espnGameId, homeOrAway, playerName, opponentAbbreviation, owner);

                addPlayerToHashtable(testPlayers, espnGameId, player);

                espnGameId = "401326422";
                espnPlayerId = "3917232";
                homeOrAway = "away";
                playerName = "tyler bass";
                player = await CreatePlayer("https://fantasysports.yahooapis.com/fantasy/v2/league/406.l.244561/players;search=" + playerName + "/stats", Position.K, espnPlayerId, espnGameId, homeOrAway, playerName, opponentAbbreviation, owner);

                addPlayerToHashtable(testPlayers, espnGameId, player);

                espnGameId = "401326416";
                espnPlayerId = "";
                homeOrAway = "away";
                playerName = "los angeles rams";
                opponentAbbreviation = "nyg";
                // Rams and Chargers are both called "Los Angeles" in yahoo, so we can only send in "los angeles" to the query. But, it will pull back
                // both teams in the API search, so we will need to search the data coming back for each and select the right one based on "rams", in this case
                player = await CreatePlayer("https://fantasysports.yahooapis.com/fantasy/v2/league/406.l.244561/players;search=" + playerName.Substring(0, playerName.LastIndexOf(" ")) + "/stats", Position.DEF, espnPlayerId, espnGameId, homeOrAway, playerName, opponentAbbreviation, owner);

                addPlayerToHashtable(testPlayers, espnGameId, player);


                // keep track of the total points from players on each team
                double teamOneTotalPoints = 0;
                double teamTwoTotalPoints = 0;

                // loop through each key (espn game id) and parse the points for each player in that game,
                // adding each SelectedPlayer in the hashtable to the approprate list of teams (team one or team two)
                foreach (string key in testPlayers.Keys)
                {
                    List<SelectedPlayer> selectedPlayers = (List<SelectedPlayer>) testPlayers[key];

                    EspnHtmlScraper scraper = new EspnHtmlScraper(key);

                    // calculate points for each of these players
                    foreach (SelectedPlayer p in selectedPlayers)
                    {
                        p.Points = calculateLiveFantasyPoints(scraper, p.EspnPlayerId, p.Position, key, p.HomeOrAway, p.RawPlayerName, p.OpponentAbbreviation);

                        // add this player to the appropate player list
                        if (p.Owner.Equals("Liz"))
                        {
                            teamOneTotalPoints += p.Points;
                            teamOnePlayers.Add(p);
                        }
                        else if (p.Owner.Equals("Chris"))
                        {
                            teamTwoTotalPoints += p.Points;
                            teamTwoPlayers.Add(p);
                        }
                    }
                }

                // sort the teams
                teamOnePlayers = teamOnePlayers.OrderBy(x => (int)(x.Position)).ToList();
                teamTwoPlayers = teamTwoPlayers.OrderBy(x => (int)(x.Position)).ToList();

                Team team = new Team
                {
                    Owner = "Liz",
                    TotalFantasyPoints = Math.Round(teamOneTotalPoints, 2),
                    Players = teamOnePlayers
                };

                teams.Add(team);

                team = new Team
                {
                    Owner = "Chris",
                    TotalFantasyPoints = Math.Round(teamTwoTotalPoints, 2),
                    Players = teamTwoPlayers
                };

                teams.Add(team);

                return View(teams);
            }
            else
            {
                // redirect to the home controller's index action so we can get a new token
                return RedirectToAction("Index", "Home");
            }
        }

        /// <summary>
        /// Adds a player to the corresponding espnGameId key so all players in the same game will be in a list
        /// of players with the esponGameId being hte key.
        /// </summary>
        /// <param name="playerTable">The hashtable holding all players grouped by espn game ids</param>
        /// <param name="espnGameId">ESPON Game ID this player is playing in</param>
        /// <param name="player">The SelectedPlayer we are adding</param>
        private void addPlayerToHashtable(Hashtable playerTable, string espnGameId, SelectedPlayer player)
        {
            if (playerTable.ContainsKey(espnGameId))
            {
                List<SelectedPlayer> playerList = (List<SelectedPlayer>)playerTable[espnGameId];
                playerList.Add(player);
                playerTable[espnGameId] = playerList;
            }
            else
            {
                List<SelectedPlayer> playerList = new List<SelectedPlayer>();
                playerList.Add(player);
                playerTable.Add(espnGameId, playerList);
            }
        }

        /// <summary>
        /// Creates the player based on the api Query for the player. THe other details for espn are supplied
        /// so that we can scrape the espn boxscore and playbyplay pages to get live stats.
        /// </summary>
        /// <param name="apiQuery">Yahoo API query for the player</param>
        /// <param name="position">Position of the player (QB, RB, WR, TE, K, DEF)</param>
        /// <param name="espnPlayerId">Player ID on ESPN so we can parse stats for this player on ESPNs pages</param>
        /// <param name="espnGameId">Game ID on ESPN so we can parse the correct game to get stats for this player</param>
        /// <param name="homeOrAway">"home" or "away" game, which is needed to find the correct stats on ESPN for the player</param>
        /// <param name="playerName">Player's name which is only used to search for 2-point conversions in the ESPN play by play page</param>
        /// <param name="opponentAbbreviation">If this palyer is a defense, this parameter is the abbreviation of their opponent</param>
        /// <returns></returns>
        private async Task<SelectedPlayer> CreatePlayer(string apiQuery, Position position, string espnPlayerId, string espnGameId, string homeOrAway, string playerName, string opponentAbbreviation, string owner)
        {
            //HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AuthModel.AccessToken);

            HttpRequestMessage request = new HttpRequestMessage();
            request.RequestUri = new Uri(apiQuery);
            request.Method = HttpMethod.Get;
            var response2 = client.GetAsync(request.RequestUri);
            string testResponse = await response2.Result.Content.ReadAsStringAsync();

            XDocument document = XDocument.Parse(testResponse);

            XmlSerializer serializer = new XmlSerializer(typeof(Player));
            List<XElement> xElements = document.Descendants(YahooXml.XMLNS + "player").ToList();
            //List<XElement> xElements = document.Descendants().ToList();
            //List<Player> collection = new List<Player>();
            // we are only looking at one player, so commenting out for now
            /*foreach (var element in xElements)
            {
                collection.Add((Player)serializer.Deserialize(element.CreateReader()));
            }*/

            // Player object pulled from the Yahoo API request
            Player player = null;

            // if we have a player (or defense, such as Los Angeles) with the same name, we need
            // to check the player name in each xElement to select the right team
            if (xElements.Count > 1)
            {
                foreach (XElement xElement in xElements)
                {
                    if (xElement.Value.ToLower().Contains(playerName))
                    {
                        player = (Player)serializer.Deserialize(xElement.CreateReader());

                        break;
                    }
                }
            }
            else
            {
                player = (Player)serializer.Deserialize(xElements.First().CreateReader());
            }

            // Player we create from the data returned from Yahoo's API as well as fantasy points we scrape from the espn grametracker
            // and play by play pages
            SelectedPlayer selectedPlayer = new SelectedPlayer();
            selectedPlayer.Name = player.Name.First + " " + player.Name.Last;
            selectedPlayer.Headshot = player.Headshot.Url;
            selectedPlayer.Position = position;
            selectedPlayer.EspnGameId = espnGameId;
            selectedPlayer.EspnPlayerId = espnPlayerId;
            selectedPlayer.OpponentAbbreviation = opponentAbbreviation;
            selectedPlayer.RawPlayerName = playerName;
            selectedPlayer.HomeOrAway = homeOrAway;
            selectedPlayer.Owner = owner;


            return selectedPlayer;
        }

        /// <summary>
        /// Helper function to calculate the live fantasy points for a specific player
        /// </summary>
        /// <param name="espnPlayerId">Player ID on ESPN so we can parse stats for this player on ESPNs pages</param>
        /// <param name="espnGameId">Game ID on ESPN so we can parse the correct game to get stats for this player</param>
        /// <param name="homeOrAway">"home" or "away" game, which is needed to find the correct stats on ESPN for the player</param>
        /// <param name="playerName">Player's name which is only used to search for 2-point conversions in the ESPN play by play page</param>
        /// <param name="opponentAbbreviation">If this palyer is a defense, this parameter is the abbreviation of their opponent</param>
        /// <returns></returns>
        private double calculateLiveFantasyPoints(EspnHtmlScraper scraper, string espnPlayerId, Position position, string espnGameId, string homeOrAway, string playerName, string opponentAbbreviation)
        {
            double fantasyPoints = 0;

            fantasyPoints += scraper.parseGameTrackerPage(espnGameId, espnPlayerId, homeOrAway, opponentAbbreviation);
            fantasyPoints += scraper.parseTwoPointConversionsForPlayer(espnGameId, playerName);

            // calculate kicker FGs if this player is a kicker
            if (position == Position.K)
            {
                fantasyPoints += scraper.parseFieldGoals(espnGameId, playerName);
            }

            return fantasyPoints;
        }
    }
}