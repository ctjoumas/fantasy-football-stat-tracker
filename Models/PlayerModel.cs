namespace FantasyFootballStatTracker.Models
{
    using Microsoft.AspNetCore.Mvc.Rendering;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Linq;
    using System.Threading.Tasks;

    public class PlayerModel
    {
        /// <summary>
        /// Stores all unselected players in the main listbox which the user will be selecting from.
        /// </summary>
        public IEnumerable<SelectListItem> PlayerUnselectedList { get; set; }
        public IEnumerable<SelectListItem> PlayerSelectedListOwner1 { get; set; }
        public IEnumerable<SelectListItem> PlayerSelectedListOwner2 { get; set; }
        //public List<EspnPlayer> PlayerUnSelectedList { get; set; }
        //public List<EspnPlayer> PlayerSelectedListOwner1 { get; set; }
        //public List<EspnPlayer> PlayerSelectedListOwner2 { get; set; }

        //public int[] SelectedEspnPlayerIdOwner1 { get; set; }
        /// <summary>
        /// The name of the selected players for owner #1
        /// </summary>
        public string[] SelectedEspnPlayerNamesOwner1 { get; set; }

        //public int[] SelectedEspnPlayerIdOwner2 { get; set; }
        /// <summary>
        /// The name of the selected players for owner #2
        /// </summary>
        public string[] SelectedEspnPlayerNamesOwner2 { get; set; }
    }

    /*public class EspnPlayer
    {
        public int EspnPlayerId { get; set; }
        public string PlayerName { get; set; }
        public string Position { get; set; }
    }*/
}