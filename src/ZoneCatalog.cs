using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TimeZoneWidgets
{
    public class ZoneEntry
    {
        public readonly string City;
        /// <summary>Three-letter city code (IATA metropolitan code where one exists: NYC, TPE, AMS).</summary>
        public readonly string Code;
        public readonly string WindowsId;
        /// <summary>Approximate city coordinates, used to mark the city on the globe.</summary>
        public readonly double Latitude;
        public readonly double Longitude;

        public ZoneEntry(string city, string code, string windowsId, double latitude, double longitude)
        {
            City = city;
            Code = code;
            WindowsId = windowsId;
            Latitude = latitude;
            Longitude = longitude;
        }
    }

    public class ZoneRegion
    {
        public readonly string Name;
        public readonly ZoneEntry[] Zones;

        public ZoneRegion(string name, params ZoneEntry[] zones)
        {
            Name = name;
            Zones = zones;
        }
    }

    /// <summary>
    /// A curated list of popular cities mapped to Windows time zone IDs, plus helpers over the
    /// full Windows time zone database (TimeZoneInfo), which works fully offline and is DST-aware.
    /// </summary>
    public static class ZoneCatalog
    {
        public static readonly ZoneRegion[] Regions = new ZoneRegion[]
        {
            new ZoneRegion("Americas",
                new ZoneEntry("Honolulu", "HNL", "Hawaiian Standard Time", 21.31, -157.86),
                new ZoneEntry("Anchorage", "ANC", "Alaskan Standard Time", 61.22, -149.9),
                new ZoneEntry("Los Angeles", "LAX", "Pacific Standard Time", 34.05, -118.24),
                new ZoneEntry("Vancouver", "YVR", "Pacific Standard Time", 49.28, -123.12),
                new ZoneEntry("Denver", "DEN", "Mountain Standard Time", 39.74, -104.99),
                new ZoneEntry("Phoenix", "PHX", "US Mountain Standard Time", 33.45, -112.07),
                new ZoneEntry("Chicago", "CHI", "Central Standard Time", 41.88, -87.63),
                new ZoneEntry("Mexico City", "MEX", "Central Standard Time (Mexico)", 19.43, -99.13),
                new ZoneEntry("New York", "NYC", "Eastern Standard Time", 40.71, -74.01),
                new ZoneEntry("Toronto", "YTO", "Eastern Standard Time", 43.65, -79.38),
                new ZoneEntry("Bogotá", "BOG", "SA Pacific Standard Time", 4.71, -74.07),
                new ZoneEntry("Lima", "LIM", "SA Pacific Standard Time", -12.05, -77.04),
                new ZoneEntry("Halifax", "YHZ", "Atlantic Standard Time", 44.65, -63.57),
                new ZoneEntry("Santiago", "SCL", "Pacific SA Standard Time", -33.45, -70.67),
                new ZoneEntry("São Paulo", "SAO", "E. South America Standard Time", -23.55, -46.63),
                new ZoneEntry("Buenos Aires", "BUE", "Argentina Standard Time", -34.6, -58.38)),
            new ZoneRegion("Europe & Africa",
                new ZoneEntry("UTC", "UTC", "UTC", 0, 0),
                new ZoneEntry("London", "LON", "GMT Standard Time", 51.51, -0.13),
                new ZoneEntry("Dublin", "DUB", "GMT Standard Time", 53.35, -6.26),
                new ZoneEntry("Lisbon", "LIS", "GMT Standard Time", 38.72, -9.14),
                new ZoneEntry("Lagos", "LOS", "W. Central Africa Standard Time", 6.52, 3.38),
                new ZoneEntry("Paris", "PAR", "Romance Standard Time", 48.86, 2.35),
                new ZoneEntry("Madrid", "MAD", "Romance Standard Time", 40.42, -3.7),
                new ZoneEntry("Berlin", "BER", "W. Europe Standard Time", 52.52, 13.41),
                new ZoneEntry("Amsterdam", "AMS", "W. Europe Standard Time", 52.37, 4.9),
                new ZoneEntry("Rome", "ROM", "W. Europe Standard Time", 41.9, 12.5),
                new ZoneEntry("Stockholm", "STO", "W. Europe Standard Time", 59.33, 18.07),
                new ZoneEntry("Warsaw", "WAW", "Central European Standard Time", 52.23, 21.01),
                new ZoneEntry("Athens", "ATH", "GTB Standard Time", 37.98, 23.73),
                new ZoneEntry("Cairo", "CAI", "Egypt Standard Time", 30.04, 31.24),
                new ZoneEntry("Johannesburg", "JNB", "South Africa Standard Time", -26.2, 28.05),
                new ZoneEntry("Nairobi", "NBO", "E. Africa Standard Time", -1.29, 36.82),
                new ZoneEntry("Istanbul", "IST", "Turkey Standard Time", 41.01, 28.98),
                new ZoneEntry("Moscow", "MOW", "Russian Standard Time", 55.76, 37.62)),
            new ZoneRegion("Middle East & Asia",
                new ZoneEntry("Jerusalem", "JRS", "Israel Standard Time", 31.77, 35.21),
                new ZoneEntry("Riyadh", "RUH", "Arab Standard Time", 24.71, 46.68),
                new ZoneEntry("Dubai", "DXB", "Arabian Standard Time", 25.2, 55.27),
                new ZoneEntry("Tehran", "THR", "Iran Standard Time", 35.69, 51.39),
                new ZoneEntry("Karachi", "KHI", "Pakistan Standard Time", 24.86, 67.0),
                new ZoneEntry("Mumbai", "BOM", "India Standard Time", 19.08, 72.88),
                new ZoneEntry("New Delhi", "DEL", "India Standard Time", 28.61, 77.21),
                new ZoneEntry("Kathmandu", "KTM", "Nepal Standard Time", 27.72, 85.32),
                new ZoneEntry("Dhaka", "DAC", "Bangladesh Standard Time", 23.81, 90.41),
                new ZoneEntry("Bangkok", "BKK", "SE Asia Standard Time", 13.76, 100.5),
                new ZoneEntry("Jakarta", "JKT", "SE Asia Standard Time", -6.21, 106.85),
                new ZoneEntry("Singapore", "SIN", "Singapore Standard Time", 1.35, 103.82),
                new ZoneEntry("Manila", "MNL", "Singapore Standard Time", 14.6, 120.98),
                new ZoneEntry("Hong Kong", "HKG", "China Standard Time", 22.32, 114.17),
                new ZoneEntry("Shanghai", "SHA", "China Standard Time", 31.23, 121.47),
                new ZoneEntry("Taipei", "TPE", "Taipei Standard Time", 25.03, 121.57),
                new ZoneEntry("Seoul", "SEL", "Korea Standard Time", 37.57, 126.98),
                new ZoneEntry("Tokyo", "TYO", "Tokyo Standard Time", 35.68, 139.69)),
            new ZoneRegion("Australia & Pacific",
                new ZoneEntry("Perth", "PER", "W. Australia Standard Time", -31.95, 115.86),
                new ZoneEntry("Adelaide", "ADL", "Cen. Australia Standard Time", -34.93, 138.6),
                new ZoneEntry("Brisbane", "BNE", "E. Australia Standard Time", -27.47, 153.03),
                new ZoneEntry("Sydney", "SYD", "AUS Eastern Standard Time", -33.87, 151.21),
                new ZoneEntry("Melbourne", "MEL", "AUS Eastern Standard Time", -37.81, 144.96),
                new ZoneEntry("Auckland", "AKL", "New Zealand Standard Time", -36.85, 174.76),
                new ZoneEntry("Fiji", "FJI", "Fiji Standard Time", -18.14, 178.44))
        };

        /// <summary>Popular zones as a flat list, in catalog order.</summary>
        public static IEnumerable<ZoneEntry> Popular
        {
            get
            {
                foreach (var region in Regions)
                    foreach (var zone in region.Zones)
                        yield return zone;
            }
        }

        /// <summary>Every zone Windows knows about, ordered by UTC offset.</summary>
        public static ReadOnlyCollection<TimeZoneInfo> All
        {
            get { return TimeZoneInfo.GetSystemTimeZones(); }
        }

        public static bool Exists(string windowsId)
        {
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Resolve a Windows zone ID, falling back to the local zone if it is unknown.</summary>
        public static TimeZoneInfo Find(string windowsId)
        {
            if (!string.IsNullOrEmpty(windowsId))
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(windowsId); }
                catch { }
            }
            return TimeZoneInfo.Local;
        }

        /// <summary>A short, human label for a zone: the catalog city if we have one, else the first city in Windows' display name.</summary>
        public static string LabelFor(string windowsId)
        {
            foreach (var zone in Popular)
                if (string.Equals(zone.WindowsId, windowsId, StringComparison.OrdinalIgnoreCase))
                    return zone.City;

            var tz = Find(windowsId);
            return ShortDisplayName(tz);
        }

        /// <summary>The city to show for the PC's own time zone (works offline: it is derived from the
        /// Windows time-zone setting, e.g. "Taipei Standard Time" gives "Taipei").</summary>
        public static string LocalCity()
        {
            return LabelFor(TimeZoneInfo.Local.Id);
        }

        /// <summary>Three-letter code for a widget: the catalog code when the label is a catalog city
        /// (matched by name first, then by zone), otherwise the label itself if it is already short,
        /// else its first three letters in capitals (so a custom "Grandma" becomes "GRA").</summary>
        public static string CodeFor(string windowsId, string label)
        {
            if (!string.IsNullOrEmpty(label))
                foreach (var zone in Popular)
                    if (string.Equals(zone.City, label, StringComparison.OrdinalIgnoreCase)) return zone.Code;
            var entry = EntryFor(windowsId, label);
            if (entry != null && (string.IsNullOrEmpty(label) || string.Equals(label, LabelFor(windowsId), StringComparison.OrdinalIgnoreCase)))
                return entry.Code;
            if (string.IsNullOrEmpty(label)) return entry != null ? entry.Code : "---";
            if (label.Length <= 4) return label.ToUpperInvariant();
            var sb = new System.Text.StringBuilder();
            foreach (char ch in label.Normalize(System.Text.NormalizationForm.FormD))
            {
                if (char.IsLetterOrDigit(ch)) sb.Append(char.ToUpperInvariant(ch));
                if (sb.Length == 3) break;
            }
            return sb.Length > 0 ? sb.ToString() : label;
        }

        public static string ShortDisplayName(TimeZoneInfo tz)
        {
            var name = tz.DisplayName ?? tz.Id;
            // Windows display names look like "(UTC+01:00) Amsterdam, Berlin, Bern, Rome".
            int close = name.IndexOf(") ", StringComparison.Ordinal);
            if (name.StartsWith("(") && close > 0) name = name.Substring(close + 2);
            int comma = name.IndexOf(',');
            if (comma > 0) name = name.Substring(0, comma);
            return name.Trim();
        }

        /// <summary>The catalog entry for a zone (first match), or null if the zone is not in the catalog.</summary>
        public static ZoneEntry EntryFor(string windowsId, string label)
        {
            ZoneEntry byId = null;
            foreach (var zone in Popular)
            {
                if (!string.Equals(zone.WindowsId, windowsId, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrEmpty(label) && string.Equals(zone.City, label, StringComparison.OrdinalIgnoreCase)) return zone;
                if (byId == null) byId = zone;
            }
            return byId;
        }

        /// <summary>Formats an offset like "UTC+5:30", "UTC−7" or "UTC".</summary>
        public static string FormatOffset(TimeSpan offset)
        {
            if (offset == TimeSpan.Zero) return "UTC";
            string sign = offset < TimeSpan.Zero ? "−" : "+";
            var abs = offset.Duration();
            if (abs.Minutes == 0) return "UTC" + sign + abs.Hours;
            return "UTC" + sign + abs.Hours + ":" + abs.Minutes.ToString("00");
        }
    }
}
