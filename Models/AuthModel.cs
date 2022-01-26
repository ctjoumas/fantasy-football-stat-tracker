namespace FantasyFootballStatTracker.Models
{
    using System;

    public static class AuthModel
    {
        /// <summary>
        /// Access token we can use to query the API.
        /// </summary>
        public static string AccessToken { get; set; }

        /// <summary>
        /// Refresh token returned by provider. Can be used for further calls of provider API.
        /// </summary>
        public static string RefreshToken { get; set; }

        /// <summary>
        /// Token type returned by provider. Can be used for further calls of provider API.
        /// </summary>
        public static string TokenType { get; set; }

        /// <summary>
        /// Seconds till the token expires returned by provider. Can be used for further calls of provider API.
        /// </summary>
        public static DateTime ExpiresAt { get; set; }

        /// <summary>
        /// This used to be the user GUID (xoauth_yahoo_guid) but that is deprecated; instead, the auth sends
        /// back the id_token.
        /// </summary>
        public static string TokenId { get; set; }
    }
}