namespace FantasyFootballStatTracker.Infrastructure
{
    using HtmlAgilityPack;
    using Newtonsoft.Json.Linq;

    public class PlayByPlayParserFactory : IPlayByPlayParserFactory
    {
        /// <summary>
        /// If the play by play JSON object is null, we will be using the HTML parser.
        /// </summary>
        /// <param name="playByPlayJsonObject"></param>
        /// <returns></returns>
        public IPlayByPlayParser GetPlayByPlayParser(JObject playByPlayJsonObject, HtmlDocument playByPlayDoc)
        {
            if (playByPlayJsonObject != null)
            {
                return new PlayByPlayJsonParser(playByPlayJsonObject);
            }
            else
            {
                return new PlayByPlayHtmlParser(playByPlayDoc);
            }
        }
    }
}