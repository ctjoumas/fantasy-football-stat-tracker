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

        /// <summary>
        /// Stores the true position of the player before a RB/WR/TE is changed to a FLEX for display
        /// purposes.
        /// </summary>
        public Position TruePosition { get; set; }

        /// <summary>
        /// THis will include the FLEX position, which is used for displaying the flex position (RB/WR/TE) in the
        /// flex row in the UI
        /// </summary>
        public Position Position { get; set; }

        public int OwnerId { get; set; }

        public string OwnerName { get; set; }

        // TODO: This should not be stored in the SelectedPlayer as it relates to only the owner; this needs to be redesigned
        public byte[] OwnerLogo { get; set; }

        public string EspnGameId { get; set; }

        public DateTime GameTime { get; set; }

        /// <summary>
        /// If the game is in progress, this will be populated with the last play by play node which will have
        /// the time remaining and quarter, so it will display something such as "4:25 1Q"
        /// </summary>
        public string TimeRemaining { get; set; }

        public string EspnPlayerId { get; set; }

        public string HomeOrAway { get; set; }

        // this can probably be removed; this was initially from entering player name manually
        public string RawPlayerName { get; set; }

        public string TeamAbbreviation { get; set; }

        public string OpponentAbbreviation { get; set; }

        // this is different than ended since even if a game isn't ended, it could not be started, so this flag is needed
        public bool GameInProgress { get; set; } = false;

        public bool GameEnded { get; set; } = false;

        // Holds the current score string such as "17 - 20"
        public string CurrentScoreString { get; set; }

        // Holds the final score string (such as "(W) 45 - 30") from the database, which is stored once the game ends
        public string FinalScoreString { get; set; }
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