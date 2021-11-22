namespace FantasyFootballStatTracker
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public interface IPlayByPlayParser
    {
        public string parseTimeRemaining();

        public string parseCurrentScore(string homeOrAway);
        
        public double parseTwoPointConversionsForPlayer(string playerName);

        public int parseFieldGoals(string playerName);

        public int handleBlockedKicksAndPunts(string opponentAbbreviation);

        public int handleDefenseTeamPointsWithTwoPointConversions(string playerName);
    }
}