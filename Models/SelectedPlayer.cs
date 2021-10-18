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
