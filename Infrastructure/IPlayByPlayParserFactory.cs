namespace FantasyFootballStatTracker.Infrastructure
{
    using HtmlAgilityPack;
    using Newtonsoft.Json.Linq;

    interface IPlayByPlayParserFactory
    {
        public IPlayByPlayParser GetPlayByPlayParser(JObject playByPlayJsonObject, HtmlDocument playByPlayDoc);
    }
}