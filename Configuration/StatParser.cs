namespace YahooFantasyFootball.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class StatParser
    {
        public static double? Parse(string value)
        {
            if (value == "-")
                return null;
            if (value.Contains(":"))
            {
                //minutes:seconds will return as minutes
                value = value.Replace(",", "");
                var split = value.Split(':');
                return double.Parse(split[0]) + (double.Parse(split[1]) / 60);
            }

            return double.Parse(value);
        }
    }
}
