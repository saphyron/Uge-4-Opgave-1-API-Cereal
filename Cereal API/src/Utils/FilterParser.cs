// Utils/FilterParser.cs
using System.Web;
using System.Text.RegularExpressions;
using Dapper;

namespace CerealAPI.Utils;

public static class FilterParser
{
    // whitelist: query-felt -> DB-kolonne
    private static readonly Dictionary<string, string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = "name",
        ["mfr"] = "mfr",
        ["type"] = "type",
        ["calories"] = "calories",
        ["protein"] = "protein",
        ["fat"] = "fat",
        ["sodium"] = "sodium",
        ["fiber"] = "fiber",
        ["carbo"] = "carbo",
        ["sugars"] = "sugars",
        ["potass"] = "potass",
        ["vitamins"] = "vitamins",
        ["shelf"] = "shelf",
        ["weight"] = "weight",
        ["cups"] = "cups",
        ["rating"] = "rating"
    };

    // Op-map + aliaser
    private static readonly Dictionary<string, string> OpMap = new()
    {
        ["="] = "=",
        ["!="] = "!=",
        [">"] = ">",
        [">="] = ">=",
        ["<"] = "<",
        ["<="] = "<="
    };

    // Regex der fanger fx calories>=100  eller  name=All-Bran
    private static readonly Regex RawExpr = new(@"(?<f>[a-zA-Z_]\w*)\s*(?<op>!=|>=|<=|=|>|<)\s*(?<v>[^&]+)",
        RegexOptions.Compiled);

    public static (string whereSql, DynamicParameters bind) BuildWhere(string? rawQuery, IQueryCollection q)
    {
        var dp = new DynamicParameters();
        var whereParts = new List<string>();
        int i = 0;

        // 1) Raw‐mode: parse hele querystringen (URL-decoded)
        if (!string.IsNullOrEmpty(rawQuery))
        {
            var decoded = HttpUtility.UrlDecode(rawQuery.TrimStart('?'));
            foreach (Match m in RawExpr.Matches(decoded))
            {
                var f = m.Groups["f"].Value;
                var op = m.Groups["op"].Value;
                var v = m.Groups["v"].Value;

                if (!Allowed.TryGetValue(f, out var col)) continue;
                if (!OpMap.TryGetValue(op, out var sqlOp)) continue;

                var p = $"@p{i++}";
                whereParts.Add($"{col} {sqlOp} {p}");
                dp.Add(p, TryConvert(v));
            }
        }

        // 2) Alias‐mode: calories_gte=100, calories_lte=200, name_eq=All-Bran, rating_neq=50
        var aliasOps = new (string suffix, string op)[] {
            ("_gte", ">="), ("_lte", "<="), ("_gt", ">"), ("_lt", "<"), ("_eq", "="), ("_neq", "!=")
        };

        foreach (var kv in q)
        {
            var key = kv.Key;
            var val = kv.Value.ToString();
            foreach (var (sfx, op) in aliasOps)
            {
                if (key.EndsWith(sfx, StringComparison.OrdinalIgnoreCase))
                {
                    var field = key[..^sfx.Length];
                    if (!Allowed.TryGetValue(field, out var col)) break;

                    var p = $"@p{i++}";
                    whereParts.Add($"{col} {op} {p}");
                    dp.Add(p, TryConvert(val));
                    break;
                }
            }
        }

        var whereSql = whereParts.Count > 0 ? "WHERE " + string.Join(" AND ", whereParts) : "";
        return (whereSql, dp);
    }

    private static object TryConvert(string s)
    {
        // Prøv int/double, ellers brug string
        if (int.TryParse(s, out var i)) return i;
        if (double.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d)) return d;
        return s;
    }
}
