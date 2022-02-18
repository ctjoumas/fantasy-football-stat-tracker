namespace FantasyFootballStatTracker.Infrastructure
{
    using Microsoft.AspNetCore.Http;
    using Newtonsoft.Json;

    public static class SessionExtensions
    {
        /// <summary>
        /// Session key for the Logos; this is not a dynamic call to the database, so store it in a session
        /// to save multiple
        /// </summary>
        public const string SessionKeyLogos = "_Logos";

        public static void SetObjectAsJson(this ISession session, string key, object value)
        {
            session.SetString(key, JsonConvert.SerializeObject(value));
        }

        public static T GetObjectFromJson<T>(this ISession session, string key)
        {
            string value = session.GetString(key);
            return value == null ? default(T) : JsonConvert.DeserializeObject<T>(value);
        }
    }
}