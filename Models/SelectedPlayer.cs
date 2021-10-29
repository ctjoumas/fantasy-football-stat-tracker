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

        public DateTime GameTime { get; set; }

        public string EspnPlayerId { get; set; }

        public string HomeOrAway { get; set; }

        // this can probably be removed; this was initially from entering player name manually
        public string RawPlayerName { get; set; }

        public string OpponentAbbreviation { get; set; }
    }

    /// <summary>
    /// Positions for fantasy players. The numbers are used to sort the the players for display.
    /// </summary>
    public enum Position
    {
        QB = 0,
        RB = 1,
        WR = 2,
        FLEX = 3,
        TE = 4,
        K = 5,
        DEF = 6
    }
}