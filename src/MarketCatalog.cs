using System;
using System.Collections.Generic;

namespace TimeZoneWidgets
{
    /// <summary>A stock exchange: its own time zone, trading days and sessions (local minutes).</summary>
    public class Market
    {
        public readonly string Name;
        public readonly string WindowsId;
        public readonly bool[] Days;        // indexed by DayOfWeek (Sunday = 0)
        public readonly int[][] Sessions;   // { openMinute, closeMinute } pairs, in exchange-local time

        public Market(string name, string windowsId, string days, params int[] hhmm)
        {
            Name = name;
            WindowsId = windowsId;
            Days = new bool[7];
            switch (days)
            {
                case "SunThu": for (int d = 0; d <= 4; d++) Days[d] = true; break;
                case "SatWed": Days[6] = true; for (int d = 0; d <= 3; d++) Days[d] = true; break;
                default: for (int d = 1; d <= 5; d++) Days[d] = true; break;   // Mon–Fri
            }
            Sessions = new int[hhmm.Length / 2][];
            for (int i = 0; i < Sessions.Length; i++)
                Sessions[i] = new int[] { (hhmm[2 * i] / 100) * 60 + hhmm[2 * i] % 100, (hhmm[2 * i + 1] / 100) * 60 + hhmm[2 * i + 1] % 100 };
        }
    }

    /// <summary>Where a market stands right now and how long until it opens or closes.</summary>
    public struct MarketStatus
    {
        public Market Market;
        public bool Open;          // a trading session is in progress
        public bool Break;         // between two sessions of the same day (lunch)
        public TimeSpan Until;     // time until the close (if open) or the next open (if closed)
        public bool Known { get { return Market != null; } }

        /// <summary>0 = nothing special, 1 = open, 2 = opens within the hour, 3 = open and closes within the hour.</summary>
        public int Phase
        {
            get
            {
                if (Market == null) return 0;
                bool soon = Until <= TimeSpan.FromHours(1);
                if (Open) return soon ? 3 : 1;
                return soon ? 2 : 0;
            }
        }

        /// <summary>Translucent tint for a phase: green open, pale yellow before the open, pale red before the close.</summary>
        public static System.Drawing.Color PhaseTint(int phase, bool darkPanel)
        {
            switch (phase)
            {
                case 1: return System.Drawing.Color.FromArgb(darkPanel ? 75 : 60, 40, 175, 95);
                case 2: return System.Drawing.Color.FromArgb(darkPanel ? 80 : 95, 250, 205, 50);
                case 3: return System.Drawing.Color.FromArgb(darkPanel ? 85 : 70, 235, 70, 60);
                default: return System.Drawing.Color.Transparent;
            }
        }

        /// <summary>Text colour matching <see cref="PhaseTint"/>, readable on a dark or light panel.</summary>
        public static System.Drawing.Color PhaseText(int phase, bool darkPanel)
        {
            switch (phase)
            {
                case 1: return darkPanel ? System.Drawing.Color.FromArgb(135, 235, 165) : System.Drawing.Color.FromArgb(14, 112, 52);
                case 2: return darkPanel ? System.Drawing.Color.FromArgb(255, 220, 120) : System.Drawing.Color.FromArgb(140, 95, 0);
                case 3: return darkPanel ? System.Drawing.Color.FromArgb(255, 150, 140) : System.Drawing.Color.FromArgb(165, 30, 30);
                default: return System.Drawing.Color.Empty;
            }
        }

        /// <summary>"2h 05m", "3m", "2d 9h 14m" – always to the minute.</summary>
        public string Countdown
        {
            get
            {
                var t = Until;
                if (t < TimeSpan.Zero) t = TimeSpan.Zero;
                int totalMin = (int)Math.Ceiling(t.TotalMinutes);
                int d = totalMin / 1440, h = (totalMin % 1440) / 60, m = totalMin % 60;
                if (d > 0) return d + "d " + h + "h " + m.ToString("00") + "m";
                if (h > 0) return h + "h " + m.ToString("00") + "m";
                return m + "m";
            }
        }
    }

    /// <summary>
    /// The main stock exchange for each catalog country (with a few city-level exceptions) and
    /// their regular trading hours. Regular hours only: public holidays and half days are not
    /// modelled, and hours are those in force as of late 2026. Works offline.
    /// </summary>
    public static class MarketCatalog
    {
        static readonly Dictionary<string, Market> ByCountry = new Dictionary<string, Market>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, Market> ByCity = new Dictionary<string, Market>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, Market> ByZone = new Dictionary<string, Market>(StringComparer.OrdinalIgnoreCase);

        static Market Add(string country, Market m) { ByCountry[country] = m; return m; }

