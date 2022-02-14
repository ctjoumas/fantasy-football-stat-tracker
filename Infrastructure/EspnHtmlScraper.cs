namespace FantasyFootballStatTracker.Infrastructure
{
    using System;
    using FantasyFootballStatTracker;
    using FantasyFootballStatTracker.Models;
    using HtmlAgilityPack;
    using Newtonsoft.Json.Linq;

    public class EspnHtmlScraper
    {
        // The gametracker doc for a specific game, which can be shared for all players in the same game, saving time from loading the doc
        private HtmlDocument gameTrackerDoc;

        // The play by play doc for a specific game, which can be shared for all players in the same game, saving time from loading the doc
        private HtmlDocument playByPlayDoc;

        // The play by play parser, which will either be using the JSON in the play by play if it exists, or the HTML doc
        private IPlayByPlayParser playByPlayParser;

        // URL constants for parsing different stats
        private const string BOXSCORE_PAGE_URL = "https://www.espn.com/nfl/boxscore/_/gameId/";
        private const string PLAY_BY_PLAY_URL = "https://www.espn.com/nfl/playbyplay/_/gameId/";

        // Scoring constants for different positions
        private const int PASS_TD_POINTS = 4;
        private const int REC_TD_POINTS = 6;
        private const int RUSH_TD_POINTS = 6;
        private const double PASS_YARDS_PER_POINT = 25.0;
        private const double RUSH_YARDS_PER_POINT = 10.0;
        private const double RECEIVING_YARDS_PER_POINT = 10.0;
        private const int FUMBLE_LOST_POINTS = -2;
        private const int INTERCEPTION_POINTS = -1;
        private const int DEFENSIVE_SACK_POINTS = 1;
        private const int DEFENSIVE_TD_POINTS = 6;
        private const int DEFENSIVE_INT_POINTS = 2;

        // Stores whether the game has ended. If it has, the ScoreboardController will check after parsing and update
        // the CurrentRoster table with "true" for game ended and final points for the player. This is initially set to false
        public bool GameEnded { get; set; } = false;

        /// <summary>
        /// Sets the gametracker and play by play HTML documents which will be used to gather stats for every player playing
        /// in the same game.
        /// </summary>
        /// <param name="gameId">The ID of the ESPN game we are getting stats from</param>
        public EspnHtmlScraper(string espnGameId)
        {
            string gameTrackerUrl = BOXSCORE_PAGE_URL + espnGameId;
            gameTrackerDoc = new HtmlWeb().Load(gameTrackerUrl);

            string playByPlayUrl = PLAY_BY_PLAY_URL + espnGameId;
            playByPlayDoc = new HtmlWeb().Load(playByPlayUrl);

            JObject playByPlayJsonObject = GetPlayByPlayJsonObject();

            // Setup the play by play parser, which will be a JSON or HTML parser depending on if the JSON exists
            IPlayByPlayParserFactory playByPlayParserFactory = new PlayByPlayParserFactory();
            playByPlayParser = playByPlayParserFactory.GetPlayByPlayParser(playByPlayJsonObject, playByPlayDoc);
        }

        /// <summary>
        /// When a game is in progress, the play by play data is updated in the javascript function's espn.gamepackage.data variable.
        /// This method will find this variable and pull out the JSON and store it so we can parse the live play by play data.
        /// </summary>
        /// <returns>The JSON object representing the play by play data.</returns>
        public JObject GetPlayByPlayJsonObject()
        {
            JObject playByPlayJsonObject = null;

            var playByPlayJavaScriptNode = playByPlayDoc.DocumentNode.SelectNodes("//script[@type='text/javascript']");

            foreach (var scriptNode in playByPlayJavaScriptNode)
            {
                if (scriptNode.InnerText.Contains("espn.gamepackage.data"))
                {
                    string[] javascriptLines = scriptNode.InnerText.Split('\n');

                    foreach (var line in javascriptLines)
                    {
                        if (line.Contains("espn.gamepackage.data"))
                        {
                            string variable = line.Trim();

                            variable = variable.Substring(variable.IndexOf("{"));

                            // there is a trailing ;, so pull that off
                            variable = variable.Substring(0, variable.Length - 1);

                            // load into JSON object
                            playByPlayJsonObject = JObject.Parse(variable);

                            break;
                        }
                    }

                    break;
                }
            }

            return playByPlayJsonObject;
        }

        /// <summary>
        /// This method will check to see if the JSON should be used to parse the game or HTML. When a game ends, at some point
        /// the JSON is removed and all data exists in the HTML; this would only happen if the scoreboard is not viewed until
        /// after a game ends, so this is here just in case that edge case happens.
        /// </summary>
        /// <returns></returns>
        public string parseTimeRemaining()
        {
            string timeRemaining = playByPlayParser.parseTimeRemaining();

            return timeRemaining;
        }

        /// <summary>
        /// Parses the current score for a given player. We don't care about the player name, we just need to know if
        /// the player is home or away so we can list that score first.
        /// </summary>
        /// <param name="homeOrAway">Home or away for the given player so we know which score to display first</param>
        /// <returns>A score in the format of "20-17", with the first number being the home or away score.</returns>
        public string parseCurrentScore(string homeOrAway)
        {
            string currentScoreString = playByPlayParser.parseCurrentScore(homeOrAway);

            return currentScoreString;
        }

        /// <summary>
        /// Gets the final score string (such as "(W) 45 - 30") and store this in the database
        /// </summary>
        /// <param name="teamAbbreviation"></param>
        /// <returns></returns>
        public string parseFinalScore(string teamAbbreviation)
        {
            string finalScoreString = "";

            // get the final score nodes, which are under the <div class="game-status"> node. This will return TR nodes which will look like:
            // <tr>
            //   <td class="team-name">NYJ</td>
            //   <td>7</td>
            //   <td>3</td>
            //   <td>6</td>
            //   <td>14</td>
            //   <td class="final-score">30</td>
            // </tr>
            // <tr>
            //   <td class="team-name">IND</td>
            //   <td>7</td>
            //   <td>21</td>
            //   <td>14</td>
            //   <td>3</td>
            //   <td class="final-score">45</td>
            // </tr>
            var finalScoreTrNodes = playByPlayDoc.DocumentNode.SelectNodes("//div[@class='game-status']/div/table/tbody/tr");

            int playerTeamScore = 0;
            int opponentTeamScore = 0;

            // go through each node and find the opponent team's score and this player's team's score
            foreach (var finalScoreTrNode in finalScoreTrNodes)
            {
                // the first and last child node is what we are interested in (the "team-name" and "final-score" nodes)
                string teamName = finalScoreTrNode.FirstChild.InnerText;//.Attributes[0].Value;

                // check if it's this players team or the opponent team
                if (teamName.ToLower().Equals(teamAbbreviation.ToLower()))
                {
                    // pull out the player's teams score
                    playerTeamScore = int.Parse(finalScoreTrNode.LastChild.InnerText);
                }
                else
                {
                    // pull out the opponent score
                    opponentTeamScore = int.Parse(finalScoreTrNode.LastChild.InnerText);
                }
            }

            if (playerTeamScore > opponentTeamScore)
            {
                finalScoreString += "(W) " + playerTeamScore.ToString() + "-" + opponentTeamScore.ToString();
            }
            else
            {
                finalScoreString += "(L) " + playerTeamScore.ToString() + "-" + opponentTeamScore.ToString();
            }

            return finalScoreString;
        }

        /// <summary>
        /// This method will check to see if the JSON should be used to parse the game or HTML. When a game ends, at some point
        /// the JSON is removed and all data exists in the HTML; this would only happen if the scoreboard is not viewed until
        /// after a game ends, so this is here just in case that edge case happens.
        /// </summary>
        /// <returns></returns>
        public double parseTwoPointConversionsForPlayer(string playerName)
        {
            double fantasyPoints = playByPlayParser.parseTwoPointConversionsForPlayer(playerName);

            return fantasyPoints;
        }

        /// <summary>
        /// This method will check to see if the JSON should be used to parse the game or HTML. When a game ends, at some point
        /// the JSON is removed and all data exists in the HTML; this would only happen if the scoreboard is not viewed until
        /// after a game ends, so this is here just in case that edge case happens.
        /// </summary>
        /// <returns></returns>
        public int parseFieldGoals(string playerName)
        {
            int fieldGoalPoints = playByPlayParser.parseFieldGoals(playerName);

            return fieldGoalPoints;
        }

        /// <summary>
        /// Parses the gametracker page for a particualr gameId and gets all relevant fantasy stats
        /// for the given playerId.
        /// </summary>
        /// <param name="gameId">Game ID of the game this player is playing in</param>
        /// <param name="playerId">Player ID of the player we are pulling stats for. If this is "", then we are looking for a team defense</param>
        /// <param name="homeOrAway">Depending on if the player is playing a home or away game, we find stats in a particular data tag</param>
        /// <param name="opponentAbbreviation">This is only used when scoring a team defnese so we know when an opponent scores a 2 point conversion</param>
        /// <returns>Fantasy points for this player (without 2-pt conversions, which is handled in a separate method</returns>
        public double parseGameTrackerPage(string gameId, string playerId, Position position, string homeOrAway, string opponentAbbreviation)
        {
            double fantasyPoints = 0;

            // if a game has ended, we will find this node:
            // < span class="game-time status-detail">Final</span>
            var statusDetailNode = gameTrackerDoc.DocumentNode.SelectSingleNode("//span[@class='game-time status-detail']");
            if ((statusDetailNode != null) && (statusDetailNode.InnerText.ToLower().Equals("final") || statusDetailNode.InnerText.ToLower().Equals("final/ot")))
            {
                GameEnded = true;
            }

            // if the player is home, their stats will be in column two; away is column one
            string column_identifier = "";
            if (homeOrAway.Equals("home"))
            {
                column_identifier = "column-two";
            }
            else if (homeOrAway.Equals("away"))
            {
                column_identifier = "column-one";
            }

            // get all of the div nodes which contain the various team stats for the given player
            // e.g. - this is the <div class="col column-two gamepackage-home-wrap"> node, which will have something like Baltimore Passing under it
            // including Passing, Rushing, Receiving, Fumbles, Interception, Kicking, and others which we don't care about for fantasy points (kick
            // returns, punt returns, etc)
            var gamePackageNodes = gameTrackerDoc.DocumentNode.SelectNodes("//div[@class='col " + column_identifier + " gamepackage-" + homeOrAway + "-wrap']");

            // if gamePackageNodes is null, this means that the game hasn't started yet, so we'll end
            // and just return 0 fantasy points
            if (gamePackageNodes != null)
            {
                foreach (var gamePackageNode in gamePackageNodes)
                {
                    // now we need to select the <div class="team-name"> tag which has the <team> <stat> text
                    // such as "Baltimore Passing" 
                    var teamNameNode = gamePackageNode.SelectSingleNode(".//div[@class='team-name']");

                    // find the last space in the team name so we can grab the stat
                    // e.g. - "Los Angeles Passing" or "Carolina Passing" should return "Passing"
                    string teamNameText = teamNameNode.InnerText;
                    int lastSpaceIndex = teamNameText.LastIndexOf(" ");
                    string stat = teamNameText.Substring(lastSpaceIndex + 1);

                    // if we are parsing the defensive stats ("Defensive" for sacks, "Interceptions" for interceptions, or "Returns" for kick/punt
                    // returns, we can just check the last row which is the team total so we don't have to loop through each player on the team
                    if (position.Equals(Position.DEF))
                    {
                        var defensiveTeamStatsTotalNode = gamePackageNode.SelectSingleNode(".//tr[@class='highlight']");

                        if (stat.Equals("Defensive"))
                            fantasyPoints += handleDefensiveStats(defensiveTeamStatsTotalNode);
                        else if (stat.Equals("Interceptions"))
                            fantasyPoints += handleInterceptionStats(defensiveTeamStatsTotalNode);
                        else if (stat.Equals("Returns"))
                            fantasyPoints += handleReturnStats(defensiveTeamStatsTotalNode);
                    }
                    else
                    {
                        // now we need to find the table row under the "mod-data" table, which contains the stats. In this
                        // table, there are two rows of data under the <tbody> for each player. The first row will contain <td>s of all stats,
                        // with the first td being the player id which we will need to check so we are pulling stats for the
                        // right player.
                        var statsNodes = gamePackageNode.SelectNodes(".//table[@class='mod-data']/tbody/tr");

                        foreach (var statsNode in statsNodes)
                        {
                            // the stats node is a <tr> with <td>'s containing the stats we are looking for, which will be different
                            // for each type of stat (passing, rushing, etc), so we will hand this node off to the helper function
                            // after extracting the player id in a link tag
                            var playerIdNode = statsNode.SelectSingleNode(".//a");

                            // if there is a playerID coming into this function and there isn't a player for this stat (e.g. this
                            // player has no interceptions), there will not be a player ID Node, so we will just skip this stat
                            if (playerIdNode != null)
                            {
                                string playerUid = playerIdNode.Attributes["data-player-uid"].Value;

                                // the beginning part of the ID has a GUID, so we will remove that and just return the player id
                                int index = playerUid.IndexOf(playerId);

                                if (index != -1)
                                {
                                    string extractedPlayerId = playerUid.Substring(index);

                                    if (extractedPlayerId.Equals(playerId))
                                    {
                                        if (stat.Equals("Passing"))
                                            fantasyPoints += handlePassingStats(statsNode);
                                        else if (stat.Equals("Rushing"))
                                            fantasyPoints += handleRbStats(statsNode);
                                        else if (stat.Equals("Receiving"))
                                            fantasyPoints += handleWrStats(statsNode);
                                        else if (stat.Equals("Fumbles"))
                                            fantasyPoints += handleFumbleStats(statsNode);
                                        else if (stat.Equals("Kicking"))
                                            fantasyPoints += handleKickingStats(statsNode);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // if we are scoring for team defense, we need to check how many points were scored
            // against them as well as whether they blocked any kicks or field goals
            if (position.Equals(Position.DEF))
            {
                fantasyPoints += handleDefenseTeamPoints(gameTrackerDoc, homeOrAway, gameId, opponentAbbreviation);
                fantasyPoints += handleBlockedKicksAndPunts(opponentAbbreviation);
            }

            return Math.Round(fantasyPoints, 2);
        }

        /// <summary>
        /// Once we parse all data points, we need to then check and see how many offensive
        /// points the opponent scored on the defense (we will not count any defensive scoring) and
        /// adjust the points awarded to the defensive team, which will then be added to all sacks, ints,
        /// and return touchdowns they scored.
        /// </summary>
        /// <returns></returns>
        private int handleDefenseTeamPoints(HtmlDocument doc, string homeOrAway, string gameId, string opponentAbbreviation)
        {
            int defensivePoints = 0;
            int pointsAllowed = 0;

            // since we are looking at the opponents scoring, we need to check the opponents column
            string column_identifier = "";
            if (homeOrAway.Equals("home"))
            {
                column_identifier = "column-one";
                homeOrAway = "away";
            }
            else if (homeOrAway.Equals("away"))
            {
                column_identifier = "column-two";
                homeOrAway = "home";
            }

            // Get all of the div nodes which contain the various team stats. But, check first if this is null (if the game hasn't started).
            // e.g. - this is the <div class="col column-two gamepackage-home-wrap"> node, which will have something like Baltimore Passing under it
            // including Passing, Rushing, Receiving, Fumbles, Interception, Kicking, and others. All we are interested in is points scored agains this
            // defense, so we'll check team passing TDs, team receiving TDs, FGs and XPs.
            var gamePackageNodes = doc.DocumentNode.SelectNodes("//div[@class='col " + column_identifier + " gamepackage-" + homeOrAway + "-wrap']");

            if (gamePackageNodes != null)
            {
                foreach (var gamePackageNode in gamePackageNodes)
                {
                    // now we need to select the <div class="team-name"> tag which has the <team> <stat> text
                    // such as "Baltimore Passing" 
                    var teamNameNode = gamePackageNode.SelectSingleNode(".//div[@class='team-name']");

                    // find the last space in the team name so we can grab the stat
                    // e.g. - "Lost Angeles Passing" or "Carolina Passing" should return "Passing"
                    string teamNameText = teamNameNode.InnerText;
                    int lastSpaceIndex = teamNameText.LastIndexOf(" ");
                    string stat = teamNameText.Substring(lastSpaceIndex + 1);

                    // we are only looking at touchdowns (we could look for passing or receiving; but not both so we don't double count TDs)
                    if (stat.Equals("Passing") || stat.Equals("Rushing"))
                    {
                        var statsNodes = gamePackageNode.SelectNodes(".//table[@class='mod-data']/tbody/tr");

                        // instead of checking each player, we only care about the last row, which is the team stats. Here we can grab the total TDs
                        var statsNode = statsNodes[statsNodes.Count - 1];

                        foreach (var node in statsNode.ChildNodes)
                        {
                            // it's possible the opponent hasn't had a drive yet so it will show something like "No Atlanta Passing", so check
                            // there there are attributes in this row
                            if (node.Attributes.Count > 0)
                            {
                                string statType = node.Attributes[0].Value;

                                // if we have found the class="td" <td>, then we will get the total touchdowns for this stat (passing or rushing)
                                if (statType.Equals("td"))
                                {
                                    pointsAllowed += int.Parse(node.InnerText) * 6;
                                }
                            }
                        }
                    }
                    // we need to see how many fumbles the opponent lost; we'll add these to the defensive points
                    else if (stat.Equals("Fumbles"))
                    {
                        var statsNodes = gamePackageNode.SelectNodes(".//table[@class='mod-data']/tbody/tr");

                        // instead of checking each player, we only care about the last row, which is the team stats. Here we can grab the total fumbles
                        var statsNode = statsNodes[statsNodes.Count - 1];

                        foreach (var node in statsNode.ChildNodes)
                        {
                            // if there are no fumbles, node.Attributes will be 0
                            if (node.Attributes.Count > 0)
                            {
                                string statType = node.Attributes[0].Value;

                                // if we have found the class="td" <td>, then we will get the total touchdowns for this stat (passing or rushing)
                                if (statType.Equals("lost"))
                                {
                                    defensivePoints += int.Parse(node.InnerText) * 2;
                                }
                            }
                        }
                    }
                    else if (stat.Equals("Kicking"))
                    {
                        var statsNodes = gamePackageNode.SelectNodes(".//table[@class='mod-data']/tbody/tr");

                        // instead of checking each player, we only care about the last row, which is the team stats. Here we can grab the total FGs and XPs
                        var statsNode = statsNodes[statsNodes.Count - 1];

                        foreach (var node in statsNode.ChildNodes)
                        {
                            // if there hasn't been a kick yet, there will be no attribues
                            if (node.Attributes.Count > 0)
                            {
                                string statType = node.Attributes[0].Value;

                                // we are looking for "pts", which will give the total points scored in FGs and XPs
                                if (statType.Equals("pts"))
                                {
                                    pointsAllowed += int.Parse(node.InnerText);
                                }
                            }
                        }
                    }
                }

                // the only other thing we need to check is if any 2-point conversions were scored since this is not in
                // the boxscore page
                pointsAllowed += handleDefenseTeamPointsWithTwoPointConversions(opponentAbbreviation);

                if (pointsAllowed == 0)
                    defensivePoints += 10;
                else if ((pointsAllowed >= 1) && (pointsAllowed <= 6))
                    defensivePoints += 7;
                else if ((pointsAllowed >= 7) && (pointsAllowed <= 13))
                    defensivePoints += 4;
                else if ((pointsAllowed >= 14) && (pointsAllowed <= 20))
                    defensivePoints += 1;
                else if ((pointsAllowed >= 21) && (pointsAllowed <= 27))
                    defensivePoints += 0;
                else if ((pointsAllowed >= 28) && (pointsAllowed <= 34))
                    defensivePoints += -1;
                else if (pointsAllowed >= 35)
                    defensivePoints += -4;
            }

            return defensivePoints;
        }

        /// <summary>
        /// This method will check to see if the JSON should be used to parse the game or HTML. When a game ends, at some point
        /// the JSON is removed and all data exists in the HTML; this would only happen if the scoreboard is not viewed until
        /// after a game ends, so this is here just in case that edge case happens.
        /// </summary>
        /// <returns></returns>
        public int handleBlockedKicksAndPunts(string playerName)
        {
            int blockedPoints = playByPlayParser.handleBlockedKicksAndPunts(playerName);

            return blockedPoints;
        }

        /// <summary>
        /// This method will check to see if the JSON should be used to parse the game or HTML. When a game ends, at some point
        /// the JSON is removed and all data exists in the HTML; this would only happen if the scoreboard is not viewed until
        /// after a game ends, so this is here just in case that edge case happens.
        /// </summary>
        /// <returns></returns>
        public int handleDefenseTeamPointsWithTwoPointConversions(string playerName)
        {
            int twoPointConversionPoints = playByPlayParser.handleDefenseTeamPointsWithTwoPointConversions(playerName);

            return twoPointConversionPoints;
        }

        /// <summary>
        /// This will loop through all defense (and actually, special teams) kick or punt returns. The only thing we care about here are touchdowns:
        /// <td class="td"></td>
        /// </summary>
        /// <param name="defensiveTeamStatsTotalNode"></param>
        /// <returns></returns>
        private int handleReturnStats(HtmlNode defensiveTeamStatsTotalNode)
        {
            int defensiveReturnPoints = 0;
            /*int touchdowns = 0;

            foreach (var node in defensiveTeamStatsTotalNode.ChildNodes)
            {
                string stat = node.Attributes[0].Value;

                if (stat.Equals("td"))
                    touchdowns += int.Parse(node.InnerText);
            }

            defensiveReturnPoints += touchdowns * DEFENSIVE_TD_POINTS;*/

            return defensiveReturnPoints;
        }

        /// <summary>
        /// This will loop through all interception stats for each player who has interceptions for the given team. These will be in the format of
        /// <td class="int"></td>
        /// <td class="td"></td>
        /// </summary>
        /// <param name="defensiveTeamStatsTotalNode"></param>
        /// <returns></returns>
        private int handleInterceptionStats(HtmlNode defensiveTeamStatsTotalNode)
        {
            int defensivePointsFromInterceptions = 0;

            /*int interceptions = 0;
            int touchdowns = 0;

            foreach (var node in defensiveTeamStatsTotalNode.ChildNodes)
            {
                if (node != null)
                {
                    string stat = node.Attributes[0].Value;

                    // we will only check for int and not TD's since interceptions returned for TD's are accounted for in the
                    // main Defensive stats table
                    if (stat.Equals("int"))
                        interceptions += int.Parse(node.InnerText);
                }
            }

            defensivePointsFromInterceptions += (interceptions * DEFENSIVE_INT_POINTS) + (touchdowns * DEFENSIVE_TD_POINTS);*/

            return defensivePointsFromInterceptions;
        }

        /// <summary>
        /// This will loop through all defensive stats for the team. We are only interested in
        /// sacks and touchdowns, which will be in the format:
        /// <td class="sacks"></td>
        /// <td class="td"></td>
        /// </summary>
        /// <param name="defensiveTeamStatsTotalNode"></param>
        /// <returns></returns>
        private double handleDefensiveStats(HtmlNode defensiveTeamStatsTotalNode)
        {
            double defensivePoints = 0;

            /*double sacks = 0;
            int touchdowns = 0;

            foreach (var node in defensiveTeamStatsTotalNode.ChildNodes)
            {
                string stat = node.Attributes[0].Value;

                if (stat.Equals("sacks"))
                    // a player can get 0.5 sacks, so we need to parse as a double
                    sacks += (double)Convert.ToDouble(node.InnerText);
                else if (stat.Equals("td"))
                    // this will count TDs made on INT and fumble returns, so we only want to calculate the TDs here and not in the
                    // other tables (such as Interceptions, where it will also show up)
                    touchdowns += int.Parse(node.InnerText);
            }

            defensivePoints += (sacks * DEFENSIVE_SACK_POINTS) + (touchdowns * DEFENSIVE_TD_POINTS);*/

            return defensivePoints;
        }

        /// <summary>
        /// Parses the qb stats from the gametracker page. The first td contains the player name,
        /// so we can skip that:
        /// <td class="name">**this has all info we already checked before calling this function***</td>
        /// <td class="c-att"></td>
        /// <td class="yds"></td>
        /// <td class="avg"></td>
        /// <td class="td"></td>
        /// <td class="int">/td>
        /// <td class="sacks"></td>
        /// <td class="qbr"></td>
        /// <td class="rtg"></td>
        /// </summary>
        /// <param name="statsNode"></param>
        /// <returns>QB Fantasy Points</returns>
        private double handlePassingStats(HtmlNode statsNode)
        {
            int passYards = 0;
            int passTds = 0;
            int passInts = 0;

            foreach (var node in statsNode.ChildNodes)
            {
                string stat = node.Attributes[0].Value;

                if (stat.Equals("yds"))
                    passYards = int.Parse(node.InnerText);
                else if (stat.Equals("td"))
                    passTds = int.Parse(node.InnerText);
                else if (stat.Equals("int"))
                    passInts = int.Parse(node.InnerText);
            }

            double qbRelatedPoints = (passYards / PASS_YARDS_PER_POINT) + (passTds * PASS_TD_POINTS) + (passInts * INTERCEPTION_POINTS);

            return qbRelatedPoints;
        }

        /// <summary>
        /// Parses the rb stats from the gametracker page. The first td contains the player name,
        /// so we can skip that:
        /// <td class="name">**this has all info we already checked before calling this function***</td>
        /// <td class="car"></td>
        /// <td class="yds"></td>
        /// <td class="avg"></td>
        /// <td class="td"></td>
        /// <td class="long">/td>
        /// </summary>
        /// <param name="statsNode"></param>
        /// <returns>RB Fantasy Points</returns>
        private double handleRbStats(HtmlNode statsNode)
        {
            int rushYards = 0;
            int rushTds = 0;

            foreach (var node in statsNode.ChildNodes)
            {
                string stat = node.Attributes[0].Value;

                if (stat.Equals("yds"))
                    rushYards = int.Parse(node.InnerText);
                else if (stat.Equals("td"))
                    rushTds = int.Parse(node.InnerText);
            }

            double rbRelatedPoints = (rushYards / RUSH_YARDS_PER_POINT) + (rushTds * RUSH_TD_POINTS);

            return rbRelatedPoints;
        }

        /// <summary>
        /// Parses the wr stats from the gametracker page. The first td contains the player name,
        /// so we can skip that:
        /// <td class="name">**this has all info we already checked before calling this function***</td>
        /// <td class="rec"></td>
        /// <td class="yds"></td>
        /// <td class="avg"></td>
        /// <td class="td"></td>
        /// <td class="long">/td>
        /// <td class="tgts">/td>
        /// </summary>
        /// <param name="statsNode"></param>
        /// <returns>WR Fantasy Points</returns>
        private double handleWrStats(HtmlNode statsNode)
        {
            int receivingYards = 0;
            int receivingTds = 0;

            foreach (var node in statsNode.ChildNodes)
            {
                string stat = node.Attributes[0].Value;

                if (stat.Equals("yds"))
                    receivingYards = int.Parse(node.InnerText);
                else if (stat.Equals("td"))
                    receivingTds = int.Parse(node.InnerText);
            }

            double wrRelatedPoints = (receivingYards / RECEIVING_YARDS_PER_POINT) + (receivingTds * REC_TD_POINTS);

            return wrRelatedPoints;
        }

        /// <summary>
        /// Parses the fumble stats from the gametracker page. The first td contains the player name,
        /// so we can skip that:
        /// <td class="name">**this has all info we already checked before calling this function***</td>
        /// <td class="fum"></td>
        /// <td class="lost"></td>
        /// <td class="rec"></td>
        /// </summary>
        /// <param name="statsNode"></param>
        /// <returns>WR Fantasy Points</returns>
        private double handleFumbleStats(HtmlNode statsNode)
        {
            int fumblesLost = 0;

            foreach (var node in statsNode.ChildNodes)
            {
                string stat = node.Attributes[0].Value;

                if (stat.Equals("lost"))
                    fumblesLost = int.Parse(node.InnerText);
            }

            double fumblePoints = fumblesLost * FUMBLE_LOST_POINTS;

            return fumblePoints;
        }

        /// <summary>
        /// Parses the kicking stats from the gametracker page. The first td contains the player name,
        /// so we can skip that, and we actually only need the pts field since that is based on fantasy points.
        /// <td class="fg">**this has all info we already checked before calling this function***</td>
        /// <td class="pct"></td>
        /// <td class="long"></td>
        /// <td class="xp"></td>
        /// <td class="pts"></td>
        /// </summary>
        /// <param name="statsNode"></param>
        /// <returns>WR Fantasy Points</returns>
        private double handleKickingStats(HtmlNode statsNode)
        {
            int kickingPoints = 0;

            foreach (var node in statsNode.ChildNodes)
            {
                string stat = node.Attributes[0].Value;

                // this will be in the format of "made/attempts", so we need to parse out the made number
                if (stat.Equals("xp"))
                {
                    string xpStat = node.InnerText;
                    // find the index of the "/" and get the number before that location
                    int slashIndex = xpStat.IndexOf("/");
                    kickingPoints = int.Parse(xpStat.Substring(0, slashIndex));
                }
            }

            return kickingPoints;
        }
    }
}