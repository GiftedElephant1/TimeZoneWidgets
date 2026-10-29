using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TimeZoneWidgets
{
    public class ZoneEntry
    {
        public readonly string City;
        public readonly string WindowsId;
        /// <summary>Approximate city coordinates, used to mark the city on the globe.</summary>
        public readonly double Latitude;
        public readonly double Longitude;

        public ZoneEntry(string city, string windowsId, double latitude, double longitude)
        {
            City = city;
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
                new ZoneEntry("Honolulu", "Hawaiian Standard Time", 21.31, -157.86),
                new ZoneEntry("Anchorage", "Alaskan Standard Time", 61.22, -149.9),
                new ZoneEntry("Los Angeles", "Pacific Standard Time", 34.05, -118.24),
                new ZoneEntry("Vancouver", "Pacific Standard Time", 49.28, -123.12),
                new ZoneEntry("Denver", "Mountain Standard Time", 39.74, -104.99),
                new ZoneEntry("Phoenix", "US Mountain Standard Time", 33.45, -112.07),
                new ZoneEntry("Chicago", "Central Standard Time", 41.88, -87.63),
                new ZoneEntry("Mexico City", "Central Standard Time (Mexico)", 19.43, -99.13),
                new ZoneEntry("New York", "Eastern Standard Time", 40.71, -74.01),
                new ZoneEntry("Toronto", "Eastern Standard Time", 43.65, -79.38),
                new ZoneEntry("Bogotá", "SA Pacific Standard Time", 4.71, -74.07),
                new ZoneEntry("Lima", "SA Pacific Standard Time", -12.05, -77.04),
                new ZoneEntry("Halifax", "Atlantic Standard Time", 44.65, -63.57),
                new ZoneEntry("Santiago", "Pacific SA Standard Time", -33.45, -70.67),
                new ZoneEntry("São Paulo", "E. South America Standard Time", -23.55, -46.63),
                new ZoneEntry("Buenos Aires", "Argentina Standard Time", -34.6, -58.38)),
            new ZoneRegion("Europe & Africa",
                new ZoneEntry("UTC", "UTC", 0, 0),
                new ZoneEntry("London", "GMT Standard Time", 51.51, -0.13),
                new ZoneEntry("Dublin", "GMT Standard Time", 53.35, -6.26),
                new ZoneEntry("Lisbon", "GMT Standard Time", 38.72, -9.14),
                new ZoneEntry("Lagos", "W. Central Africa Standard Time", 6.52, 3.38),
                new ZoneEntry("Paris", "Romance Standard Time", 48.86, 2.35),
                new ZoneEntry("Madrid", "Romance Standard Time", 40.42, -3.7),
                new ZoneEntry("Berlin", "W. Europe Standard Time", 52.52, 13.41),
                new ZoneEntry("Amsterdam", "W. Europe Standard Time", 52.37, 4.9),
                new ZoneEntry("Rome", "W. Europe Standard Time", 41.9, 12.5),
                new ZoneEntry("Stockholm", "W. Europe Standard Time", 59.33, 18.07),
                new ZoneEntry("Warsaw", "Central European Standard Time", 52.23, 21.01),
                new ZoneEntry("Athens", "GTB Standard Time", 37.98, 23.73),
                new ZoneEntry("Cairo", "Egypt Standard Time", 30.04, 31.24),
                new ZoneEntry("Johannesburg", "South Africa Standard Time", -26.2, 28.05),
                new ZoneEntry("Nairobi", "E. Africa Standard Time", -1.29, 36.82),
                new ZoneEntry("Istanbul", "Turkey Standard Time", 41.01, 28.98),
                new ZoneEntry("Moscow", "Russian Standard Time", 55.76, 37.62)),
            new ZoneRegion("Middle East & Asia",
                new ZoneEntry("Jerusalem", "Israel Standard Time", 31.77, 35.21),
                new ZoneEntry("Riyadh", "Arab Standard Time", 24.71, 46.68),
                new ZoneEntry("Dubai", "Arabian Standard Time", 25.2, 55.27),
                new ZoneEntry("Tehran", "Iran Standard Time", 35.69, 51.39),
                new ZoneEntry("Karachi", "Pakistan Standard Time", 24.86, 67.0),
                new ZoneEntry("Mumbai", "India Standard Time", 19.08, 72.88),
                new ZoneEntry("New Delhi", "India Standard Time", 28.61, 77.21),
                new ZoneEntry("Kathmandu", "Nepal Standard Time", 27.72, 85.32),
                new ZoneEntry("Dhaka", "Bangladesh Standard Time", 23.81, 90.41),
                new ZoneEntry("Bangkok", "SE Asia Standard Time", 13.76, 100.5),
                new ZoneEntry("Jakarta", "SE Asia Standard Time", -6.21, 106.85),
                new ZoneEntry("Singapore", "Singapore Standard Time", 1.35, 103.82),
                new ZoneEntry("Manila", "Singapore Standard Time", 14.6, 120.98),
                new ZoneEntry("Hong Kong", "China Standard Time", 22.32, 114.17),
                new ZoneEntry("Shanghai", "China Standard Time", 31.23, 121.47),
                new ZoneEntry("Taipei", "Taipei Standard Time", 25.03, 121.57),
                new ZoneEntry("Seoul", "Korea Standard Time", 37.57, 126.98),
                new ZoneEntry("Tokyo", "Tokyo Standard Time", 35.68, 139.69)),
            new ZoneRegion("Australia & Pacific",
                new ZoneEntry("Perth", "W. Australia Standard Time", -31.95, 115.86),
                new ZoneEntry("Adelaide", "Cen. Australia Standard Time", -34.93, 138.6),
                new ZoneEntry("Brisbane", "E. Australia Standard Time", -27.47, 153.03),
                new ZoneEntry("Sydney", "AUS Eastern Standard Time", -33.87, 151.21),
                new ZoneEntry("Melbourne", "AUS Eastern Standard Time", -37.81, 144.96),
                new ZoneEntry("Auckland", "New Zealand Standard Time", -36.85, 174.76),
                new ZoneEntry("Fiji", "Fiji Standard Time", -18.14, 178.44))
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
