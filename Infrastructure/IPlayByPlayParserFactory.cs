namespace FantasyFootballStatTracker.Infrastructure
{
    using HtmlAgilityPack;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    interface IPlayByPlayParserFactory
    {
        public IPlayByPlayParser GetPlayByPlayParser(JObject playByPlayJsonObject, HtmlDocument playByPlayDoc);
    }
}