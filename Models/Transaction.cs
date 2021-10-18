namespace YahooFantasyFootball.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Xml.Serialization;

    [XmlRoot(ElementName = "transaction_data")]
    public class TransactionData
    {
        [XmlElement(ElementName = "type")]
        public string Type { get; set; }
        [XmlElement(ElementName = "destination_team_key")]
        public string DestinationTeamKey { get; set; }
    }

    [XmlRoot(ElementName = "transaction")]
    public class Transaction
    {
        [XmlElement(ElementName = "type")]
        public string Type { get; set; }
        [XmlElement(ElementName = "faab_bid")]
        public string FaabBid { get; set; }
        [XmlElement(ElementName = "players")]
        public PlayerList PlayerList { get; set; }
    }
}
