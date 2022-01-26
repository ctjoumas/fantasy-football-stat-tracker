namespace FantasyFootballStatTracker.Models
{
    using System.Collections.Generic;

    public class Team
    {
        public List<SelectedPlayer> Players { get; set; }

        public int OwnerId { get; set; }

        public int Week { get; set; }

        public byte[] OwnerLogo { get; set; }

        public double TotalFantasyPoints { get; set; }
    }
}