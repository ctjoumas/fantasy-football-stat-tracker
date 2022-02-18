namespace FantasyFootballStatTracker.Models
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class OwnerStats
    {
        public OwnerStats()
        {
            WeeklyScores = new Hashtable();
            Wins = 0;
            Losses = 0;
            TotalPoints = 0;
            Streak = "";
        }

        /// <summary>
        /// Stores each weeks score where the key is the week and the value is the score
        /// </summary>
        public Hashtable WeeklyScores { get; set; }
        public byte[] OwnerLogo { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public double TotalPoints { get; set; }
        public string Streak { get; set; }
    }
}