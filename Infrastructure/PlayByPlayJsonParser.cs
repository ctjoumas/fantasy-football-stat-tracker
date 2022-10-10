namespace FantasyFootballStatTracker.Infrastructure
{
    using Newtonsoft.Json.Linq;

    public class PlayByPlayJsonParser : IPlayByPlayParser
    {
        private JObject _playByPlayJsonObject;

        public PlayByPlayJsonParser(JObject playByPlayJsonObject)
        {
            _playByPlayJsonObject = playByPlayJsonObject;
        }

        /// <summary>
        /// Safeties only show up in the play by play, so we need to check the JSON to see if a defense
        /// gets 2 points for each safety.
        /// </summary>
        /// <param name="opponentAbbreviation">The three letter abbreviation of the defense's opponent</param>
        /// <returns></returns>
        public int handleSafeties(string opponentAbbreviation)
        {
            int safetyPoints = 0;

            // each play token is a drive, so we will go through this to parse all player stats
            JToken driveTokens = _playByPlayJsonObject.SelectToken("drives.previous");

            // if the game started and there are no drives yet
            if (driveTokens != null)
            {
                foreach (JToken driveToken in driveTokens)
                {
                    JToken driveResultValue = driveToken.SelectToken("displayResult");

                    if (driveResultValue != null)
                    {
                        // if parsing blocked punts and kicks, we can check to see if there is a block in this drive, otherwise, we don't need to parse this
                        string driveResult = ((JValue)driveToken.SelectToken("displayResult")).Value.ToString();

                        // only parse the plays in this drive if this drive resulted in a safety
                        // TODO: guessing that this is what the drive result would say; have not been able to see this yet during live game
                        if (driveResult.ToLower().Contains(("safety")))
                        {
                            // get the team who just got the safety
                            string teamAbbreviation = (string)((JValue)driveToken.SelectToken("team.abbreviation")).Value;

                            // if the team abbreviation on this drive is the opponent, we'll give 2 points
                            if (teamAbbreviation.ToLower().Equals(opponentAbbreviation.ToLower()))
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
        /// Blocked kicks and punts only show up in the play by play, so we need to check the JSON to see if a defense
        /// gets 2 points for each blocked kick and punt.
        /// </summary>
        /// <param name="opponentAbbreviation">The three letter abbreviation of the defense's opponent</param>
        /// <returns></returns>
        public int handleBlockedKicksAndPunts(string opponentAbbreviation)
        {
            int blockedPoints = 0;

            // each play token is a drive, so we will go through this to parse all player stats
            JToken driveTokens = _playByPlayJsonObject.SelectToken("drives.previous");

            // if the game started and there are no drives yet
            if (driveTokens != null)
            {
                foreach (JToken driveToken in driveTokens)
                {
                    JToken driveResultValue = driveToken.SelectToken("displayResult");

                    if (driveResultValue != null)
                    {
                        // if parsing blocked punts and kicks, we can check to see if there is a block in this drive, otherwise, we don't need to parse this
                        string driveResult = ((JValue)driveToken.SelectToken("displayResult")).Value.ToString();

                        // only parse the plays in this drive if this drive resulted in a made FG
                        if (driveResult.ToLower().Contains(("blocked")))
                        {
                            // get the team who just got their punt or kick blocked
                            string teamAbbreviation = (string)((JValue)driveToken.SelectToken("team.abbreviation")).Value;

                            // if the team abbreviation on this drive is the opponent, we'll give 2 points
                            if (teamAbbreviation.ToLower().Equals(opponentAbbreviation.ToLower()))
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
        /// Checks the play by play JSON to see if the opponent scored any 2-point conversions.
        /// </summary>
        /// <param name="opponentAbbreviation"></param>
        /// <returns>The number of points scored by the opponent from 2-pt conversions, which is used to
        /// figure out how many total points the defense has let up, determining fantasy points earned.</returns>
        public int handleDefenseTeamPointsWithTwoPointConversions(string opponentAbbreviation)
        {
            int twoPointConversionPoints = 0;

            // each play token is a drive, so we will go through this to parse all player stats
            JToken driveTokens = _playByPlayJsonObject.SelectToken("drives.previous");

            // if the game started and there are no drives yet
            if (driveTokens != null)
            {
                foreach (JToken driveToken in driveTokens)
                {
                    JToken driveResultValue = driveToken.SelectToken("displayResult");

                    if (driveResultValue != null)
                    {
                        string driveResult = ((JValue)driveToken.SelectToken("displayResult")).Value.ToString();

                        // check to see if there is a touchdown on this drive, otherwise, we don't need to parse this
                        if (driveResult.ToLower().Contains(("touchdown")))
                        {
                            // get the team who just scored
                            string teamAbbreviation = (string)((JValue)driveToken.SelectToken("team.abbreviation")).Value;

                            // if the team abbreviation on this drive is the opponent, we'll give 2 points
                            if (teamAbbreviation.ToLower().Equals(opponentAbbreviation.ToLower()))
                            {
                                // Now go through each play in this drive to find the two-point conversion and see if it didn't fail.
                                // We are going through each play because if there is a penalty, it looks like that would be the last play
                                // and not the 2 point conversion
                                JToken playTokens = driveToken.SelectToken("plays");

                                foreach (JToken playToken in playTokens)
                                {
                                    string play = (string)((JValue)playToken.SelectToken("text")).Value;

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
        /// Parses the current score for a given player. We don't care about the player name, we just need to know if
        /// the player is home or away so we can list that score first.
        /// </summary>
        /// <param name="homeOrAway">Home or away for the given player so we know which score to display first</param>
        /// <returns>A score in the format of "20-17", with the first number being the home or away score.</returns>
        public string parseCurrentScore(string homeOrAway)
        {
            string currentScore = "0-0";

            // we need to look at the drives.current.plays token. The plays is an array of plays, but we should only care
            // about the last item which should be the latest play
            // TODO: This can be combined with the parseTimeRemaining method since it pulls the same element to find the current time remaining
            JToken currentDriveTokens = _playByPlayJsonObject.SelectToken("drives.current.plays");

            if (currentDriveTokens != null)
            {
                int currentPlayTokens = ((JArray)currentDriveTokens).Count;

                string homeScore = (string)_playByPlayJsonObject.SelectToken("drives.current.plays[" + (currentPlayTokens - 1).ToString() + "].homeScore");
                string awayScore = (string)_playByPlayJsonObject.SelectToken("drives.current.plays[" + (currentPlayTokens - 1).ToString() + "].awayScore");

                if (homeOrAway.Equals("home"))
                {
                    currentScore = homeScore + "-" + awayScore;
                }
                else
                {
                    currentScore = awayScore + "-" + homeScore;
                }
            }
            
            return currentScore;
        }

        /// <summary>
        /// Calculate the field goal points, based on distance, for a kicker. This data is only found in the JSON play by play, but we
        /// are able to get the number of XPs from there which is calculated in the parseGameTrackerPage method.
        /// </summary>
        /// <param name="playerName"></param>
        /// <returns>Total number of FG points (not counting XPs)</returns>
        public int parseFieldGoals(string playerName)
        {
            int fieldGoalPoints = 0;

            // we need to restructure this so it's C.Boswell for Chris Boswell, which is how it's stored in the JSON
            int spaceIndex = playerName.IndexOf(" ");
            string abbreviatedPlayerName = playerName.Substring(0, 1) + "." + playerName.Substring(spaceIndex + 1);

            // each play token is a drive, so we will go through this to parse all player stats
            JToken driveTokens = _playByPlayJsonObject.SelectToken("drives.previous");

            // if the game started and there are no drives yet
            if (driveTokens != null)
            {
                foreach (JToken driveToken in driveTokens)
                {
                    // if parsing field goals, we can check to see if there is a FG made in this drive, otherwise, we don't need to parse this
                    JToken driveResultValue = driveToken.SelectToken("displayResult");

                    if (driveResultValue != null)
                    {
                        string driveResult = ((JValue)driveToken.SelectToken("displayResult")).Value.ToString();

                        // only parse the plays in this drive if this drive resulted in a made FG
                        if (driveResult.ToLower().Equals(("field goal")))
                        {
                            // the FG will be listed as the last play, so grab that token and make sure the kicker's name is in this text
                            JToken playTokens = driveToken.SelectToken("plays");

                            // there could be a penalty or a timeout after the kick, so the FG may not be the last play token so we
                            // need to loop through all plays
                            foreach (JToken playToken in playTokens)
                            {
                                // if this is the field goal token, the "scoringType" token will exist and the displayName will be "Field Goal"
                                JToken scoringTypeToken = playToken["scoringType"];
                                if (scoringTypeToken != null)
                                {
                                    if (scoringTypeToken["displayName"].ToString().ToLower().Equals("field goal"))
                                    {
                                        string playText = (string)playToken["text"];

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

        public string parseTimeRemaining()
        {
            string timeRemaining = "";

            // get the current drive token to get the quarter time remaining
            JToken currentDriveTokens = _playByPlayJsonObject.SelectToken("drives.current.plays");

            if (currentDriveTokens != null)
            {
                int currentPlayTokens = ((JArray)currentDriveTokens).Count;

                string quarter = (string)_playByPlayJsonObject.SelectToken("drives.current.plays[" + (currentPlayTokens - 1).ToString() + "].period.number");
                string clock = (string)_playByPlayJsonObject.SelectToken("drives.current.plays[" + (currentPlayTokens - 1).ToString() + "].clock.displayValue");

                switch (quarter)
                {
                    case "1":
                        quarter = "1st";
                        break;
                    case "2":
                        quarter = "2nd";
                        break;
                    case "3":
                        quarter = "3rd";
                        break;
                    case "4":
                        quarter = "4th";
                        break;
                    case "5": // TODO: guessing on this...need to verify
                        quarter = "OT";
                        break;
                    default:
                        quarter = "?";
                        break;
                }

                timeRemaining = quarter + " " + clock;
            }

            return timeRemaining;
        }

        /// <summary>
        /// Parses the play by play JSON object and finds all two point conversions which did not fail. Of the ones
        /// which didn't fail, we will see if the player is part of this play
        /// </summary>
        /// <param name="playerName">The full name, first and last, of the player</param>
        /// <returns></returns>
        public double parseTwoPointConversionsForPlayer(string playerName)
        {
            double fantasyPoints = 0;

            // each play token is a drive, so we will go through this to parse all player stats
            JToken driveTokens = _playByPlayJsonObject.SelectToken("drives.previous");

            // if the game started and there are no drives yet
            if (driveTokens != null)
            {
                foreach (JToken driveToken in driveTokens)
                {
                    JToken driveResultValue = driveToken.SelectToken("displayResult");

                    if (driveResultValue != null)
                    {
                        string driveResult = ((JValue)driveToken.SelectToken("displayResult")).Value.ToString();

                        // check to see if there is a touchdown on this drive, otherwise, we don't need to parse this
                        if (driveResult.ToLower().Contains(("touchdown")))
                        {
                            // Now go through each play in this drive to find the two-point conversion and see if it didn't fail.
                            // We are going through each play because if there is a penalty, it looks like that would be the last play
                            // and not the 2 point conversion
                            JToken playTokens = driveToken.SelectToken("plays");

                            foreach (JToken playToken in playTokens)
                            {
                                string play = (string)((JValue)playToken.SelectToken("text")).Value;

                                if (play.ToLower().Contains("two-point") &&
                                    !play.ToLower().Contains("fails") && !play.ToLower().Contains("failed") &&
                                    play.ToLower().Contains(playerName.ToLower()))
                                {
                                    fantasyPoints += 2;
                                }
                            }
                        }
                    }
                }
            }

            return fantasyPoints;
        }
    }
}