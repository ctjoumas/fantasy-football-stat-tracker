namespace FantasyFootballStatTracker.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Stores the properties of the CurrentRoster database for each owners rostered players.
    /// </summary>
    public class RosterPlayer
    {
        public string Owner { get; set; }
        public int Week { get; set; }
        public string PlayerName { get; set; }
        public string Position { get; set; }
    }
}