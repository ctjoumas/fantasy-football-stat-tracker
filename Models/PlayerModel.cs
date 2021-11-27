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
        public List<EspnPlayer> PlayerUnSelectedList { get; set; }
        public List<EspnPlayer> PlayerSelectedListOwner1 { get; set; }
        public List<EspnPlayer> PlayerSelectedListOwner2 { get; set; }
        
        public int[] SelectedEspnPlayerIdOwner1 { get; set; }
        public string[] SelectedEspnPlayerNamesOwner1 { get; set; }
        
        public int[] SelectedEspnPlayerIdOwner2 { get; set; }
        public string[] SelectedEspnPlayerNamesOwner2 { get; set; }
        /*public List<SelectListItem> Players { get; set; }

        [Key]
        public int EspnPlayerID { get; set; }
        public string PlayerName { get; set; }
        public string Position { get; set; }*/
    }

    public class EspnPlayer
    {
        public int EspnPlayerId { get; set; }
        public string PlayerName { get; set; }
        public string Position { get; set; }
    }
}
