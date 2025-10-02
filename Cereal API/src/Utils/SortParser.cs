// src/Utils/SortParser.cs
using Microsoft.AspNetCore.Http;

namespace CerealAPI.Utils
{
    public static class SortParser
    {
        // whitelist af kolonner der må sorteres på
        private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
        {
            "name","mfr","type","calories","protein","fat","sugars","rating","fiber","carbo","sodium","potass","vitamins","shelf","weight","cups"
        };

        /// <summary>
        /// Læser ?sort=calories_desc,name_asc og bygger en ORDER BY streng.
        /// Returnerer både SQL og parse-dele for evt. debugging/tests.
        /// </summary>
        public static (string orderBy, List<(string col, bool desc)> parts) BuildOrderBy(IQueryCollection q)
        {
            var sort = q["sort"].ToString();
            var parts = new List<(string col, bool desc)>();

            if (!string.IsNullOrWhiteSpace(sort))
            {
                foreach (var token in sort.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var t = token.ToLowerInvariant();
                    var desc = t.EndsWith("_desc", StringComparison.Ordinal);
                    var col = t.Replace("_desc", "").Replace("_asc", "");
                    if (Allowed.Contains(col))
                        parts.Add((col, desc));
                }
            }

            if (parts.Count == 0)
                parts.Add(("name", false)); // default

            var order = "ORDER BY " + string.Join(", ", parts.Select(p => $"{p.col} {(p.desc ? "DESC" : "ASC")}"));
            return (order, parts);
        }
    }
}