        static MarketCatalog()
        {
            // ---- Americas ----
            var nyse = Add("United States", new Market("NYSE", "Eastern Standard Time", "MonFri", 930, 1600));
            ByCountry["Puerto Rico"] = nyse;
            Add("Canada", new Market("TSX", "Eastern Standard Time", "MonFri", 930, 1600));
            Add("Mexico", new Market("BMV", "Central Standard Time (Mexico)", "MonFri", 830, 1500));
            Add("Brazil", new Market("B3", "E. South America Standard Time", "MonFri", 1000, 1700));
            Add("Argentina", new Market("BYMA", "Argentina Standard Time", "MonFri", 1100, 1700));
            Add("Chile", new Market("BCS", "Pacific SA Standard Time", "MonFri", 930, 1600));
            Add("Colombia", new Market("BVC", "SA Pacific Standard Time", "MonFri", 930, 1600));
            Add("Peru", new Market("BVL", "SA Pacific Standard Time", "MonFri", 900, 1600));
            Add("Jamaica", new Market("JSE", "SA Pacific Standard Time", "MonFri", 900, 1300));
            Add("Costa Rica", new Market("BNV", "Central America Standard Time", "MonFri", 900, 1400));
            Add("Panama", new Market("BVP", "SA Pacific Standard Time", "MonFri", 1000, 1500));
            Add("Uruguay", new Market("BVM", "Montevideo Standard Time", "MonFri", 1200, 1700));
            Add("Venezuela", new Market("BVC", "Venezuela Standard Time", "MonFri", 930, 1300));
            // ---- Europe ----
            Add("United Kingdom", new Market("LSE", "GMT Standard Time", "MonFri", 800, 1630));
            Add("Ireland", new Market("ISE", "GMT Standard Time", "MonFri", 800, 1630));
            Add("Portugal", new Market("ELI", "GMT Standard Time", "MonFri", 800, 1630));
            Add("Iceland", new Market("ICEX", "Greenwich Standard Time", "MonFri", 930, 1530));
            var euronext = Add("France", new Market("ENX", "Romance Standard Time", "MonFri", 900, 1730));
            ByCountry["Netherlands"] = euronext;
            ByCountry["Belgium"] = euronext;
            Add("Spain", new Market("BME", "Romance Standard Time", "MonFri", 900, 1730));
            Add("Germany", new Market("Xetra", "W. Europe Standard Time", "MonFri", 900, 1730));
            Add("Italy", new Market("BIT", "W. Europe Standard Time", "MonFri", 900, 1730));
            Add("Switzerland", new Market("SIX", "W. Europe Standard Time", "MonFri", 900, 1730));
            Add("Austria", new Market("VSE", "W. Europe Standard Time", "MonFri", 900, 1730));
            Add("Luxembourg", new Market("LuxSE", "W. Europe Standard Time", "MonFri", 900, 1730));
            Add("Sweden", new Market("OMXS", "W. Europe Standard Time", "MonFri", 900, 1730));
            Add("Norway", new Market("OSE", "W. Europe Standard Time", "MonFri", 900, 1620));
            Add("Denmark", new Market("OMXC", "Romance Standard Time", "MonFri", 900, 1700));
            Add("Finland", new Market("OMXH", "FLE Standard Time", "MonFri", 1000, 1830));
            Add("Poland", new Market("GPW", "Central European Standard Time", "MonFri", 900, 1700));
            Add("Czechia", new Market("PSE", "Central Europe Standard Time", "MonFri", 900, 1620));
            Add("Hungary", new Market("BSE", "Central Europe Standard Time", "MonFri", 900, 1700));
            Add("Greece", new Market("ATHEX", "GTB Standard Time", "MonFri", 1000, 1720));
            Add("Romania", new Market("BVB", "GTB Standard Time", "MonFri", 1000, 1800));
            Add("Türkiye", new Market("BIST", "Turkey Standard Time", "MonFri", 1000, 1800));
            Add("Russia", new Market("MOEX", "Russian Standard Time", "MonFri", 1000, 1840));
            var baltic = Add("Latvia", new Market("OMXB", "FLE Standard Time", "MonFri", 1000, 1600));
            ByCountry["Lithuania"] = baltic;
            ByCountry["Estonia"] = baltic;
            Add("Croatia", new Market("ZSE", "Central European Standard Time", "MonFri", 900, 1600));
            Add("Serbia", new Market("BELEX", "Central Europe Standard Time", "MonFri", 930, 1400));
            Add("Bulgaria", new Market("BSE", "FLE Standard Time", "MonFri", 1000, 1700));
            Add("Cyprus", new Market("CSE", "GTB Standard Time", "MonFri", 1000, 1700));
            Add("Malta", new Market("MSE", "W. Europe Standard Time", "MonFri", 1000, 1230));
            // ---- Africa ----
            Add("South Africa", new Market("JSE", "South Africa Standard Time", "MonFri", 900, 1700));
            Add("Nigeria", new Market("NGX", "W. Central Africa Standard Time", "MonFri", 1000, 1420));
            Add("Kenya", new Market("NSE", "E. Africa Standard Time", "MonFri", 930, 1500));
            Add("Egypt", new Market("EGX", "Egypt Standard Time", "SunThu", 1000, 1430));
            Add("Morocco", new Market("CSE", "Morocco Standard Time", "MonFri", 930, 1530));
            Add("Tunisia", new Market("BVMT", "W. Central Africa Standard Time", "MonFri", 900, 1400));
            Add("Ghana", new Market("GSE", "Greenwich Standard Time", "MonFri", 930, 1500));
            Add("Mauritius", new Market("SEM", "Mauritius Standard Time", "MonFri", 900, 1330));
            Add("Zimbabwe", new Market("ZSE", "South Africa Standard Time", "MonFri", 1000, 1230));
            Add("Botswana", new Market("BSE", "South Africa Standard Time", "MonFri", 1030, 1330));
            Add("Uganda", new Market("USE", "E. Africa Standard Time", "MonFri", 1000, 1400));
            Add("Tanzania", new Market("DSE", "E. Africa Standard Time", "MonFri", 1000, 1400));
            Add("Rwanda", new Market("RSE", "South Africa Standard Time", "MonFri", 900, 1200));
            // ---- Middle East & Caucasus ----
            Add("Israel", new Market("TASE", "Israel Standard Time", "MonFri", 1000, 1715));
            Add("Saudi Arabia", new Market("TDWL", "Arab Standard Time", "SunThu", 1000, 1500));
            Add("United Arab Emirates", new Market("DFM", "Arabian Standard Time", "MonFri", 1000, 1500));
            Add("Qatar", new Market("QSE", "Arab Standard Time", "SunThu", 930, 1300));
            Add("Kuwait", new Market("KSE", "Arab Standard Time", "SunThu", 900, 1240));
            Add("Bahrain", new Market("BHB", "Arab Standard Time", "SunThu", 930, 1300));
            Add("Oman", new Market("MSX", "Arabian Standard Time", "SunThu", 1000, 1300));
            Add("Jordan", new Market("ASE", "Jordan Standard Time", "SunThu", 1000, 1230));
            Add("Lebanon", new Market("BSE", "Middle East Standard Time", "MonFri", 930, 1230));
            Add("Iraq", new Market("ISX", "Arabic Standard Time", "SunThu", 1000, 1300));
            Add("Iran", new Market("TSE", "Iran Standard Time", "SatWed", 900, 1230));
            Add("Azerbaijan", new Market("BSE", "Azerbaijan Standard Time", "MonFri", 1000, 1700));
            Add("Georgia", new Market("GSE", "Georgian Standard Time", "MonFri", 1000, 1700));
            Add("Armenia", new Market("AMX", "Caucasus Standard Time", "MonFri", 1000, 1600));
            // ---- South & Central Asia ----
            Add("India", new Market("NSE", "India Standard Time", "MonFri", 915, 1530));
            Add("Pakistan", new Market("PSX", "Pakistan Standard Time", "MonFri", 930, 1530));
            Add("Bangladesh", new Market("DSE", "Bangladesh Standard Time", "SunThu", 1000, 1430));
            Add("Sri Lanka", new Market("CSE", "Sri Lanka Standard Time", "MonFri", 930, 1430));
            Add("Nepal", new Market("NEPSE", "Nepal Standard Time", "SunThu", 1100, 1500));
            Add("Kazakhstan", new Market("KASE", "Central Asia Standard Time", "MonFri", 1100, 1700));
            Add("Uzbekistan", new Market("UZSE", "West Asia Standard Time", "MonFri", 1000, 1500));
            Add("Kyrgyzstan", new Market("KSE", "Central Asia Standard Time", "MonFri", 1000, 1500));
            Add("Maldives", new Market("MSE", "West Asia Standard Time", "SunThu", 1000, 1500));
            Add("Bhutan", new Market("RSEB", "Bangladesh Standard Time", "MonFri", 1000, 1300));
            // ---- East & Southeast Asia ----
            Add("Japan", new Market("TSE", "Tokyo Standard Time", "MonFri", 900, 1130, 1230, 1530));
            Add("South Korea", new Market("KRX", "Korea Standard Time", "MonFri", 900, 1530));
            Add("China", new Market("SSE", "China Standard Time", "MonFri", 930, 1130, 1300, 1500));
            Add("Hong Kong", new Market("HKEX", "China Standard Time", "MonFri", 930, 1200, 1300, 1600));
            Add("Taiwan", new Market("TWSE", "Taipei Standard Time", "MonFri", 900, 1330));
            Add("Singapore", new Market("SGX", "Singapore Standard Time", "MonFri", 900, 1200, 1300, 1700));
            Add("Malaysia", new Market("KLSE", "Singapore Standard Time", "MonFri", 900, 1230, 1430, 1700));
            Add("Thailand", new Market("SET", "SE Asia Standard Time", "MonFri", 1000, 1230, 1430, 1630));
            Add("Indonesia", new Market("IDX", "SE Asia Standard Time", "MonFri", 900, 1130, 1330, 1550));
            Add("Philippines", new Market("PSE", "Singapore Standard Time", "MonFri", 930, 1200, 1300, 1500));
            Add("Vietnam", new Market("HOSE", "SE Asia Standard Time", "MonFri", 900, 1130, 1300, 1500));
            Add("Mongolia", new Market("MSE", "Ulaanbaatar Standard Time", "MonFri", 1000, 1300));
            Add("Cambodia", new Market("CSX", "SE Asia Standard Time", "MonFri", 900, 1500));
            Add("Laos", new Market("LSX", "SE Asia Standard Time", "MonFri", 900, 1500));
            Add("Myanmar", new Market("YSX", "Myanmar Standard Time", "MonFri", 930, 1300));
            // ---- Australia & Pacific ----
            Add("Australia", new Market("ASX", "AUS Eastern Standard Time", "MonFri", 1000, 1600));
            Add("New Zealand", new Market("NZX", "New Zealand Standard Time", "MonFri", 1000, 1645));
            Add("Papua New Guinea", new Market("PNGX", "West Pacific Standard Time", "MonFri", 1000, 1600));
            Add("Fiji", new Market("SPX", "Fiji Standard Time", "MonFri", 1030, 1430));

            // City-level exceptions (where a city's exchange differs from its country's main one).
            ByCity["Shenzhen"] = new Market("SZSE", "China Standard Time", "MonFri", 930, 1130, 1300, 1500);
            ByCity["Chicago"] = new Market("NYSE", "Eastern Standard Time", "MonFri", 930, 1600);
            ByCity["Abu Dhabi"] = new Market("ADX", "Arabian Standard Time", "MonFri", 1000, 1500);
        }

