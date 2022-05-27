namespace FantasyFootballStatTracker.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class AppConfiguration
    {
        /// <summary>
        /// The current NFL season, set in the appsettings configuration file.
        /// </summary>
        public int Season { get; set; }
    }
}