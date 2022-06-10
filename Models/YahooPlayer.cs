namespace FantasyFootballStatTracker.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    public class Playerimage
    {
        [JsonProperty("nfl.p.32711")]
        public string NflP32711;
    }

    public class PlayerimageHeadshotLarge
    {
        [JsonProperty("nfl.p.32711")]
        public string NflP32711;
    }

    public class PlayerimageLarge
    {
        [JsonProperty("nfl.p.32711")]
        public string NflP32711;
    }

    public class PlayerimageLineup
    {
        [JsonProperty("nfl.p.32711")]
        public string NflP32711;
    }

    public class PlayerimageLineupFallback
    {
        [JsonProperty("nfl.p.32711")]
        public string NflP32711;
    }

    public class PlayerimageMedium
    {
        [JsonProperty("nfl.p.32711")]
        public string NflP32711;
    }

    public class PlayerimageSmall
    {
        [JsonProperty("nfl.p.32711")]
        public string NflP32711;
    }
    public class Positions
    {
        public class nflpos14
        {
            public string position_id { get; set; }
            public string name { get; set; }
            public string collective_name { get; set; }
            public string abbr { get; set; }
            public bool primary { get; set; }
        }

        public nflpos14 P { get; set; }
    }

    public class Root
    {
        /*public Playerimage Playerimage;
        public PlayerimageHeadshotLarge PlayerimageHeadshotLarge;
        public PlayerimageLarge PlayerimageLarge;
        public PlayerimageMedium PlayerimageMedium;
        public PlayerimageSmall PlayerimageSmall;
        public PlayerimageLineup PlayerimageLineup;
        public PlayerimageLineupFallback PlayerimageLineupFallback;*/
        public Positions Positions;
        /*public Players Players { get; set; }
        public League League;*/
    }

    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    /*public class _1
    {
        public string PhaseId;
        public string Name;
        public string SchedState;
        public string PhaseStart;
        public string PhaseEnd;
        public string PhaseStartWeek;
        public string PhaseEndWeek;
        public List<string> Structures;
    }

    public class _2
    {
        public string PhaseId;
        public string Name;
        public string SchedState;
        public string PhaseStart;
        public string PhaseEnd;
        public string PhaseStartWeek;
        public string PhaseEndWeek;
        public List<string> Structures;
    }

    public class _3
    {
        public string PhaseId;
        public string Name;
        public string SchedState;
        public string PhaseStart;
        public string PhaseEnd;
        public string PhaseStartWeek;
        public string PhaseEndWeek;
        public List<string> Structures;
    }

    public class _4
    {
        public string PhaseId;
        public string Name;
        public string SchedState;
        public string PhaseStart;
        public string PhaseEnd;
        public string PhaseStartWeek;
        public string PhaseEndWeek;
        public List<string> Structures;
    }

    public class CurrentStatState
    {
        public int Season;
        public string Week;
        public string GraphitePhase;
    }

    public class League
    {
        public List<string> _0;
        public object CompetitorsRanking;
        public object TennisName;
        public object TennisType;
        public object TennisActiveDate;
        public string LeagueId;
        public string Name;
        public string DisplayName;
        public string LeagueShortName;
        public object Poll;
        public object EditorialRanking;
        public object Polls;
        public Season Season;
        public SeasonPhases SeasonPhases;
        public object Seasons;
        public string Link;
        public List<string> StatCategories;
        public List<string> Tournaments;
        public object Matches;
        public object PageMetadata;
        public object S2Id;
        public object CompetitorsStart;
        public object CompetitorsTotal;
        public object Alphabet;
        public int PlayersStart;
        public int PlayersTotal;
        public List<string> Positions;
        public object OddsProvider;
        public List<string> FuturesOdds;
        public List<string> Transactions;
        public object SportacularUrl;
        public object TournamentScheduleAvailable;
    }

    public class NflP32711
    {
        public string PlayerId { get; set; }
        public string DisplayName { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string TeamId;
        public string HomeUrl;
        public string UniformNumber;
        public object Bat;
        public object Throw;
        public object Shoots;
        public object S2Id;
        public List<object> Seasons;
        public SeasonPhases SeasonPhases;
        public string SportacularUrl;
        public List<string> Image;
        public List<string> ImageHeadshotLarge;
        public List<string> ImageLarge { get; set; }
        public List<string> ImageMedium;
        public List<string> ImageSmall;
        public List<string> ImageLineup;
        public List<string> ImageLineupFallback;
        public List<string> Games;
        public List<string> StatCategories;
        public List<string> Injury;
        public List<string> Bio;
        public List<string> Notes;
        public List<string> Positions;
        public List<string> DepthChartPositions;
        public List<string> Team;
        public List<string> ColorPrimary;
        public List<string> ColorSecondary;
        public List<string> DraftTeam;
        public List<string> PrimaryPositionId;
        public List<string> PageMetadata;
        public List<string> WikiId;
        public List<string> TeamBios;
        public List<string> Videos;
    }

    public class Playerimage
    {
        [JsonProperty("nfl.p.32711")]
        public string NflP32711;
    }

    public class PlayerimageHeadshotLarge
    {
        [JsonProperty("nfl.p.32711")]
        public string NflP32711;
    }

    public class PlayerimageLarge
    {
        [JsonProperty("nfl.p.32711")]
        public string NflP32711;
    }

    public class PlayerimageLineup
    {
        [JsonProperty("nfl.p.32711")]
        public string NflP32711;
    }

    public class PlayerimageLineupFallback
    {
        [JsonProperty("nfl.p.32711")]
        public string NflP32711;
    }

    public class PlayerimageMedium
    {
        [JsonProperty("nfl.p.32711")]
        public string NflP32711;
    }

    public class PlayerimageSmall
    {
        [JsonProperty("nfl.p.32711")]
        public string NflP32711;
    }

    public class Players
    {
        [JsonProperty("nfl.p.32711")]
        public NflP32711 NflP32711 { get; set; }
    }

    public class Positions
    {
    }

    public class Root
    {
        public Playerimage Playerimage;
        public PlayerimageHeadshotLarge PlayerimageHeadshotLarge;
        public PlayerimageLarge PlayerimageLarge;
        public PlayerimageMedium PlayerimageMedium;
        public PlayerimageSmall PlayerimageSmall;
        public PlayerimageLineup PlayerimageLineup;
        public PlayerimageLineupFallback PlayerimageLineupFallback;
        public Positions Positions;
        public Players Players { get; set; }
        public League League;
    }

    public class Season
    {
        public string Year;
        public string DisplayYear;
        public string WeekNumber;
        public string SeasonCurrentDate;
        public string SeasonMonth;
        public string DisplaySchedulePeriod;
        public string DisplaySchedulePeriodId;
        public string CurrentPhase;
        public string CurrentSchedState;
        public CurrentStatState CurrentStatState;
    }

    public class SeasonPhases
    {
    }

    public class SeasonPhases2
    {
        public _1 _1;
        public _2 _2;
        public _3 _3;
        public _4 _4;
    }*/
}