        /// <summary>The market for a catalog city, or null if none is listed.</summary>
        public static Market For(ZoneEntry entry)
        {
            if (entry == null) return null;
            Market m;
            if (ByCity.TryGetValue(entry.City, out m)) return m;
            if (!string.IsNullOrEmpty(entry.Country) && ByCountry.TryGetValue(entry.Country, out m)) return m;
            return null;
        }

        /// <summary>The market for a widget: by its city label when it is a catalog city, else by zone.</summary>
        public static Market ForWidget(string windowsId, string label)
        {
            var entry = ZoneCatalog.EntryFor(windowsId, label);
            return For(entry);
        }

        /// <summary>Open/closed and the countdown for a market at a given UTC instant.</summary>
        public static MarketStatus GetStatus(Market m, DateTime utc)
        {
            var s = new MarketStatus();
            s.Market = m;
            if (m == null) return s;
            TimeZoneInfo tz;
            try { tz = TimeZoneInfo.FindSystemTimeZoneById(m.WindowsId); }
            catch { s.Market = null; return s; }
            var local = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
            double nowMin = (local - local.Date).TotalMinutes;
            if (m.Days[(int)local.DayOfWeek])
            {
                for (int i = 0; i < m.Sessions.Length; i++)
                {
                    int open = m.Sessions[i][0], close = m.Sessions[i][1];
                    if (nowMin >= open && nowMin < close)
                    {
                        s.Open = true;
                        s.Until = TimeSpan.FromMinutes(close - nowMin);
                        return s;
                    }
                    if (nowMin < open)
                    {
                        s.Break = i > 0;
                        s.Until = TimeSpan.FromMinutes(open - nowMin);
                        return s;
                    }
                }
            }
            // Closed for the day: the first session of the next trading day.
            for (int d = 1; d <= 8; d++)
            {
                var day = local.Date.AddDays(d);
                if (!m.Days[(int)day.DayOfWeek]) continue;
                var openAt = day.AddMinutes(m.Sessions[0][0]);
                s.Until = openAt - local;
                return s;
            }
            s.Market = null;
            return s;
        }
    }
}
