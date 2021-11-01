namespace YahooFantasyFootball.Infrastructure
{
    using System;
    using HtmlAgilityPack;
    using System.Linq;
    using System.Net;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;

    public class EspnHtmlScraper
    {
        // The gametracker doc for a specific game, which can be shared for all players in the same game, saving time from loading the doc
        private HtmlDocument gameTrackerDoc;

        // The play by play doc for a specific game, which can be shared for all players in the same game, saving time from loading the doc
        private HtmlDocument playByPlayDoc;

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
        }

        /// <summary>
        /// Parses the play by play page and finds all two point conversions which did not "fail". Of the ones
        /// which didn't fail, we will see if the player is part of this play (there is no player id associated
        /// with this page)
        /// </summary>
        /// <param name="gameId">Gmae ID of the game this player is playing in</param>
        /// <param name="playerName">The full name, first and last, of the player</param>
        /// <returns></returns>
        public double parseTwoPointConversionsForPlayer(string gameId, string playerName)
        {
            double fantasyPoints = 0;

            // we are looking for all <span class="post-play"> nodes which have "Two-Point" in the inner text such as:
            // <span class="post-play">
            //   (0:27 - 3rd) Tommy Sweeney Pass From Josh Allen for 1 Yard (Pass formation) TWO-POINT CONVERSION ATTEMPT. D.Knox pass to J.Allen is complete. ATTEMPT SUCCEEDS.
            // </span>
            var twoPointConversionNodes = playByPlayDoc.DocumentNode.Descendants("span")
                    .Where(node => node.InnerText.ToLower().Contains("two-point"));

            foreach (var twoPointConversionNode in twoPointConversionNodes)
            {
                // if the two-point conversion didn't fail, check if the player's name was invovled (pass
                // or reception, it's 2 points either way)
                if (!twoPointConversionNode.InnerText.ToLower().Contains("failed") && twoPointConversionNode.InnerText.ToLower().Contains(playerName.ToLower()))
                {
                    fantasyPoints += 2;
                }
            }

            // we are looking for all <span class="post-play"> nodes
            /*var postPlayNodes = doc.DocumentNode.SelectNodes("//span[@class='post-play']");

            // if the game hasn't started, there will be no data, so check for null
            if (postPlayNodes != null)
            {
                foreach (var postPlayNode in postPlayNodes)
                {
                    // search for an occurence of a two-point conversion
                    bool twoPointConversionOccurred = postPlayNode.InnerText.ToLower().Contains("two-point");

                    if (twoPointConversionOccurred)
                    {
                        // if the two-point conversion didn't fail, check if the player's name was invovled (pass
                        // or reception, it's 2 points either way)
                        if (!postPlayNode.InnerText.ToLower().Contains("failed") && postPlayNode.InnerText.ToLower().Contains(playerName.ToLower()))
                        {
                            fantasyPoints += 2;
                        }
                    }
                }
            }*/

            return fantasyPoints;
        }

        /// <summary>
        /// Calculate the field goal points, based on distance, for a kicker. This data is not on the gametracker page, but we are able to get the
        /// number of XPs from there which is calculated in the parseGameTrackerPage method.
        /// </summary>
        /// <param name="gameId"></param>
        /// <param name="playerName"></param>
        /// <returns>Total number of FG points (not counting XPs)</returns>
        public int parseFieldGoals(string gameId, string playerName)
        {
            int fieldGoalPoints = 0;

            // we are looking for all <span class="post-play"> nodes which have "Field Goal" in the inner text such as:
            // <span class="post-play">
            //   (6:07 - 1st) Tyler Bass 24 Yd Field Goal
            // </span>
            var fieldGoalNodes = playByPlayDoc.DocumentNode.Descendants("span")
                    .Where(node => node.InnerText.ToLower().Contains("field goal"));

            foreach (var fieldGoalNode in fieldGoalNodes)
            {
                // if the field goal was good, check if the player's name was invovled (pass
                // or reception, it's 2 points either way)
                if (!fieldGoalNode.InnerText.ToLower().Contains("no good") && fieldGoalNode.InnerText.ToLower().Contains(playerName.ToLower()))
                {
                    // this player successfully kicked a FG, so we need to parse out the length.
                    // it will be in this format: (5:02 - 3rd) Justin Tucker 39 Yd Field Goal,
                    // so we will look for the player name and grab the number between the next two spaces
                    int playerNameIndex = fieldGoalNode.InnerText.ToLower().IndexOf(playerName.ToLower());

                    int indexOfSpaceAfterPlayerName = fieldGoalNode.InnerText.IndexOf(" ", playerNameIndex + playerName.Length);
                    int indexOfSpaceAfterFgDisatance = fieldGoalNode.InnerText.IndexOf(" ", indexOfSpaceAfterPlayerName + 1);
                    int fgDistance = int.Parse(fieldGoalNode.InnerText.Substring(indexOfSpaceAfterPlayerName, (indexOfSpaceAfterFgDisatance - indexOfSpaceAfterPlayerName)));

                    if (fgDistance < 40)
                        fieldGoalPoints += 3;
                    else if (fgDistance < 50)
                        fieldGoalPoints += 4;
                    else if (fgDistance >= 50)
                        fieldGoalPoints += 5;
                }
            }

            // we are looking for all <span class="post-play"> nodes
            /*var postPlayNodes = doc.DocumentNode.SelectNodes("//span[@class='post-play']");

            // if the game hasn't started, there will be no data, so check for null
            if (postPlayNodes != null)
            {
                foreach (var postPlayNode in postPlayNodes)
                {
                    // search for an occurence of a two-point conversion
                    bool fieldGoalOccurred = postPlayNode.InnerText.ToLower().Contains("field goal");

                    if (fieldGoalOccurred)
                    {
                        // if the field goal occurred, it will list the length field goal; if it is no good, the words "No Good" will
                        // be present, so we need to check for that.
                        if (!postPlayNode.InnerText.ToLower().Contains("no good") && postPlayNode.InnerText.ToLower().Contains(playerName.ToLower()))
                        {
                            // this player successfully kicked a FG, so we need to parse out the length.
                            // it will be in this format: (5:02 - 3rd) Justin Tucker 39 Yd Field Goal,
                            // so we will look for the player name and grab the number between the next two spaces
                            int playerNameIndex = postPlayNode.InnerText.ToLower().IndexOf(playerName.ToLower());

                            int indexOfSpaceAfterPlayerName = postPlayNode.InnerText.IndexOf(" ", playerNameIndex + playerName.Length);
                            int indexOfSpaceAfterFgDisatance = postPlayNode.InnerText.IndexOf(" ", indexOfSpaceAfterPlayerName + 1);
                            int fgDistance = int.Parse(postPlayNode.InnerText.Substring(indexOfSpaceAfterPlayerName, (indexOfSpaceAfterFgDisatance - indexOfSpaceAfterPlayerName)));

                            if (fgDistance < 40)
                                fieldGoalPoints += 3;
                            else if (fgDistance < 50)
                                fieldGoalPoints += 4;
                            else if (fgDistance >= 50)
                                fieldGoalPoints += 5;
                        }
                    }
                }
            }*/

            return fieldGoalPoints;
        }

        /// <summary>
        /// Parses the gametracker page for a particualr gameId and gets all relevant fantasy stats
        /// for the given playerId.
        /// </summary>
        /// <param name="gameId">Game ID of the game this player is playing in</param>
        /// <param name="playerId">Player ID of the player we are pulling stats for. If this is "", then we are looking for a team defense</param>
        /// <param name="home_or_away">Depending on if the player is playing a home or away game, we find stats in a particular data tag</param>
        /// <param name="opponentAbbreviation">This is only used when scoring a team defnese so we know when an opponent scores a 2 point conversion</param>
        /// <returns>Fantasy points for this player (without 2-pt conversions, which is handled in a separate method</returns>
        public double parseGameTrackerPage(string gameId, string playerId, string homeOrAway, string opponentAbbreviation)
        {
            double fantasyPoints = 0;

            // TODO: CHECK IF GAME IS OVER AND STORE THE DOC IN CACHE
            // if a game has ended, we will find this node:
            // < span class="game-time status-detail">Final</span>
            var statusDetailNode = gameTrackerDoc.DocumentNode.SelectSingleNode("//span[@class='game-time status-detail']");
            if ((statusDetailNode != null) && statusDetailNode.InnerText.ToLower().Equals("final"))
            {
                // add to cache with something saying that the game is over
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
                    // e.g. - "Lost Angeles Passing" or "Carolina Passing" should return "Passing"
                    string teamNameText = teamNameNode.InnerText;
                    int lastSpaceIndex = teamNameText.LastIndexOf(" ");
                    string stat = teamNameText.Substring(lastSpaceIndex + 1);

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

                        // if the playerId coming into this function is "0", as stored in the database, that means we are searching for
                        // team defense stats, so we will fall into the else to do that. Otherwise, if there is a playerID coming into
                        // this function and there isn't a player for this stat (e.g. this player has no interceptions), there will not
                        // be a player ID Node, so we will just skip this stat
                        if ((playerIdNode != null) && (!playerId.Equals("0")))
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
                        else if ((playerIdNode != null) && (playerId.Equals("0")))
                        {
                            if (stat.Equals("Defensive"))
                                fantasyPoints += handleDefensiveStats(statsNode);
                            else if (stat.Equals("Interceptions"))
                                fantasyPoints += handleInterceptionStats(statsNode);
                            else if (stat.Equals("Returns"))
                                fantasyPoints += handleReturnStats(statsNode);
                        }
                    }
                }
            }

            // if we are scoring for team defense (playerId == "0"), we need to check how many points were scored
            // against them as well as whether they blocked any kicks or field goals
            if (playerId.Equals("0"))
            {
                fantasyPoints += handleDefenseTeamPoints(gameTrackerDoc, homeOrAway, gameId, opponentAbbreviation);
                fantasyPoints += handleBlockedKicksAndPunts(gameId, opponentAbbreviation);
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
                            string statType = node.Attributes[0].Value;

                            // if we have found the class="td" <td>, then we will get the total touchdowns for this stat (passing or rushing)
                            if (statType.Equals("td"))
                            {
                                pointsAllowed += int.Parse(node.InnerText) * 6;
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
                pointsAllowed += handleDefenseTeamPointsWithTwoPointConversions(gameId, opponentAbbreviation);

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
        /// Blocked kicks and punts only show up in the play by play, so we need to check to see if a defense
        /// gets 2 points for each blocked kick and punt.
        /// </summary>
        /// <param name="gameId">The gameId of the ESPN game this defense is a part of</param>
        /// <param name="opponentAbbreviation">The three letter abbreviation of the defense's opponent</param>
        /// <returns></returns>
        private int handleBlockedKicksAndPunts(string gameId, string opponentAbbreviation)
        {
            int blockedPoints = 0;

            // We are looking for the <li class="accordion-item"> nodes which contain two div's, the first of which is the header showing the
            // outcome of the drive and the logo of the team, and the second of which is the play by plays. In the second div with the play
            // by plays, we'll search for the "blocked" text; if it's there, we will use the first div to get the team name from the logo,
            // such as:
            // <span class="home-logo">
            //    < img class="team-logo" src="https://a.espncdn.com/combiner/i?img=/i/teamlogos/nfl/500/cle.png&h=100&w=100"/>
            // </span>
            // The "cle" would be the opponent abbreviation
            var driveNodes = playByPlayDoc.DocumentNode.SelectNodes("//li[@class='accordion-item']");

            // if the game hasn't started, ther ewill be no data, so check for null
            if (driveNodes != null)
            {
                int i = 0;
                foreach (var driveNode in driveNodes)
                {
                    // first check the second div which has the drives to see if "blocked" text is there:
                    // <div id="gp-playbyplay-4013264237" class="accordion-content collapse">
                    //     <div class="content">
                    //         <ul class="drive-list">
                    //             <li class=""> (all plays in the drive will look like this node
                    //                 <h3>4th & 13 at DEN 23</h3>
                    //                 <p>
                    //                     <span class="post-play">
                    //                         (1:55 - 2nd)  C.McLaughlin 41 yard field goal is BLOCKED (S.Harris), Center-C.Hughlett, Holder-J.Gillan.
                    //                     </span>
                    //                 </p>
                    //             </li>
                    // Check the div nodes with the playbyplay for this drive has the "blocked" test
                    var blockedNodes = driveNode.Descendants("span").Where(node => node.InnerText.ToLower().Contains("blocked"));

                    // TODO: We may need to check for more htan one in case there is a penalty and the block is taken back
                    if (blockedNodes.Count() > 0)
                    {
                        // we need to go back to the first div of the drive node and get the logo to pull out
                        // the opponent name
                        string teamLogoUrl = driveNode.SelectSingleNode(".//span[@class='home-logo']/img").Attributes["src"].Value;

                        // the logo will be in the format of:
                        // https://a.espncdn.com/combiner/i?img=/i/teamlogos/nfl/500/cle.png&h=100&w=100
                        if (teamLogoUrl.Contains(opponentAbbreviation + ".png"))
                        {
                            blockedPoints += 2;
                        }
                    }

                    i++;
                }
            }

            return blockedPoints;
        }
        
        /// <summary>
        /// Checks the play by play page to see if the opponent scored any 2-point conversions.
        /// </summary>
        /// <param name="gameId"></param>
        /// <param name="opponentAbbreviation"></param>
        /// <returns>The number of points scored by the opponent from 2-pt conversions, which is used to
        /// figure out how many total points the defense has let up, determining fantasy points earned.</returns>
        private int handleDefenseTeamPointsWithTwoPointConversions(string gameId, string opponentAbbreviation)
        {
            int twoPointConversionPoints = 0;

            // We are looking for the <div id="gamepackage-scoring-summary">/div/table table, which will have all rows with scoring drives.
            // The scoring drives row will have the first <td> being <td class="logo">, where we will need to parse the logo to see
            // if it is the first three letters of the team.png (e.g., bal.png for Baltimore).
            var postPlayNodes = playByPlayDoc.DocumentNode.SelectNodes("//div[@class='scoring-summary']/table/tbody/tr");

            // if the game hasn't started, there will be no data, so check for null
            if (postPlayNodes != null)
            {
                foreach (var postPlayNode in postPlayNodes)
                {
                    // The rows without attributes in the <tr> contain the game-detail drives
                    if (postPlayNode.Attributes.Count == 0)
                    {
                        var imgNode = postPlayNode.SelectSingleNode(".//td/img");

                        string logoName = imgNode.Attributes[1].Value;

                        // we found the opponent drive, so now let's check if it's a 2-pt conversion
                        if (logoName.Contains(opponentAbbreviation + ".png"))
                        {
                            // the <div class="headline"> node will has the scoring play which we will check
                            var headlineNode = postPlayNode.SelectSingleNode(".//td/div/div/div[@class='headline']");

                            if (headlineNode.InnerText.ToLower().Contains("two-point") &&
                                !headlineNode.InnerText.ToLower().Contains("failed"))
                            {
                                twoPointConversionPoints += 2;

                            }
                        }
                    }
                }
            }

            return twoPointConversionPoints;
        }

        /// <summary>
        /// This will loop through all defense (and actually, special teams) kick or punt returns. The only thing we care about here are touchdowns:
        /// <td class="td"></td>
        /// </summary>
        /// <param name="statsNode"></param>
        /// <returns></returns>
        private int handleReturnStats(HtmlNode statsNode)
        {
            int defensiveReturnPoints = 0;
            int touchdowns = 0;

            foreach (var node in statsNode.ChildNodes)
            {
                string stat = node.Attributes[0].Value;

                if (stat.Equals("td"))
                    touchdowns += int.Parse(node.InnerText);
            }

            defensiveReturnPoints += touchdowns * DEFENSIVE_TD_POINTS;

            return defensiveReturnPoints;
        }

        /// <summary>
        /// This will loop through all interception stats for each player who has interceptions for the given team. These will be in the format of
        /// <td class="int"></td>
        /// <td class="td"></td>
        /// </summary>
        /// <param name="statsNode"></param>
        /// <returns></returns>
        private int handleInterceptionStats(HtmlNode statsNode)
        {
            int defensivePointsFromInterceptions = 0;

            int interceptions = 0;
            int touchdowns = 0;

            foreach (var node in statsNode.ChildNodes)
            {
                string stat = node.Attributes[0].Value;

                if (stat.Equals("int"))
                    interceptions += int.Parse(node.InnerText);
                else if (stat.Equals("td"))
                    touchdowns += int.Parse(node.InnerText);
            }

            defensivePointsFromInterceptions += (interceptions * DEFENSIVE_INT_POINTS) + (touchdowns * DEFENSIVE_TD_POINTS);

            return defensivePointsFromInterceptions;
        }

        /// <summary>
        /// This will loop through all defensive stats for the team. We are only interested in
        /// sacks and touchdowns, which will be in the format:
        /// <td class="sacks"></td>
        /// <td class="td"></td>
        /// </summary>
        /// <param name="statsNode"></param>
        /// <returns></returns>
        private double handleDefensiveStats(HtmlNode statsNode)
        {
            double defensivePoints = 0;

            double sacks = 0;
            int touchdowns = 0;

            foreach (var node in statsNode.ChildNodes)
            {
                string stat = node.Attributes[0].Value;

                if (stat.Equals("sacks"))
                    // a player can get 0.5 sacks, so we need to parse as a double
                    sacks += (double)Convert.ToDouble(node.InnerText);
                else if (stat.Equals("td"))
                    touchdowns += int.Parse(node.InnerText);
            }

            defensivePoints += (sacks * DEFENSIVE_SACK_POINTS) + (touchdowns * DEFENSIVE_TD_POINTS);

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