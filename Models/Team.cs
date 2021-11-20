namespace YahooFantasyFootball.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class Team
    {
        public List<SelectedPlayer> Players { get; set; }

        public string Owner { get; set; }

        public byte[] OwnerLogo { get; set; }

        public double TotalFantasyPoints { get; set; }
    }
}
