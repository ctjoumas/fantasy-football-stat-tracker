namespace FantasyFootballStatTracker.Infrastructure
{
    using System;
    using System.IO;
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

        // The play by play JSON object for a given game
        private JObject _playByPlayJsonObject;

        // The play by play parser, which will either be using the JSON in the play by play if it exists, or the HTML doc
        //private IPlayByPlayParser playByPlayParser;

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
        /// Stores whether the game has been canceled and will update the CurrentRoster table just as the GameEnded flag does.
        /// </summary>
        public bool GameCanceled { get; set; } = false;

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

            _playByPlayJsonObject = GetPlayByPlayJsonObject();

            // Setup the play by play parser, which will be a JSON or HTML parser depending on if the JSON exists
            //IPlayByPlayParserFactory playByPlayParserFactory = new PlayByPlayParserFactory();
            //playByPlayParser = playByPlayParserFactory.GetPlayByPlayParser(playByPlayJsonObject, playByPlayDoc);
        }

        /// <summary>
        /// When a game is in progress, the play by play data is updated in the __espnfitt__ variable. This method will
        /// pull out the JSON and store it so we can parse the live play by play data. Starting in December 2022, ESPN
        /// has kept this JSON even post-game, whereas before, the JSON would be deleted.
        /// </summary>
        /// <returns></returns>
        public JObject GetPlayByPlayJsonObject()
        {
            JObject playByPlayJsonObject = null;

            var playByPlayJavaScriptNodes = playByPlayDoc.DocumentNode.SelectNodes("//script[@type='text/javascript']");

            foreach (var scriptNode in playByPlayJavaScriptNodes)
            {
                // the script will have:
                // window['__espnfitt__'] = { "app": {.... <all json> }
                if (scriptNode.InnerText.Contains("window['__espnfitt__']"))
                {
                    string content = scriptNode.InnerText.Trim();
                    int equalIndex = content.IndexOf("=");

                    // there is a trailing ;, so pull that off
                    string jsonContent = content.Substring(equalIndex + 1, content.Length - (equalIndex + 2));

                    playByPlayJsonObject = JObject.Parse(jsonContent);

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
            string timeRemaining = "";

            // get all of the plays, but only look at the last play and check the game clock from there
            // this will be in the format of "12:54 - 2nd"
            JToken timeRemainingToken = _playByPlayJsonObject.SelectToken("page.content.gamepackage.gmStrp.status.det");

            if (timeRemainingToken != null)
            {
                string strTimeRemaining = ((JValue)timeRemainingToken).Value.ToString();

                if (strTimeRemaining.ToLower().Equals("final") || strTimeRemaining.ToLower().Equals("final/ot"))
                {
                    timeRemaining = "Final";
                }
                else if (strTimeRemaining.ToLower().Equals("halftime"))
                {
                    timeRemaining = "Half";
                }
                else if (strTimeRemaining.ToLower().Equals("postponed"))
                {
                    timeRemaining = "Postponed";
                }
                else if (strTimeRemaining.ToLower().Equals("canceled"))
                {
                    timeRemaining = "Canceled";
                }
                else
                {
                    int indexOfDash = strTimeRemaining.IndexOf("-");
                    string clock = strTimeRemaining.Substring(0, indexOfDash - 1);
                    string quarter = strTimeRemaining.Substring(indexOfDash + 2);

                    timeRemaining = quarter + " " + clock;
                }
            }

            return timeRemaining;
        }

        /// <summary>
        /// Parses the current score for a given player. We don't care about the player name, we just need to know if
        /// the player is home or away so we can list that score first.
        /// </summary>
        /// <param name="teamAbbreviation">Abbreviation of this player's team</param>
        /// <returns>A score in the format of "20-17", with the first number being the home or away score.</returns>
        public string parseCurrentScore(string teamAbbreviation)
        {
            // flag indicating whether this player's team is home or away
            bool isPlayersTeamHome = false;

            // get the teams array where we can get the score and see which team is home and away
            JToken teamsArray = (JArray)_playByPlayJsonObject.SelectToken("page.content.gamepackage.gmStrp.tms");

            string homeScore = "";
            string awayScore = "";
            string scoreString = "";

            foreach (var team in teamsArray)
            {
                string teamAbbreviationInNode = ((JValue)team.SelectToken("isHome")).Value.ToString();

                bool isHome = (bool)((JValue)team.SelectToken("isHome")).Value;

                if (isHome)
                {
                    if (teamAbbreviationInNode.ToLower().Equals(teamAbbreviation.ToLower()))
                    {
                        isPlayersTeamHome = true;
                    }

                    homeScore = ((JValue)team.SelectToken("score")).Value.ToString();
                }
                else
                {
                    awayScore = ((JValue)team.SelectToken("score")).Value.ToString();
                }
            }



            if (isPlayersTeamHome)
            {
                scoreString = homeScore + "-" + awayScore;
            }
            else
            {
                scoreString = awayScore + "-" + homeScore;
            }

            return scoreString;
        }

        /// <summary>
        /// Gets the final score string (such as "(W) 45 - 30") and store this in the database
        /// </summary>
        /// <param name="teamAbbreviation"></param>
        /// <returns></returns>
        public string parseFinalScore(string teamAbbreviation)
        {
            string finalScoreString = "";

            // get the teams array which has the scores and team data (name, abbrev, score, etc)
            JArray teamsData = (JArray)_playByPlayJsonObject.SelectToken("page.content.gamepackage.gmStrp.tms");

            int playerTeamScore = 0;
            int opponentTeamScore = 0;

            // there are only two elements in the array and we will be checking each for the player teams score and the opponent teams score
            foreach (var teamData in teamsData)
            {
                // if this is the data for this players team, save their team's score
                if (teamData["abbrev"].ToString().ToLower().Equals(teamAbbreviation))
                {
                    playerTeamScore = int.Parse(teamData["score"].ToString());
                }
                else
                {
                    // pull out the opponent's score
                    opponentTeamScore = int.Parse(teamData["score"].ToString());
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
            double fantasyPoints = 0;

            // each play token is a drive, so we will go through this to parse all player stats; allPlys is a group of drives
            JToken driveTokens = _playByPlayJsonObject.SelectToken("page.content.gamepackage.allPlys");

            // if the game started and there are no drives yet
            if (driveTokens != null)
            {
                // the two-point conversion may be using the abbreviated players name, so we need to check both full and abbreviated names
                int spaceIndex = playerName.IndexOf(" ");
                string abbreviatedPlayerName = playerName.Substring(0, 1) + "." + playerName.Substring(spaceIndex + 1);

                foreach (JToken driveToken in driveTokens)
                {
                    JToken driveResultValue = driveToken.SelectToken("headline");

                    if (driveResultValue != null)
                    {
                        string driveResult = ((JValue)driveResultValue).Value.ToString();

                        // check to see if there is a touchdown on this drive, otherwise, we don't need to parse this
                        if (driveResult.ToLower().Contains(("touchdown")))
                        {
                            // Now go through each play in this drive to find the two-point conversion and see if it didn't fail.
                            // We are going through each play because if there is a penalty, it looks like that would be the last play
                            // and not the 2 point conversion
                            JToken playTokens = driveToken.SelectToken("plays");

                            foreach (JToken playToken in playTokens)
                            {
                                string play = (string)((JValue)playToken.SelectToken("description")).Value;

                                if (play.ToLower().Contains("two-point") &&
                                    !play.ToLower().Contains("fails") && !play.ToLower().Contains("failed") &&
                                    (play.ToLower().Contains(playerName.ToLower()) || play.ToLower().Contains(abbreviatedPlayerName.ToLower())))
                                {
                                    // We need one more check to ensure this player actually made the play. This will need more testing as there are several
                                    // ways this is shown in the play by play:
                                    // (12:42 - 4th) Stefon Diggs Pass From Josh Allen for 9 Yrds TWO-POINT CONVERSION ATTEMPT. J.Allen rushes right end. ATTEMPT SUCCEEDS.
                                    // (10:34 - 4th) James Robinson 1 Yard Rush (Pass formation) TWO-POINT CONVERSION ATTEMPT. T.Lawrence pass to D.Arnold is complete. ATTEMPT SUCCEEDS.
                                    // (0:37 - 2nd) Nahshon Wright 0 Yd Return of Blocked Punt (Ezekiel Elliott Run for Two-Point Conversion)
                                    // In the first two examples, the play before "TWO-POINT CONVERSION ATTEMPT" is the TD play and what follows is the two-point conversion;
                                    // in the last example, the two-point conversion play is in parentheses. It seems safe to check both conditions

                                    string twoPointConversionText;
                                    int index;

                                    // check first condition
                                    if (play.ToLower().Contains("two-point conversion attempt"))
                                    {
                                        // cut off the scoring play and just check the two point conversion text remaining
                                        index = play.ToLower().IndexOf("two-point conversion attempt");
                                        twoPointConversionText = play.Substring(index);
                                    }
                                    else
                                    {
                                        // cut off the scoring play, which is before the parentheses
                                        index = play.IndexOf("(");
                                        twoPointConversionText = play.Substring(index);
                                    }

                                    // now we can check the text containing only the two point conversion play to see if this player was involved
                                    if (twoPointConversionText.ToLower().Contains(playerName.ToLower()) ||
                                        twoPointConversionText.ToLower().Contains(abbreviatedPlayerName.ToLower()))
                                    {
                                        fantasyPoints += 2;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return fantasyPoints;
        }

        /// <summary>
        /// In the event that the offense fumbles the ball and a player on the offense, such as a wide receiver,
        /// recovers the fumble and scores a touchdown, we need to parse the play by play json for this.
        /// </summary>
        /// <param name="playerName"></param>
        /// <returns></returns>
        public int parseOffensivePlayerFumbleRecoveryForTouchdown(string playerName)
        {
            int fantasyPoints = 0;

            // we need to restructure this so it's T.Hill for Tyreek Hill, which is how it's stored in the JSON
            int spaceIndex = playerName.IndexOf(" ");
            string abbreviatedPlayerName = playerName.Substring(0, 1) + "." + playerName.Substring(spaceIndex + 1);

            // each play token is a drive, so we will go through this to parse all player stats; allPlys is a group of drives
            JToken driveTokens = _playByPlayJsonObject.SelectToken("page.content.gamepackage.allPlys");

            // if the game started and there are no drives yet
            if (driveTokens != null)
            {
                foreach (JToken driveToken in driveTokens)
                {
                    // if parsing field goals, we can check to see if there is a FG made in this drive, otherwise, we don't need to parse this
                    JToken driveResultValue = driveToken.SelectToken("headline");

                    if (driveResultValue != null)
                    {
                        string driveResult = ((JValue)driveResultValue).Value.ToString();

                        // only parse the plays in this drive if this drive resulted in a TD
                        if (driveResult.ToLower().Equals(("touchdown")))
                        {
                            JToken playTokens = driveToken.SelectToken("plays");

                            // loop through all plays in the drive to find the fumble recovery for a touchdown
                            foreach (JToken playToken in playTokens)
                            {
                                // the fumble recovery for a touchdown play will have the following format:
                                // "(8:45 - 2nd) (Shotgun) J.Wilson up the middle to MIA 47 for 6 yards (A.Gilman). FUMBLES (A.Gilman), touched at MIA 44, recovered by MIA-T.Hill at MIA 43. T.Hill for 57 yards, TOUCHDOWN.J.Sanders extra point is GOOD, Center-B.Ferguson, Holder-T.Morstead."
                                JToken playDescription = playToken["description"];

                                if (playDescription != null)
                                {
                                    string playText = ((JValue)playDescription).Value.ToString();

                                    // The play text should have "fumbles", "recovered", and "touchdown" in it. If so, we can then check for the players name
                                    if (playText.ToLower().Contains("fumbles") &&
                                        playText.ToLower().Contains("recovered") &&
                                        playText.ToLower().Contains("touchdown"))
                                    {
                                        // we have the fumble recovery for touchdown play, so now let's see if the players name is in this play
                                        // and, if so, it appears after the last occurence of "recovered", just in case there were multiple fumbles
                                        // in the drive before the last player who touched it scored (rare case, but may happen)
                                        int indexOfLastRecoveredWord = playText.LastIndexOf("recovered");
                                        int indexOfLastAbbreviatedPlayerName = playText.LastIndexOf(abbreviatedPlayerName);
                                        int indexOfLastFullPlayerName = playText.LastIndexOf(playerName);

                                        // everything looks to be in the right place for this player to have scored the fumble recover,
                                        // so award them 6 points for the TD
                                        if ((indexOfLastAbbreviatedPlayerName > indexOfLastRecoveredWord) ||
                                            (indexOfLastFullPlayerName > indexOfLastRecoveredWord))
                                        {
                                            fantasyPoints += 6;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

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
            int fieldGoalPoints = 0;

            // we need to restructure this so it's C.Boswell for Chris Boswell, which is how it's stored in the JSON
            int spaceIndex = playerName.IndexOf(" ");
            string abbreviatedPlayerName = playerName.Substring(0, 1) + "." + playerName.Substring(spaceIndex + 1);

            // each play token is a drive, so we will go through this to parse all player stats; allPlys is a group of drives
            JToken driveTokens = _playByPlayJsonObject.SelectToken("page.content.gamepackage.allPlys");

            // if the game started and there are no drives yet
            if (driveTokens != null)
            {
                foreach (JToken driveToken in driveTokens)
                {
                    // if parsing field goals, we can check to see if there is a FG made in this drive, otherwise, we don't need to parse this
                    JToken driveResultValue = driveToken.SelectToken("headline");

                    if (driveResultValue != null)
                    {
                        string driveResult = ((JValue)driveResultValue).Value.ToString();

                        // only parse the plays in this drive if this drive resulted in a made FG
                        if (driveResult.ToLower().Equals(("field goal")))
                        {
                            JToken playTokens = driveToken.SelectToken("plays");

                            // there could be a penalty or a timeout after the kick, so the FG may not be the last play token so we
                            // need to loop through all plays
                            foreach (JToken playToken in playTokens)
                            {
                                // if this is the field goal token, the description will have the word "field goal" in it; we'll assume if there was
                                // a penalty in any field goal attempt, it is in a later token, so we should be ok just checking for "field goal" since
                                // a successful field goal will be the first play we encounter
                                JToken playDescription = playToken["description"];
                                
                                if (playDescription != null)
                                {
                                    string playText = ((JValue)playDescription).Value.ToString();
                                    
                                    if (playText.ToLower().Contains("field goal"))
                                    {
                                        // if this play has the player name, check the distance of the kick
                                        // this will be in the format of: "(4:25) C.Boswell 20 yard field goal is GOOD, Center-C.Kuntz, Holder-P.Harvin."
                                        if (playText.ToLower().Contains(abbreviatedPlayerName.ToLower()) ||
                                            playText.ToLower().Contains(playerName.ToLower()))
                                        {
                                            int indexOfSpaceAfterPlayerName;
                                            int playerNameIndex = playText.IndexOf(abbreviatedPlayerName);

                                            // if the abbreviated player name isn't found, we need to check for the full player name
                                            if (playerNameIndex == -1)
                                            {
                                                playerNameIndex = playText.IndexOf(playerName.ToLower());
                                                indexOfSpaceAfterPlayerName = playText.IndexOf(" ", playerNameIndex + playerName.Length);
                                            }
                                            else
                                            {
                                                indexOfSpaceAfterPlayerName = playText.IndexOf(" ", playerNameIndex + abbreviatedPlayerName.Length);
                                            }

                                            int indexOfSpaceAfterFgDistance = playText.IndexOf(" ", indexOfSpaceAfterPlayerName + 1);
                                            int fgDistance = int.Parse(playText.Substring(indexOfSpaceAfterPlayerName, (indexOfSpaceAfterFgDistance - indexOfSpaceAfterPlayerName)));

                                            if (fgDistance < 40)
                                                fieldGoalPoints += 3;
                                            else if (fgDistance < 50)
                                                fieldGoalPoints += 4;
                                            else if (fgDistance >= 50)
                                                fieldGoalPoints += 5;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

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
        public double parseGameTrackerPage(string gameId, string playerId, Position position, /*string homeOrAway, */string teamName, string teamAbbreviation, string opponentAbbreviation)
        {
            double fantasyPoints = 0;

            // Check if the game has ended - get the status description which will be Final if the game has ended
            JToken statusToken = _playByPlayJsonObject.SelectToken("page.content.gamepackage.gmStrp.status.desc");
            
            string txtStatusToken = ((JValue)statusToken).Value.ToString();
            
            if (txtStatusToken.ToLower().Equals("final"))
            {
                GameEnded = true;
            }
            else if (txtStatusToken.ToLower().Equals("canceled"))
            {
                GameCanceled = true;
            }

            // Keeps track of the points allowed for a defense
            int defensePointsAllowed = 0;

            // get all of the div nodes which contain the various team stats for the given player
            // This has been updated to be under the <div class="Boxscore__Team"> nodes and is the same node for all teams so we need
            // to pull all nodes and check to make sure we are looking under the correct team's nodes for each stat (i.e. Las Vegas Passing).
            // The way to check the nodes is if under the main boscore category node, there is a img source such as <img alt="Raiders"...>
            var boxscoreTeamNodes = gameTrackerDoc.DocumentNode.SelectNodes("//div[@class='Boxscore__Team']");
            if (boxscoreTeamNodes != null)
            {
                foreach (var boxscoreTeamNode in boxscoreTeamNodes)
                {
                    // check the team name to see if this is the team this player is on so we know whether we should parse this or
                    // move to the next one. Our teamName from the databse is the full team name (Minnesota Vikings) while the teamNameInNode is the last part (Vikings),
                    // so we need to take a substring to check
                    // the team name is in the img node: <img alt="Raiders" class="Image Logo pr3 Logo__sm"
                    string teamNameInNode = boxscoreTeamNode.SelectSingleNode(".//img[@class='Image Logo pr3 Logo__sm']").Attributes["alt"].Value;

                    string partialTeamName = teamName.Substring(teamName.LastIndexOf(" ") + 1);

                    // now we need to select the <div class="TeamTitle__Name">Las Vegas Passing</div> node which has the <team> <stat> text
                    // such as "Baltimore Passing" 
                    var teamNameNode = boxscoreTeamNode.SelectSingleNode(".//div[@class='TeamTitle__Name']");

                    // find the last space in the team name so we can grab the stat
                    // e.g. - "Los Angeles Passing" or "Carolina Passing" should return "Passing"
                    string teamNameText = teamNameNode.InnerText;
                    int lastSpaceIndex = teamNameText.LastIndexOf(" ");
                    string stat = teamNameText.Substring(lastSpaceIndex + 1);

                    if (teamNameInNode.ToLower().Equals(partialTeamName.ToLower()))
                    {
                        // if we are parsing the defensive stats ("Defensive" for sacks, "Interceptions" for interceptions, or "Returns" for kick/punt
                        // returns, we can just check the last row which is the team total so we don't have to loop through each player on the team
                        if (position.Equals(Position.DEF))
                        {
                            if (stat.Equals("Defensive"))
                            {
                                var defensiveTeamStatsTotalNode = boxscoreTeamNode.SelectNodes(".//tr[@class='Boxscore__Totals Table__TR Table__TR--sm Table__even']")[1];
                                fantasyPoints += handleDefensiveStats(defensiveTeamStatsTotalNode);
                            }
                            else if (stat.Equals("Interceptions"))
                            {
                                var interceptionsStatsTotalNodes = boxscoreTeamNode.SelectNodes(".//tr[@class='Boxscore__Totals Table__TR Table__TR--sm Table__even']");

                                // if there are no interceptions, this node will be null
                                if (interceptionsStatsTotalNodes != null)
                                {
                                    var interceptionsStatsTotalNode = interceptionsStatsTotalNodes[1];
                                    fantasyPoints += handleInterceptionStats(interceptionsStatsTotalNode);
                                }
                            }
                        }
                        else
                        {
                            // The new HTML has a table with the player names, then a separate table with the stats. We need to first get
                            // the player nodes to find out which index the player we are looking for is in, then use that to pull out
                            // the player stats in the stats table
                            if (stat.Equals("Passing") || stat.Equals("Rushing") || stat.Equals("Receiving") || stat.Equals("Fumbles") || stat.Equals("Kicking"))
                            {
                                // this pulls out all of the players so we can find where our player is
                                var playerNodes = boxscoreTeamNode.SelectNodes(".//table[@class='Table Table--align-right Table--fixed Table--fixed-left']/tbody/tr[@class='Table__TR Table__TR--sm Table__even']");

                                // if we are parsing a node that doesn't have any stats, such as fumbles, this node can be null
                                if (playerNodes != null)
                                {
                                    // there is actually a data-idx attribute in each passingStatNode tr which we can probably use, but since this may not be
                                    // exactly what it seems to reprsent (the player index for the stats table later i the HTML), we'll build our own index
                                    int playerNodeIndex = 0;

                                    foreach (var playerNode in playerNodes)
                                    {
                                        // get the <a..> node which has the link to the player page, which has the player ID in the link
                                        HtmlNode playerLinkNode = playerNode.SelectSingleNode(".//td/div/a");

                                        // the link has the format of http://www.espn.com/nfl/player/_/id/14880/kirk-cousins
                                        string playerLink = playerLinkNode.Attributes["href"].Value;

                                        int lastSlashIndex = playerLink.LastIndexOf("/");

                                        // cut off the player name and then get the last slash again so we can get the player id
                                        playerLink = playerLink.Substring(0, lastSlashIndex);

                                        lastSlashIndex = playerLink.LastIndexOf("/");

                                        string nodePlayerId = playerLink.Substring(lastSlashIndex + 1);

                                        // we found the node for this player, so get the stats nodes and pull out the node for this player,
                                        // based on the index we have, and then break out of this loop
                                        if (nodePlayerId.Equals(playerId))
                                        {
                                            var statNodes = boxscoreTeamNode.SelectNodes(".//table[@class='Table Table--align-right']/tbody/tr[@class='Table__TR Table__TR--sm Table__even']");

                                            // just pull out the node at the proper index which maps to the player node index in the header table we found above
                                            HtmlNode playerStatNode = statNodes[playerNodeIndex];

                                            if (stat.Equals("Passing"))
                                            {
                                                fantasyPoints += handlePassingStats(playerStatNode);
                                            }
                                            else if (stat.Equals("Rushing"))
                                            {
                                                fantasyPoints += handleRushingStats(playerStatNode);
                                            }
                                            else if (stat.Equals("Receiving"))
                                            {
                                                fantasyPoints += handleReceivingStats(playerStatNode);
                                            }
                                            else if (stat.Equals("Fumbles"))
                                            {
                                                fantasyPoints += handleFumbleStats(playerStatNode);
                                            }
                                            else if (stat.Equals("Kicking"))
                                            {
                                                fantasyPoints += handleKickingStats(playerStatNode);
                                            }

                                            break;
                                        }

                                        playerNodeIndex++;
                                    }
                                }
                            }
                        }
                    }
                    // otherwise, if this player is a DEF and the section we are looking at is the opponent, let's check for any points the
                    // other team scored against this defense
                    else if (position.Equals(Position.DEF))
                    {
                        var statsNodes = boxscoreTeamNode.SelectNodes(".//tr[@class='Boxscore__Totals Table__TR Table__TR--sm Table__even']");

                        // if there are no returns, this node will be null
                        if (statsNodes != null)
                        {
                            var statsNode = statsNodes[1];

                            if (stat.Equals("Passing") || stat.Equals("Rushing"))
                            {
                                defensePointsAllowed += handleDefensePointsAllowedFromPassingOrRushing(statsNode);
                            }
                            else if (stat.Equals("Kicking"))
                            {
                                defensePointsAllowed += handleDefensePointsAllowedFromKicking(statsNode);
                            }
                            else if (stat.Equals("Fumbles"))
                            {
                                // based on a new change in the HTML, there are no longer attributes to show which stat the node corresponds to, so we'll need to map it such that
                                // column 0: FUM
                                // column 1: LOST
                                // column 2: REC
                                fantasyPoints += handleDefensePointsForFumbles(statsNode);
                            }
                        }                            
                    }
                }
            }

            // if we are scoring for team defense, we need to check how many points were scored
            // against them as well as whether they blocked any kicks or field goals
            if (position.Equals(Position.DEF))
            {
                defensePointsAllowed += handleDefenseTeamPointsWithTwoPointConversions(teamName);
                fantasyPoints += handleBlockedKicksAndPunts(teamName);
                fantasyPoints += handleSafeties(teamName);

                if (defensePointsAllowed == 0)
                    fantasyPoints += 10;
                else if ((defensePointsAllowed >= 1) && (defensePointsAllowed <= 6))
                    fantasyPoints += 7;
                else if ((defensePointsAllowed >= 7) && (defensePointsAllowed <= 13))
                    fantasyPoints += 4;
                else if ((defensePointsAllowed >= 14) && (defensePointsAllowed <= 20))
                    fantasyPoints += 1;
                else if ((defensePointsAllowed >= 21) && (defensePointsAllowed <= 27))
                    fantasyPoints += 0;
                else if ((defensePointsAllowed >= 28) && (defensePointsAllowed <= 34))
                    fantasyPoints += -1;
                else if (defensePointsAllowed >= 35)
                    fantasyPoints += -4;
            }

            return Math.Round(fantasyPoints, 2);
        }

        /// <summary>
        /// This is used for points allowed from a defense for passing or rushing scores. The format is as follows:
        /// column 0: C/Att
        /// column 1: YDS
        /// column 2: AVG
        /// column 3: TD
        /// column 4: INT
        /// column 5: SACKS
        /// column 6: QBR
        /// column 7: RTG
        /// </summary>
        /// <returns></returns>
        private int handleDefensePointsAllowedFromPassingOrRushing(HtmlNode statsNode)
        {
            // Both rushing and passing have the TD in column 3, so this will work for both
            int pointsAllowed = int.Parse(statsNode.ChildNodes[3].InnerText) * 6;

            return pointsAllowed;
        }

        /// <summary>
        /// This is used for points allowed from a defense for kicking. The format is as follows:
        /// // column 0: FG
        /// column 1: PCT
        /// column 2: LONG
        /// column 3: XP
        /// column 4: PTS
        /// </summary>
        /// <param name="statsNode"></param>
        /// <returns></returns>
        private int handleDefensePointsAllowedFromKicking(HtmlNode statsNode)
        {
            // We only need the PTS column, which will give the total points scored in FGs and XPs
            int pointsAllowed = int.Parse(statsNode.ChildNodes[4].InnerText);

            return pointsAllowed;
        }

        /// <summary>
        /// Proceses points for a defense if they have recovered a fumble. THe data is in the following format:
        /// column 0: FUM
        /// column 1: LOST
        /// column 2: REC
        /// </summary>
        /// <param name="statsNode"></param>
        /// <returns></returns>
        private int handleDefensePointsForFumbles(HtmlNode statsNode)
        {
            int defensivePoints = int.Parse(statsNode.ChildNodes[1].InnerText) * 2;

            return defensivePoints;
        }

        /// <summary>
        /// Once we parse all data points, we need to then check and see how many offensive
        /// points the opponent scored on the defense (we will not count any defensive scoring) and
        /// adjust the points awarded to the defensive team, which will then be added to all sacks, ints,
        /// and return touchdowns they scored.
        /// </summary>
        /// <returns></returns>
        private int handleDefenseTeamPoints(string stat, HtmlNode statsNode, string opponentAbbreviation)
        {
            int defensivePoints = 0;
            int pointsAllowed = 0;

            // we are only looking at touchdowns (we could look for passing or receiving; but not both so we don't double count TDs)
            if (stat.Equals("Passing") || stat.Equals("Rushing"))
            {
                // Both passing and rushing have the TD in column 3, so this will work for both
                // column 0: C/Att
                // column 1: YDS
                // column 2: AVG
                // column 3: TD
                // column 4: INT
                // column 5: SACKS
                // column 6: QBR
                // column 7: RTG
                pointsAllowed += int.Parse(statsNode.ChildNodes[3].InnerText) * 6;
            }
            // we need to see how many fumbles the opponent lost; we'll add these to the defensive points
            else if (stat.Equals("Fumbles"))
            {
                // based on a new change in the HTML, there are no longer attributes to show which stat the node corresponds to, so we'll need to map it such that
                // column 0: FUM
                // column 1: LOST
                // column 2: REC
                defensivePoints += int.Parse(statsNode.ChildNodes[1].InnerText) * 2;
            }
            else if (stat.Equals("Kicking"))
            {
                // based on a new change in the HTML, there are no longer attributes to show which stat the node corresponds to, so we'll need to map it such that
                // column 0: FG
                // column 1: PCT
                // column 2: LONG
                // column 3: XP
                // column 4: PTS

                // We only need the PTS column, which will give the total points scored in FGs and XPs
                pointsAllowed += int.Parse(statsNode.ChildNodes[4].InnerText);
            }

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

            return defensivePoints;
        }

        /// <summary>
        /// This method will check to see if the JSON should be used to parse the game or HTML. When a game ends, at some point
        /// the JSON is removed and all data exists in the HTML; this would only happen if the scoreboard is not viewed until
        /// after a game ends, so this is here just in case that edge case happens.
        /// </summary>
        /// <returns></returns>
        public int handleSafeties(string teamName)
        {
            int safetyPoints = 0;

            // each play token is a drive, so we will go through this to parse all player stats; allPlys is a group of drives
            JToken driveTokens = _playByPlayJsonObject.SelectToken("page.content.gamepackage.allPlys");

            // if the game started and there are no drives yet
            if (driveTokens != null)
            {
                foreach (JToken driveToken in driveTokens)
                {
                    JToken driveResultValue = driveToken.SelectToken("headline");

                    if (driveResultValue != null)
                    {
                        // if parsing a safety, we can check to see if there is a safety in this drive, otherwise, we don't need to parse this
                        string driveResult = ((JValue)driveResultValue).Value.ToString();

                        // only parse the plays in this drive if this drive resulted in a safety
                        // TODO: guessing that this is what the drive result would say; have not been able to see this yet during live game
                        if (driveResult.ToLower().Contains(("safety")))
                        {
                            // get the team name who had the ball during this drive
                            string driveTeamName = (string)((JValue)driveToken.SelectToken("teamName")).Value;

                            // if the team name is not the same as the defense we are scoring, that means they blocked the punt, so give them 2 points
                            if (!driveTeamName.ToLower().Equals(teamName.ToLower()))
                            {
                                safetyPoints += 2;
                            }
                        }
                    }
                }
            }

            return safetyPoints;
        }

        /// <summary>
        /// This method will check to see if the JSON should be used to parse the game or HTML. When a game ends, at some point
        /// the JSON is removed and all data exists in the HTML; this would only happen if the scoreboard is not viewed until
        /// after a game ends, so this is here just in case that edge case happens.
        /// </summary>
        /// <returns></returns>
        public int handleBlockedKicksAndPunts(string teamName)
        {
            int blockedPoints = 0;

            // each play token is a drive, so we will go through this to parse all player stats; allPlys is a group of drives
            JToken driveTokens = _playByPlayJsonObject.SelectToken("page.content.gamepackage.allPlys");

            // if the game started and there are no drives yet
            if (driveTokens != null)
            {
                foreach (JToken driveToken in driveTokens)
                {
                    JToken driveResultValue = driveToken.SelectToken("headline");

                    if (driveResultValue != null)
                    {
                        // if parsing blocked punts and kicks, we can check to see if there is a block in this drive, otherwise, we don't need to parse this
                        string driveResult = ((JValue)driveResultValue).Value.ToString();

                        // only parse the plays in this drive if this drive resulted in a made FG
                        if (driveResult.ToLower().Contains(("blocked")))
                        {
                            // get the team name who had the ball during this drive
                            string driveTeamName = (string)((JValue)driveToken.SelectToken("teamName")).Value;

                            // if the team name is not the same as the defense we are scoring, that means they blocked the punt, so give them 2 points
                            if (!driveTeamName.ToLower().Equals(teamName.ToLower()))
                            {
                                blockedPoints += 2;
                            }
                        }
                    }
                }
            }

            return blockedPoints;
        }

        /// <summary>
        /// This method will check to see if the JSON should be used to parse the game or HTML. When a game ends, at some point
        /// the JSON is removed and all data exists in the HTML; this would only happen if the scoreboard is not viewed until
        /// after a game ends, so this is here just in case that edge case happens.
        /// </summary>
        /// <param name="opponentAbbreviation">This defenses opponents team abbreviation</param>
        /// <returns></returns>
        public int handleDefenseTeamPointsWithTwoPointConversions(string teamName)
        {
            int twoPointConversionPoints = 0;

            // each play token is a drive, so we will go through this to parse all player stats; allPlys is a group of drives
            JToken driveTokens = _playByPlayJsonObject.SelectToken("page.content.gamepackage.allPlys");

            // if the game started and there are no drives yet
            if (driveTokens != null)
            {
                foreach (JToken driveToken in driveTokens)
                {
                    // "headline" will be "Touchdown" if this is a scoring drive
                    JToken driveResultValue = driveToken.SelectToken("headline");

                    if (driveResultValue != null)
                    {
                        string driveResult = ((JValue)driveResultValue).Value.ToString();

                        // check to see if there is a touchdown on this drive, otherwise, we don't need to parse this
                        if (driveResult.ToLower().Equals(("touchdown")))
                        {
                            // get the team name who had the ball during this drive
                            string driveTeamName = (string)((JValue)driveToken.SelectToken("teamName")).Value;

                            // if the team name is not the same as the defense we are scoring, that means the other team scored a 2 point conversion, so add 2 points
                            // if there is a 2 point conversion
                            if (!driveTeamName.ToLower().Equals(teamName.ToLower()))
                            {
                                // Now go through each play in this drive to find the two-point conversion and see if it didn't fail.
                                // We are going through each play because if there is a penalty, it looks like that would be the last play
                                // and not the 2 point conversion
                                JToken playTokens = driveToken.SelectToken("plays");

                                foreach (JToken playToken in playTokens)
                                {
                                    string play = (string)((JValue)playToken.SelectToken("description")).Value;

                                    if (play.ToLower().Contains("two-point") &&
                                       !play.ToLower().Contains("fails") && !play.ToLower().Contains("failed"))
                                    {
                                        twoPointConversionPoints += 2;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return twoPointConversionPoints;
        }

        /// <summary>
        /// This will loop through all interception stats for each player who has interceptions for the given team. Based on a new change
        /// in the HTML, there are no longer attributes to show which stat the node corresponds to, so we'll need to map it such that
        /// column 0: total team interceptions
        /// column 1: total team interception yards
        /// column 2: total team interception TDs - we will not check for TDs since these are counted in the main Defensive stats table
        /// </summary>
        /// <param name="defensiveTeamStatsTotalNode"></param>
        /// <returns></returns>
        private int handleInterceptionStats(HtmlNode defensiveTeamStatsTotalNode)
        {
            int defensivePointsFromInterceptions = 0;

            int interceptions = 0;

            // we will only check for int and not TD's since interceptions returned for TD's are accounted for in the
            // main Defensive stats table
            interceptions += int.Parse(defensiveTeamStatsTotalNode.ChildNodes[0].InnerText);
            
            defensivePointsFromInterceptions += (interceptions * DEFENSIVE_INT_POINTS);

            return defensivePointsFromInterceptions;
        }

        /// <summary>
        /// This will loop through all defensive stats for the team. We are only interested in
        /// sacks and touchdowns, which will be in the format:
        /// column 0: total team tackles
        /// column 1: total team solo tackles
        /// column 2: total team sacks
        /// column 3: total tackles for loss
        /// column 4: total PDs (?)
        /// column 5: total QB hits
        /// column 6: total TDs - we will count the TDs here and not in the other tables (such as Interceptions or Fumbles) since the TDs will show up in each table
        /// </summary>
        /// <param name="defensiveTeamStatsTotalNode"></param>
        /// <returns></returns>
        private double handleDefensiveStats(HtmlNode defensiveTeamStatsTotalNode)
        {
            double defensivePoints = 0;

            double sacks = 0;
            int touchdowns = 0;

            sacks += (double)Convert.ToDouble(defensiveTeamStatsTotalNode.ChildNodes[2].InnerText);
            // this will count TDs made on INT and fumble returns, so we only want to calculate the TDs here and not in the
            // other tables (such as Interceptions, where it will also show up)
            touchdowns += int.Parse(defensiveTeamStatsTotalNode.ChildNodes[6].InnerText);

            defensivePoints += (sacks * DEFENSIVE_SACK_POINTS) + (touchdowns * DEFENSIVE_TD_POINTS);

            return defensivePoints;
        }

        /// <summary>
        /// Parses the qb stats from the gametracker page. The stats for the passing are as follows:
        /// column 0: C/ATT
        /// column 1: YDS
        /// column 2: AVG
        /// column 3: TD
        /// column 4: INT
        /// column 5: SACKS
        /// column 6: QBR
        /// column 7: RTG
        /// </summary>
        /// <param name="statsNode"></param>
        /// <returns>QB Fantasy Points</returns>
        private double handlePassingStats(HtmlNode statsNode)
        {
            int passYards = int.Parse(statsNode.ChildNodes[1].InnerText);
            int passTds = int.Parse(statsNode.ChildNodes[3].InnerText);
            int passInts = int.Parse(statsNode.ChildNodes[4].InnerText);

            double fantasyPoints = (passYards / PASS_YARDS_PER_POINT) + (passTds * PASS_TD_POINTS) + (passInts * INTERCEPTION_POINTS);

            return fantasyPoints;

        }

        /// <summary>
        /// Parses the rb stats from the gametracker page. The stats for the passing are as follows:
        /// column 0: CAR
        /// column 1: YDS
        /// column 2: AVG
        /// column 3: TD
        /// column 4: LONG
        /// </summary>
        /// <param name="statsNode"></param>
        /// <returns>Rusing Fantasy Points</returns>
        private double handleRushingStats(HtmlNode statsNode)
        {
            int rushYards = int.Parse(statsNode.ChildNodes[1].InnerText);
            int rushTds = int.Parse(statsNode.ChildNodes[3].InnerText);

            double fantasyPoints = (rushYards / RUSH_YARDS_PER_POINT) + (rushTds * RUSH_TD_POINTS);

            return fantasyPoints;
        }

        /// <summary>
        /// Parses the wr stats from the gametracker page. The stats for the passing are as follows:
        /// column 0: REC
        /// column 1: YDS
        /// column 2: AVG
        /// column 3: TD
        /// column 4: LONG
        /// column 5: TGTS
        /// </summary>
        /// <param name="statsNode"></param>
        /// <returns>WR Fantasy Points</returns>
        private double handleReceivingStats(HtmlNode statsNode)
        {
            int receivingYards = int.Parse(statsNode.ChildNodes[1].InnerText);
            int receivingTds = int.Parse(statsNode.ChildNodes[3].InnerText);

            double fantasyPoints = (receivingYards / RECEIVING_YARDS_PER_POINT) + (receivingTds * REC_TD_POINTS);

            return fantasyPoints;
        }

        /// <summary>
        /// Parses the fumble stats from the gametracker page. The stats for the passing are as follows:
        /// column 0: FUM
        /// column 1: LOST
        /// column 2: REC
        /// </summary>
        /// <param name="statsNode"></param>
        /// <returns>WR Fantasy Points</returns>
        private double handleFumbleStats(HtmlNode statsNode)
        {
            int fumblesLost = int.Parse(statsNode.ChildNodes[1].InnerText);

            double fantasyPoints = fumblesLost * FUMBLE_LOST_POINTS;

            return fantasyPoints;
        }

        /// <summary>
        /// Parses the kicking stats from the gametracker page. We are only grabbing the XPs made because the gametracker
        /// doesn't show the distance of FGs, so we use the JSON to parse that separately. The stats for the passing are as follows:
        /// column 0: FG
        /// column 1: PCT
        /// column 2: LONG
        /// column 3: XP
        /// column 4: PTS
        /// </summary>
        /// <param name="statsNode"></param>
        /// <returns>K Fantasy Points</returns>
        private double handleKickingStats(HtmlNode statsNode)
        {
            int kickingPoints = 0;

            string xpStat = statsNode.ChildNodes[3].InnerText;

            // find the index of the "/" and get the number before that location
            int slashIndex = xpStat.IndexOf("/");
            kickingPoints = int.Parse(xpStat.Substring(0, slashIndex));

            return kickingPoints;
        }
    }
}