namespace FantasyFootballStatTracker.Infrastructure
{
    using HtmlAgilityPack;
    using System.Linq;

    public class PlayByPlayHtmlParser : IPlayByPlayParser
    {
        private HtmlDocument _playByPlayDoc;

        public PlayByPlayHtmlParser(HtmlDocument playByPlayDoc)
        {
            _playByPlayDoc = playByPlayDoc;
        }

        /// <summary>
        /// Safeties only show up in the play by play, so we need to check to see if a defense
        /// gets 2 points for each safety.
        /// </summary>
        /// <param name="opponentAbbreviation">The three letter abbreviation of the defense's opponent</param>
        /// <returns></returns>
        public int handleSafeties(string opponentAbbreviation)
        {
            int safetyPoints = 0;

            // We are looking for the <li class="accordion-item"> nodes which contain two div's, the first of which is the header showing the
            // outcome of the drive and the logo of the team, and the second of which is the play by plays. In the second div with the play
            // by plays, we'll search for the "blocked" text; if it's there, we will use the first div to get the team name from the logo,
            // such as:
            // <span class="home-logo">
            //    < img class="team-logo" src="https://a.espncdn.com/combiner/i?img=/i/teamlogos/nfl/500/cle.png&h=100&w=100"/>
            // </span>
            // The "cle" would be the opponent abbreviation
            var driveNodes = _playByPlayDoc.DocumentNode.SelectNodes("//li[@class='accordion-item']");

            // if the game hasn't started, there will be no data, so check for null
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
                    var safetyNodes = driveNode.Descendants("span").Where(node => node.InnerText.ToLower().Contains("safety"));

                    // TODO: We may need to check for more than one in case there is a penalty and the block is taken back
                    if (safetyNodes.Count() > 0)
                    {
                        // we need to go back to the first div of the drive node and get the logo to pull out
                        // the opponent name
                        string teamLogoUrl = driveNode.SelectSingleNode(".//span[@class='home-logo']/img").Attributes["src"].Value;

                        // the logo will be in the format of:
                        // https://a.espncdn.com/combiner/i?img=/i/teamlogos/nfl/500/cle.png&h=100&w=100
                        if (teamLogoUrl.Contains(opponentAbbreviation + ".png"))
                        {
                            safetyPoints += 2;
                        }
                    }

                    i++;
                }
            }

