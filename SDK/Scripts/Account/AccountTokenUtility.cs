using System;
using System.Reflection;
using MetaverseCloudEngine.ApiClient.Controllers;
using Newtonsoft.Json.Linq;

namespace MetaverseCloudEngine.Unity.Account
{
    /// <summary>
    /// Utilities for working with API client tokens from the Unity SDK.
    /// </summary>
    public static class AccountTokenUtility
    {
        private static readonly Lazy<PropertyInfo> AccessTokenExpirationProperty = new(() =>
        {
            try
            {
                return typeof(AccountController).GetProperty(
                    "AccessTokenExpiration",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            catch
            {
                return null;
            }
        });

        /// <summary>
        /// Best-effort read of the API client's access-token expiration (UTC).
        /// </summary>
        public static DateTime? GetApiClientAccessTokenExpirationUtc(AccountController account)
        {
            if (account == null)
                return null;

            var prop = AccessTokenExpirationProperty.Value;
            if (prop == null)
                return null;

            try
            {
                var value = (DateTime?)prop.GetValue(account);
                return NormalizeDateTimeToUtc(value);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Best-effort write of the API client's access-token expiration (UTC). Returns false if the
        /// underlying member is missing or inaccessible in this version of the API client.
        /// </summary>
        public static bool TrySetApiClientAccessTokenExpirationUtc(AccountController account, DateTime? expirationUtc)
        {
            if (account == null)
                return false;

            var prop = AccessTokenExpirationProperty.Value;
            if (prop == null)
                return false;

            try
            {
                var setter = prop.GetSetMethod(nonPublic: true);
                if (setter == null)
                    return false;

                var normalized = NormalizeDateTimeToUtc(expirationUtc);
                setter.Invoke(account, new object[] { normalized });
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Attempts to parse the UTC expiration from a JWT access token's <c>exp</c> claim.
        /// Returns null if the token does not look like a JWT or the payload cannot be parsed.
        /// </summary>
        public static DateTime? TryGetJwtExpirationUtc(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
                return null;

            try
            {
                var parts = accessToken.Split('.');
                if (parts.Length < 2)
                    return null;

                var payloadJson = Base64UrlDecodeToString(parts[1]);
                if (string.IsNullOrWhiteSpace(payloadJson))
                    return null;

                var payload = JToken.Parse(payloadJson);
                var expToken = payload.SelectToken("exp");
                if (expToken == null)
                    return null;

                long expSeconds;
                switch (expToken.Type)
                {
                    case JTokenType.Integer:
                        expSeconds = expToken.Value<long>();
                        break;
                    case JTokenType.String:
                        if (!long.TryParse(expToken.Value<string>(), out expSeconds))
                            return null;
                        break;
                    default:
                        return null;
                }

                return DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
            }
            catch
            {
                return null;
            }
        }

        private static string Base64UrlDecodeToString(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            try
            {
                var base64 = input.Replace('-', '+').Replace('_', '/');
                switch (base64.Length % 4)
                {
                    case 2:
                        base64 += "==";
                        break;
                    case 3:
                        base64 += "=";
                        break;
                }

                var bytes = Convert.FromBase64String(base64);
                return System.Text.Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return null;
            }
        }

        private static DateTime? NormalizeDateTimeToUtc(DateTime? dt)
        {
            if (!dt.HasValue)
                return null;

            var value = dt.Value;
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            };
        }
    }
}

