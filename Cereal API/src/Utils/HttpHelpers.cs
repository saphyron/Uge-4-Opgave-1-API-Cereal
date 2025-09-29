using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using CerealAPI.Models;

namespace CerealAPI.Utils
{
    /// <summary>
    /// Hjælpere til sikker URL-opbygning og HTTP-kald.
    /// - Encoder path-segmenter korrekt (Uri.EscapeDataString)
    /// - Opbygger querystrings
    /// - Convenience metoder til Cereal-endpoints
    /// </summary>
    public static class HttpHelpers
    {
        /// <summary>
        /// URL-encoder ét path-segment (fx "100% Bran" -> "100%25%20Bran").
        /// Brug altid per segment – ikke på hele URL'er i ét hug.
        /// </summary>
        public static string EncodePathSegment(string segment)
            => Uri.EscapeDataString(segment ?? string.Empty);

        /// <summary>
        /// Bygger en path med allerede encodede segmenter.
        /// </summary>
        public static string CombinePath(params string[] segments)
        {
            var sb = new StringBuilder();
            foreach (var raw in segments)
            {
                if (string.IsNullOrEmpty(raw)) continue;
                // Sørg for én enkelt '/' mellem segmenter
                if (sb.Length == 0 || sb[^1] != '/')
                    sb.Append('/');

                // Fjern evt. leading '/' i segment for at undgå dobbelte skråstreger
                var seg = raw[0] == '/' ? raw.Substring(1) : raw;
                sb.Append(seg);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Byg querystring fra key/value (værdier URL-encodes).
        /// </summary>
        public static string BuildQuery(IDictionary<string, object?>? query)
        {
            if (query == null || query.Count == 0) return string.Empty;
            var sb = new StringBuilder("?");
            bool first = true;
            foreach (var kv in query)
            {
                if (!first) sb.Append('&'); else first = false;
                sb.Append(Uri.EscapeDataString(kv.Key));
                sb.Append('=');
                sb.Append(Uri.EscapeDataString(kv.Value?.ToString() ?? string.Empty));
            }
            return sb.ToString();
        }

        // ------------ Cereal helpers (strongly-typed) ------------

        /// <summary>
        /// /cereals/{name}/{mfr}/{type} – med korrekt encoding pr. segment.
        /// </summary>
        public static string CerealKeyPath(string name, string mfr, string type)
            => CombinePath("cereals",
                           EncodePathSegment(name),
                           EncodePathSegment(mfr),
                           EncodePathSegment(type));

        // ------------ (Optional) HttpClient convenience ------------

        public static async System.Threading.Tasks.Task<HttpResponseMessage>
            PutCerealAsync(this HttpClient http, string baseAddress, Cereal cerealKey, Cereal update)
        {
            var path = CerealKeyPath(cerealKey.name, cerealKey.mfr, cerealKey.type);
            var uri = new Uri(new Uri(baseAddress, UriKind.Absolute), path);
            return await http.PutAsJsonAsync(uri, update);
        }

        public static async System.Threading.Tasks.Task<HttpResponseMessage>
            DeleteCerealAsync(this HttpClient http, string baseAddress, string name, string mfr, string type)
        {
            var path = CerealKeyPath(name, mfr, type);
            var uri = new Uri(new Uri(baseAddress, UriKind.Absolute), path);
            return await http.DeleteAsync(uri);
        }
    }
}
