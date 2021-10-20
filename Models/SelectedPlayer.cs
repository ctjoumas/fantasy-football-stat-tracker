namespace YahooFantasyFootball.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Threading.Tasks;

    public class SelectedPlayer
    {
        public string Headshot { get; set; }

        [Display(Name = "Player")]
        public string Name { get; set; }

        [Display(Name = "Fantasy Points")]
        public double Points { get; set; }

        public Position Position { get; set; }

        public string Owner { get; set; }

        public string EspnGameId { get; set; }
        
        public string EspnPlayerId { get; set; }
        
        public string HomeOrAway { get; set; }
        
        // this can probably be removed; this was initially from entering player name manually
        public string RawPlayerName { get; set; }
        
        public string OpponentAbbreviation { get; set; }
    }

    public enum Position
    {
        QB,
        WR,
        RB,
        TE,
        FLEX,
        K,
        DEF
    }
}