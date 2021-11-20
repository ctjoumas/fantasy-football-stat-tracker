namespace FantasyFootballStatTracker.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using YahooFantasyFootball.Models;

    /// <summary>
    /// Stores the properties of the CurrentRoster database for each owners rostered players.
    /// </summary>
    public class RosterPlayer
    {
        public string Owner { get; set; }
        public byte[] Logo { get; set; }
        public int Week { get; set; }
        public string PlayerName { get; set; }
        public Position Position { get; set; }
        public bool GameEnded { get; set; }
        public double FinalPoints { get; set; }

        /// <summary>
        /// If the game is over, we will have a final score string such as "(W) 45 - 30"
        /// </summary>
        public string FinalScoreString { get; set; }
    }
}