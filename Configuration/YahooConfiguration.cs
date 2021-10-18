namespace YahooFantasyFootball.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class YahooConfiguration
    {
        /// <summary>
        /// Client Id from Yahoo
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// Client Secret From Yahoo
        /// </summary>
        public string ClientSecret { get; set; }

        /// <summary>
        /// Redirect Uri Specified. This will be redirected back to from Yahoo Auth Flow
        /// </summary>
        public string RedirectUri { get; set; }
    }
}