namespace FantasyFootballStatTracker.Models
{
    /// <summary>
    /// Represents the current state of a draft session
    /// </summary>
    public class DraftState
    {
        public List<Owner> Owners { get; set; }
        public int Week { get; set; }
        public int CurrentPickOwnerId { get; set; }
        public int FirstPickOwnerId { get; set; }
        public int PickNumber { get; set; }
        public int TotalPicks { get; set; }
        public List<DraftedPlayer> Owner1Roster { get; set; } = new();
        public List<DraftedPlayer> Owner2Roster { get; set; } = new();
        public bool IsComplete { get; set; }
        public string DraftId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Gets the roster for a specific owner
        /// </summary>
        public List<DraftedPlayer> GetRosterForOwner(int ownerId)
        {
            return ownerId == 1 ? Owner1Roster : Owner2Roster;
        }

        /// <summary>
        /// Gets the name of the current pick owner
        /// </summary>
        public string GetCurrentPickOwnerName()
        {
            string ownerName = "Unknown";

            foreach (var owner in Owners)
            {
                if (owner.OwnerId == CurrentPickOwnerId)
                {
                    ownerName = owner.OwnerName;
                    break;
                }
            }

            return ownerName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string GetOwnerName(int ownerId)
        {
            string ownerName = "Unknown";

            foreach (var owner in Owners)
            {
                if (owner.OwnerId == ownerId)
                {
                    ownerName = owner.OwnerName;
                    break;
                }
            }

            return ownerName;
        }

        /// <summary>
        /// Gets the pick order display (e.g., "Round 1, Pick 2")
        /// </summary>
        public string GetPickDisplay()
        {
            int round = ((PickNumber - 1) / 2) + 1;
            int pickInRound = ((PickNumber - 1) % 2) + 1;
            return $"Round {round}, Pick {pickInRound}";
        }

        /// <summary>
        /// Checks if a roster is valid (has required positions)
        /// </summary>
        public bool IsRosterValid(int ownerId)
        {
            var roster = GetRosterForOwner(ownerId);
            if (roster.Count != 9) return false;

            var positions = roster.GroupBy(p => p.Position).ToDictionary(g => g.Key, g => g.Count());

            // Required: 1 QB, 1 K, 1 DEF
            if (!positions.ContainsKey("QB") || positions["QB"] != 1) return false;
            if (!positions.ContainsKey("K") || positions["K"] != 1) return false;
            if (!positions.ContainsKey("DEF") || positions["DEF"] != 1) return false;

            // Flexible positions: need 2 RB, 2 WR, 1 TE minimum, then 1 FLEX
            int rbCount = positions.ContainsKey("RB") ? positions["RB"] : 0;
            int wrCount = positions.ContainsKey("WR") ? positions["WR"] : 0;
            int teCount = positions.ContainsKey("TE") ? positions["TE"] : 0;

            // Valid combinations for RB/WR/TE (total should be 6):
            // 3 RB, 2 WR, 1 TE OR
            // 2 RB, 3 WR, 1 TE OR  
            // 2 RB, 2 WR, 2 TE
            int flexTotal = rbCount + wrCount + teCount;
            if (flexTotal != 6) return false;

            return (rbCount >= 2 && wrCount >= 2 && teCount >= 1);
        }

        /// <summary>
        /// Generates a unique Draft ID based on the week and current UTC time
        /// </summary>
        public static string GenerateDraftId(int week)
        {
            return $"draft_week_{week}_{DateTimeOffset.UtcNow:yyyyMMdd}";
        }
    }

    /// <summary>
    /// Represents a player that has been drafted
    /// </summary>
    public class DraftedPlayer
    {
        public int EspnPlayerId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string TeamAbbreviation { get; set; } = string.Empty;
        public int PickNumber { get; set; }
        public int OwnerId { get; set; }

        /// <summary>
        /// Gets a display-friendly position name
        /// </summary>
        public string GetDisplayPosition()
        {
            return Position switch
            {
                "PK" => "K",
                _ => Position
            };
        }

        /// <summary>
        /// Gets a formatted player display (e.g., "Justin Jefferson (WR - MIN)")
        /// </summary>
        public string GetPlayerDisplay()
        {
            return $"{PlayerName} ({GetDisplayPosition()} - {TeamAbbreviation.ToUpper()})";
        }
    }

    public class Owner
    {
        public int OwnerId { get; set; }
        public string OwnerName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents an event that occurs during the draft
    /// </summary>
    public class DraftEvent
    {
        public string EventType { get; set; } = string.Empty; // "PICK_MADE", "DRAFT_COMPLETE", etc.
        public string DraftId { get; set; } = string.Empty;
        public int OwnerId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public int PickNumber { get; set; }
        public int EspnPlayerId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}