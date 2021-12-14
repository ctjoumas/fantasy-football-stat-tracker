namespace FantasyFootballStatTracker.Models
{
    using System.Collections.Generic;

    public class PlayerModel
    {
        /// <summary>
        /// Stores all unselected players in the main listbox which the user will be selecting from.
        /// </summary>
        public List<EspnPlayer> PlayerUnselectedList { get; set; }
        public List<int> PlayerIdSelectedListOwner1 { get; set; }
        public List<int> PlayerIdSelectedListOwner2 { get; set; }
    }

    public class EspnPlayer
    {
        public int EspnPlayerId { get; set; }
        public string PlayerName { get; set; }
    }
}