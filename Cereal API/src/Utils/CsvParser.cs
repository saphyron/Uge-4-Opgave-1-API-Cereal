using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CerealAPI.Models;

namespace CerealAPI.Utils
{
    public static class CsvParser
    {
        // Læser semikolon-CSV, springer 2. linje (datatype-linje) over.
        // -1 => NULL for talfelter.
        public static List<Cereal> ParseCereal(Stream csvStream)
        {
            var list = new List<Cereal>();
            using var reader = new StreamReader(csvStream);

            string? header = reader.ReadLine();     // header
            if (header == null) return list;

            _ = reader.ReadLine();                  // type-linje (skip)

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var p = line.Split(';');
                string Get(int i) => i < p.Length ? p[i].Trim() : "";

                var cereal = new Cereal
                {
                    name     = Get(0),
                    mfr      = Get(1),
                    type     = Get(2),
                    calories = ToNullableInt(Get(3)),
                    protein  = ToNullableInt(Get(4)),
                    fat      = ToNullableInt(Get(5)),
                    sodium   = ToNullableInt(Get(6)),
                    fiber    = ToNullableDouble(Get(7)),
                    carbo    = ToNullableDouble(Get(8)),
                    sugars   = ToNullableInt(Get(9)),
                    potass   = ToNullableInt(Get(10)),
                    vitamins = ToNullableInt(Get(11)),
                    shelf    = ToNullableInt(Get(12)),
                    weight   = ToNullableDouble(Get(13)),
                    cups     = ToNullableDouble(Get(14)),
                    rating   = ToNullableString(Get(15))
                };

                list.Add(cereal);
            }

            return list;
        }

        static int? ToNullableInt(string s)
        {
            if (string.IsNullOrWhiteSpace(s) || s == "-1") return null;
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : (int?)null;
        }

        static double? ToNullableDouble(string s)
        {
            if (string.IsNullOrWhiteSpace(s) || s == "-1") return null;

            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var inv))
                return inv;

            if (double.TryParse(s, NumberStyles.Float, new CultureInfo("da-DK"), out var dk))
                return dk;

            return null;
        }

        static string? ToNullableString(string s)
        {
            if (string.IsNullOrWhiteSpace(s) || s == "-1") return null;
            return s.Trim();
        }
    }
}