            return safetyPoints;
        }

        /// <summary>
        /// Blocked kicks and punts only show up in the play by play, so we need to check to see if a defense
        /// gets 2 points for each blocked kick and punt.
        /// </summary>
        /// <param name="opponentAbbreviation">The three letter abbreviation of the defense's opponent</param>
        /// <returns></returns>
        public int handleBlockedKicksAndPunts(string opponentAbbreviation)
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
            var driveNodes = _playByPlayDoc.DocumentNode.SelectNodes("//li[@class='accordion-item']");

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
        /// <param name="opponentAbbreviation"></param>
        /// <returns>The number of points scored by the opponent from 2-pt conversions, which is used to
        /// figure out how many total points the defense has let up, determining fantasy points earned.</returns>
        public int handleDefenseTeamPointsWithTwoPointConversions(string opponentAbbreviation)
        {
            int twoPointConversionPoints = 0;

            // We are looking for the <div id="gamepackage-scoring-summary">/div/table table, which will have all rows with scoring drives.
            // The scoring drives row will have the first <td> being <td class="logo">, where we will need to parse the logo to see
            // if it is the first three letters of the team.png (e.g., bal.png for Baltimore).
            var postPlayNodes = _playByPlayDoc.DocumentNode.SelectNodes("//div[@class='scoring-summary']/table/tbody/tr");

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
        /// This shouldn't be needed as the current score will only be parsed during a live game, in which case,
        /// the JSON parser will be used.
        /// </summary>
        /// <param name="homeOrAway"></param>
        /// <returns></returns>
        public string parseCurrentScore(string homeOrAway)
        {
            return "N/A";
        }

        /// <summary>
        /// Calculate the field goal points, based on distance, for a kicker. This data is not on the gametracker page, but we are able to get the
        /// number of XPs from there which is calculated in the parseGameTrackerPage method.
        /// </summary>
        /// <param name="playerName"></param>
        /// <returns>Total number of FG points (not counting XPs)</returns>
        public int parseFieldGoals(string playerName)
        {
            int fieldGoalPoints = 0;

            // the player name in the text could be full name or first initial and last name (e.g., N.Folk), so we need
            // to build that name here to add into the check below
            int indexOfSpaceAfterPlayerName = playerName.IndexOf(" ");
            string abbreviatedPlayerName = playerName[0] + "." + playerName.Substring(indexOfSpaceAfterPlayerName + 1);

            // we are looking for all <span class="post-play"> nodes which have "Field Goal" in the inner text such as:
            // <span class="post-play">
            //   (6:07 - 1st) Tyler Bass 24 Yd Field Goal
            // </span>
            var fieldGoalNodes = _playByPlayDoc.DocumentNode.Descendants("span")
                    .Where(node => node.InnerText.ToLower().Contains("field goal"));

            foreach (var fieldGoalNode in fieldGoalNodes)
            {
                // if the field goal was good, check if the player's name was invovled (pass
                // or reception, it's 2 points either way)
                if (!fieldGoalNode.InnerText.ToLower().Contains("no good") && !fieldGoalNode.InnerText.ToLower().Contains("nullified") &&
                    (fieldGoalNode.InnerText.ToLower().Contains(playerName.ToLower()) || fieldGoalNode.InnerText.ToLower().Contains(abbreviatedPlayerName.ToLower())))
                {
                    // this player successfully kicked a FG, so we need to parse out the length.
                    // it will be in this format: (5:02 - 3rd) Justin Tucker 39 Yd Field Goal,
                    // so we will look for the player name and grab the number between the next two spaces
                    int playerNameIndex = fieldGoalNode.InnerText.ToLower().IndexOf(playerName.ToLower());

                    // if the full player name isn't found, we need to check for the abbreviated player name
                    if (playerNameIndex == -1)
                    {
                        playerNameIndex = fieldGoalNode.InnerText.ToLower().IndexOf(abbreviatedPlayerName.ToLower());
                        indexOfSpaceAfterPlayerName = fieldGoalNode.InnerText.IndexOf(" ", playerNameIndex + abbreviatedPlayerName.Length);
                    }
                    else
                    {
                        indexOfSpaceAfterPlayerName = fieldGoalNode.InnerText.IndexOf(" ", playerNameIndex + playerName.Length);
                    }

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
        /// Parses the play by play node and gets the last node, which will have the time remaining and current quarter
        /// of the last play, such as: (13:16 - 1st)  D.Johnson left tackle to DEN 4 for 10 yards (K.Jackson). In this case,
        /// we will store "13:16 1Q". If it's on OT, it will be "13:16 OT".
        /// </summary>
        /// <returns></returns>
        public string parseTimeRemaining()
        {
            string timeRemaining = "";

            // we are looking for all <span class="post-play"> nodes
            var postPlayNodes = _playByPlayDoc.DocumentNode.SelectNodes("//span[@class='post-play']");

            // get the last play by play node and parse out the time remaining and current quarter
            var lastPostPlayNode = postPlayNodes[postPlayNodes.Count - 1];

            // don't parse time remaining if the game has ended
            if (!lastPostPlayNode.InnerText.ToLower().Equals("end game"))
            {
                int indexOfFirstParenthesis = lastPostPlayNode.InnerText.IndexOf("(");
                int indexOfLastParenthesis = lastPostPlayNode.InnerText.IndexOf(")");

                // this will leave us with something like "13:16 - 1st)
                timeRemaining = lastPostPlayNode.InnerText.Substring(indexOfFirstParenthesis + 1, (indexOfLastParenthesis - indexOfFirstParenthesis - 1));
                string[] timeRemainingElements = timeRemaining.Split("-");
                timeRemaining = string.Join("", timeRemainingElements);
            }

            return timeRemaining;
        }

        /// <summary>
        /// Parses the play by play page and finds all two point conversions which did not "fail". Of the ones
        /// which didn't fail, we will see if the player is part of this play (there is no player id associated
        /// with this page)
        /// </summary>
        /// <param name="playerName">The full name, first and last, of the player</param>
        /// <returns></returns>
        public double parseTwoPointConversionsForPlayer(string playerName)
        {
            double fantasyPoints = 0;

            // the two-point conversion may be using the abbreviated players name, so we need to check both full and abbreviated names
            int spaceIndex = playerName.IndexOf(" ");
            string abbreviatedPlayerName = playerName.Substring(0, 1) + "." + playerName.Substring(spaceIndex + 1);

            // we are looking for all <span class="post-play"> nodes which have "Two-Point" in the inner text such as:
            // <span class="post-play">
            //   (0:27 - 3rd) Tommy Sweeney Pass From Josh Allen for 1 Yard (Pass formation) TWO-POINT CONVERSION ATTEMPT. D.Knox pass to J.Allen is complete. ATTEMPT SUCCEEDS.
            // </span>
            var twoPointConversionNodes = _playByPlayDoc.DocumentNode.Descendants("span")
                    .Where(node => node.InnerText.ToLower().Contains("two-point"));

            foreach (var twoPointConversionNode in twoPointConversionNodes)
            {
                // if the two-point conversion didn't fail (which will appear with text "fails or failed"), check if
                // the player's name was invovled (pass or reception, it's 2 points either way)
                if (!twoPointConversionNode.InnerText.ToLower().Contains("failed") &&
                    !twoPointConversionNode.InnerText.ToLower().Contains("fails") &&
                    (twoPointConversionNode.InnerText.ToLower().Contains(playerName.ToLower()) || twoPointConversionNode.InnerText.ToLower().Contains(abbreviatedPlayerName.ToLower())))
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
                    if (twoPointConversionNode.InnerText.ToLower().Contains("two-point conversion attempt"))
                    {
                        // cut off the scoring play and just check the two point conversion text remaining
                        index = twoPointConversionNode.InnerText.ToLower().IndexOf("two-point conversion attempt");
                        twoPointConversionText = twoPointConversionNode.InnerText.Substring(index);
                    }
                    else
                    {
                        // cut off the scoring play, which is before the parentheses
                        index = twoPointConversionNode.InnerText.IndexOf("(");
                        twoPointConversionText = twoPointConversionNode.InnerText.Substring(index);
                    }

                    // now we can check the text containing only the two point conversion play to see if this player was involved
                    if (twoPointConversionText.ToLower().Contains(playerName.ToLower()) ||
                        twoPointConversionText.ToLower().Contains(abbreviatedPlayerName.ToLower()))
                    {
                        fantasyPoints += 2;
                    }
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
    }
}