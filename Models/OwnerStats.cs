namespace FantasyFootballStatTracker.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class OwnerStats
    {
        public OwnerStats()
        {
            Wins = 0;
            Losses = 0;
            TotalPoints = 0;
            Streak = "";
        }

        public byte[] OwnerLogo { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public double TotalPoints { get; set; }
        public string Streak { get; set; }
    }
}