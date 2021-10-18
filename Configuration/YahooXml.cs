namespace YahooFantasyFootball.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Xml.Linq;

    internal static class YahooXml
    {
        /// <summary>
        /// Xml Namespace for Yahoo Fantasy XML Returned
        /// </summary>
        internal static XNamespace XMLNS = "http://fantasysports.yahooapis.com/fantasy/v2/base.rng";

        internal static XNamespace XMLNS_V1_NoWWW = "http://yahooapis.com/v1/base.rng";

        internal static XNamespace XMLNS_V1 = "http://yahooapis.com/v1/base.rng";
    }
}
