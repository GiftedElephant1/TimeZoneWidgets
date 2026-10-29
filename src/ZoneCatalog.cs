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
        public readonly string Country;
        public readonly string WindowsId;
        /// <summary>Approximate city coordinates, used to mark the city on the globe.</summary>
        public readonly double Latitude;
        public readonly double Longitude;

        public ZoneEntry(string city, string code, string country, string windowsId, double latitude, double longitude)
        {
            City = city;
            Code = code;
            Country = country ?? "";
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
            new ZoneRegion("North America",
                new ZoneEntry("Honolulu", "HNL", "United States", "Hawaiian Standard Time", 21.31, -157.86),
                new ZoneEntry("Anchorage", "ANC", "United States", "Alaskan Standard Time", 61.22, -149.90),
                new ZoneEntry("Los Angeles", "LAX", "United States", "Pacific Standard Time", 34.05, -118.24),
                new ZoneEntry("San Francisco", "SFO", "United States", "Pacific Standard Time", 37.77, -122.42),
                new ZoneEntry("San Jose", "SJC", "United States", "Pacific Standard Time", 37.34, -121.89),
                new ZoneEntry("San Diego", "SAN", "United States", "Pacific Standard Time", 32.72, -117.16),
                new ZoneEntry("Seattle", "SEA", "United States", "Pacific Standard Time", 47.61, -122.33),
                new ZoneEntry("Portland", "PDX", "United States", "Pacific Standard Time", 45.52, -122.68),
                new ZoneEntry("Las Vegas", "LAS", "United States", "Pacific Standard Time", 36.17, -115.14),
                new ZoneEntry("Vancouver", "YVR", "Canada", "Pacific Standard Time", 49.28, -123.12),
                new ZoneEntry("Tijuana", "TIJ", "Mexico", "Pacific Standard Time (Mexico)", 32.51, -117.04),
                new ZoneEntry("Denver", "DEN", "United States", "Mountain Standard Time", 39.74, -104.99),
                new ZoneEntry("Salt Lake City", "SLC", "United States", "Mountain Standard Time", 40.76, -111.89),
                new ZoneEntry("Calgary", "YYC", "Canada", "Mountain Standard Time", 51.05, -114.07),
                new ZoneEntry("Edmonton", "YEG", "Canada", "Mountain Standard Time", 53.55, -113.49),
                new ZoneEntry("Phoenix", "PHX", "United States", "US Mountain Standard Time", 33.45, -112.07),
                new ZoneEntry("Chicago", "CHI", "United States", "Central Standard Time", 41.88, -87.63),
                new ZoneEntry("Houston", "HOU", "United States", "Central Standard Time", 29.76, -95.37),
                new ZoneEntry("Dallas", "DFW", "United States", "Central Standard Time", 32.78, -96.80),
                new ZoneEntry("Austin", "AUS", "United States", "Central Standard Time", 30.27, -97.74),
                new ZoneEntry("Minneapolis", "MSP", "United States", "Central Standard Time", 44.98, -93.27),
                new ZoneEntry("New Orleans", "MSY", "United States", "Central Standard Time", 29.95, -90.07),
                new ZoneEntry("Kansas City", "MKC", "United States", "Central Standard Time", 39.10, -94.58),
                new ZoneEntry("Winnipeg", "YWG", "Canada", "Central Standard Time", 49.90, -97.14),
                new ZoneEntry("Mexico City", "MEX", "Mexico", "Central Standard Time (Mexico)", 19.43, -99.13),
                new ZoneEntry("Guadalajara", "GDL", "Mexico", "Central Standard Time (Mexico)", 20.66, -103.35),
                new ZoneEntry("Monterrey", "MTY", "Mexico", "Central Standard Time (Mexico)", 25.69, -100.32),
                new ZoneEntry("Cancún", "CUN", "Mexico", "Eastern Standard Time (Mexico)", 21.16, -86.85),
                new ZoneEntry("New York", "NYC", "United States", "Eastern Standard Time", 40.71, -74.01),
                new ZoneEntry("Washington", "WAS", "United States", "Eastern Standard Time", 38.91, -77.04),
                new ZoneEntry("Boston", "BOS", "United States", "Eastern Standard Time", 42.36, -71.06),
                new ZoneEntry("Philadelphia", "PHL", "United States", "Eastern Standard Time", 39.95, -75.17),
                new ZoneEntry("Miami", "MIA", "United States", "Eastern Standard Time", 25.76, -80.19),
                new ZoneEntry("Atlanta", "ATL", "United States", "Eastern Standard Time", 33.75, -84.39),
                new ZoneEntry("Detroit", "DTT", "United States", "Eastern Standard Time", 42.33, -83.05),
                new ZoneEntry("Toronto", "YTO", "Canada", "Eastern Standard Time", 43.65, -79.38),
                new ZoneEntry("Montréal", "YMQ", "Canada", "Eastern Standard Time", 45.50, -73.57),
                new ZoneEntry("Ottawa", "YOW", "Canada", "Eastern Standard Time", 45.42, -75.70),
                new ZoneEntry("Havana", "HAV", "Cuba", "Cuba Standard Time", 23.11, -82.37),
                new ZoneEntry("Nassau", "NAS", "Bahamas", "Eastern Standard Time", 25.05, -77.34),
                new ZoneEntry("Kingston", "KIN", "Jamaica", "SA Pacific Standard Time", 18.00, -76.80),
                new ZoneEntry("Panama City", "PTY", "Panama", "SA Pacific Standard Time", 8.98, -79.52),
                new ZoneEntry("Guatemala City", "GUA", "Guatemala", "Central America Standard Time", 14.63, -90.51),
                new ZoneEntry("San Salvador", "SAL", "El Salvador", "Central America Standard Time", 13.69, -89.19),
                new ZoneEntry("Tegucigalpa", "TGU", "Honduras", "Central America Standard Time", 14.07, -87.19),
                new ZoneEntry("Managua", "MGA", "Nicaragua", "Central America Standard Time", 12.11, -86.24),
                new ZoneEntry("San José", "SJO", "Costa Rica", "Central America Standard Time", 9.93, -84.08),
                new ZoneEntry("Halifax", "YHZ", "Canada", "Atlantic Standard Time", 44.65, -63.57),
                new ZoneEntry("San Juan", "SJU", "Puerto Rico", "SA Western Standard Time", 18.47, -66.11),
                new ZoneEntry("Santo Domingo", "SDQ", "Dominican Republic", "SA Western Standard Time", 18.49, -69.93),
                new ZoneEntry("Port-au-Prince", "PAP", "Haiti", "Haiti Standard Time", 18.54, -72.34),
                new ZoneEntry("St. John's", "YYT", "Canada", "Newfoundland Standard Time", 47.56, -52.71),
                new ZoneEntry("Nuuk", "GOH", "Greenland", "Greenland Standard Time", 64.18, -51.72)),
            new ZoneRegion("South America",
                new ZoneEntry("Bogotá", "BOG", "Colombia", "SA Pacific Standard Time", 4.71, -74.07),
                new ZoneEntry("Medellín", "MDE", "Colombia", "SA Pacific Standard Time", 6.25, -75.56),
                new ZoneEntry("Lima", "LIM", "Peru", "SA Pacific Standard Time", -12.05, -77.04),
                new ZoneEntry("Quito", "UIO", "Ecuador", "SA Pacific Standard Time", -0.18, -78.47),
                new ZoneEntry("Guayaquil", "GYE", "Ecuador", "SA Pacific Standard Time", -2.19, -79.89),
                new ZoneEntry("Caracas", "CCS", "Venezuela", "Venezuela Standard Time", 10.48, -66.90),
                new ZoneEntry("La Paz", "LPB", "Bolivia", "SA Western Standard Time", -16.50, -68.15),
                new ZoneEntry("Manaus", "MAO", "Brazil", "SA Western Standard Time", -3.12, -60.02),
                new ZoneEntry("Santiago", "SCL", "Chile", "Pacific SA Standard Time", -33.45, -70.67),
                new ZoneEntry("Asunción", "ASU", "Paraguay", "Paraguay Standard Time", -25.26, -57.58),
                new ZoneEntry("São Paulo", "SAO", "Brazil", "E. South America Standard Time", -23.55, -46.63),
                new ZoneEntry("Rio de Janeiro", "RIO", "Brazil", "E. South America Standard Time", -22.91, -43.17),
                new ZoneEntry("Brasília", "BSB", "Brazil", "E. South America Standard Time", -15.79, -47.88),
                new ZoneEntry("Belo Horizonte", "BHZ", "Brazil", "E. South America Standard Time", -19.92, -43.94),
                new ZoneEntry("Salvador", "SSA", "Brazil", "Bahia Standard Time", -12.97, -38.51),
                new ZoneEntry("Recife", "REC", "Brazil", "SA Eastern Standard Time", -8.05, -34.90),
                new ZoneEntry("Buenos Aires", "BUE", "Argentina", "Argentina Standard Time", -34.60, -58.38),
                new ZoneEntry("Córdoba", "COR", "Argentina", "Argentina Standard Time", -31.42, -64.18),
                new ZoneEntry("Montevideo", "MVD", "Uruguay", "Montevideo Standard Time", -34.90, -56.16)),
            new ZoneRegion("Western Europe",
                new ZoneEntry("UTC", "UTC", "Coordinated Universal Time", "UTC", 0, 0),
                new ZoneEntry("Reykjavík", "REK", "Iceland", "Greenwich Standard Time", 64.15, -21.94),
                new ZoneEntry("London", "LON", "United Kingdom", "GMT Standard Time", 51.51, -0.13),
                new ZoneEntry("Manchester", "MAN", "United Kingdom", "GMT Standard Time", 53.48, -2.24),
                new ZoneEntry("Edinburgh", "EDI", "United Kingdom", "GMT Standard Time", 55.95, -3.19),
                new ZoneEntry("Dublin", "DUB", "Ireland", "GMT Standard Time", 53.35, -6.26),
                new ZoneEntry("Lisbon", "LIS", "Portugal", "GMT Standard Time", 38.72, -9.14),
                new ZoneEntry("Porto", "OPO", "Portugal", "GMT Standard Time", 41.15, -8.61),
                new ZoneEntry("Paris", "PAR", "France", "Romance Standard Time", 48.86, 2.35),
                new ZoneEntry("Lyon", "LYS", "France", "Romance Standard Time", 45.76, 4.84),
                new ZoneEntry("Marseille", "MRS", "France", "Romance Standard Time", 43.30, 5.37),
                new ZoneEntry("Madrid", "MAD", "Spain", "Romance Standard Time", 40.42, -3.70),
                new ZoneEntry("Barcelona", "BCN", "Spain", "Romance Standard Time", 41.39, 2.17),
                new ZoneEntry("Brussels", "BRU", "Belgium", "Romance Standard Time", 50.85, 4.35),
                new ZoneEntry("Copenhagen", "CPH", "Denmark", "Romance Standard Time", 55.68, 12.57),
                new ZoneEntry("Amsterdam", "AMS", "Netherlands", "W. Europe Standard Time", 52.37, 4.90),
                new ZoneEntry("Rotterdam", "RTM", "Netherlands", "W. Europe Standard Time", 51.92, 4.48),
                new ZoneEntry("Luxembourg", "LUX", "Luxembourg", "W. Europe Standard Time", 49.61, 6.13),
                new ZoneEntry("Berlin", "BER", "Germany", "W. Europe Standard Time", 52.52, 13.41),
                new ZoneEntry("Munich", "MUC", "Germany", "W. Europe Standard Time", 48.14, 11.58),
                new ZoneEntry("Frankfurt", "FRA", "Germany", "W. Europe Standard Time", 50.11, 8.68),
                new ZoneEntry("Hamburg", "HAM", "Germany", "W. Europe Standard Time", 53.55, 9.99),
                new ZoneEntry("Cologne", "CGN", "Germany", "W. Europe Standard Time", 50.94, 6.96),
                new ZoneEntry("Düsseldorf", "DUS", "Germany", "W. Europe Standard Time", 51.23, 6.78),
                new ZoneEntry("Stuttgart", "STR", "Germany", "W. Europe Standard Time", 48.78, 9.18),
                new ZoneEntry("Vienna", "VIE", "Austria", "W. Europe Standard Time", 48.21, 16.37),
                new ZoneEntry("Zürich", "ZRH", "Switzerland", "W. Europe Standard Time", 47.38, 8.54),
                new ZoneEntry("Geneva", "GVA", "Switzerland", "W. Europe Standard Time", 46.20, 6.14),
                new ZoneEntry("Rome", "ROM", "Italy", "W. Europe Standard Time", 41.90, 12.50),
                new ZoneEntry("Milan", "MIL", "Italy", "W. Europe Standard Time", 45.46, 9.19),
                new ZoneEntry("Naples", "NAP", "Italy", "W. Europe Standard Time", 40.85, 14.27),
                new ZoneEntry("Turin", "TRN", "Italy", "W. Europe Standard Time", 45.07, 7.69),
                new ZoneEntry("Stockholm", "STO", "Sweden", "W. Europe Standard Time", 59.33, 18.07),
                new ZoneEntry("Gothenburg", "GOT", "Sweden", "W. Europe Standard Time", 57.71, 11.97),
                new ZoneEntry("Oslo", "OSL", "Norway", "W. Europe Standard Time", 59.91, 10.75),
                new ZoneEntry("Malta", "MLA", "Malta", "W. Europe Standard Time", 35.90, 14.51)),
            new ZoneRegion("Eastern Europe & Russia",
                new ZoneEntry("Prague", "PRG", "Czechia", "Central Europe Standard Time", 50.08, 14.44),
                new ZoneEntry("Budapest", "BUD", "Hungary", "Central Europe Standard Time", 47.50, 19.04),
                new ZoneEntry("Belgrade", "BEG", "Serbia", "Central Europe Standard Time", 44.79, 20.47),
                new ZoneEntry("Bratislava", "BTS", "Slovakia", "Central Europe Standard Time", 48.15, 17.11),
                new ZoneEntry("Ljubljana", "LJU", "Slovenia", "Central Europe Standard Time", 46.06, 14.51),
                new ZoneEntry("Tirana", "TIA", "Albania", "Central Europe Standard Time", 41.33, 19.82),
                new ZoneEntry("Warsaw", "WAW", "Poland", "Central European Standard Time", 52.23, 21.01),
                new ZoneEntry("Kraków", "KRK", "Poland", "Central European Standard Time", 50.06, 19.94),
                new ZoneEntry("Zagreb", "ZAG", "Croatia", "Central European Standard Time", 45.81, 15.98),
                new ZoneEntry("Sarajevo", "SJJ", "Bosnia and Herzegovina", "Central European Standard Time", 43.86, 18.41),
                new ZoneEntry("Skopje", "SKP", "North Macedonia", "Central European Standard Time", 41.99, 21.43),
                new ZoneEntry("Helsinki", "HEL", "Finland", "FLE Standard Time", 60.17, 24.94),
                new ZoneEntry("Kyiv", "IEV", "Ukraine", "FLE Standard Time", 50.45, 30.52),
                new ZoneEntry("Riga", "RIX", "Latvia", "FLE Standard Time", 56.95, 24.11),
                new ZoneEntry("Vilnius", "VNO", "Lithuania", "FLE Standard Time", 54.69, 25.28),
                new ZoneEntry("Tallinn", "TLL", "Estonia", "FLE Standard Time", 59.44, 24.75),
                new ZoneEntry("Sofia", "SOF", "Bulgaria", "FLE Standard Time", 42.70, 23.32),
                new ZoneEntry("Bucharest", "BUH", "Romania", "GTB Standard Time", 44.43, 26.10),
                new ZoneEntry("Athens", "ATH", "Greece", "GTB Standard Time", 37.98, 23.73),
                new ZoneEntry("Thessaloniki", "SKG", "Greece", "GTB Standard Time", 40.64, 22.94),
                new ZoneEntry("Nicosia", "NIC", "Cyprus", "GTB Standard Time", 35.17, 33.37),
                new ZoneEntry("Chișinău", "KIV", "Moldova", "E. Europe Standard Time", 47.01, 28.86),
                new ZoneEntry("Istanbul", "IST", "Türkiye", "Turkey Standard Time", 41.01, 28.98),
                new ZoneEntry("Ankara", "ESB", "Türkiye", "Turkey Standard Time", 39.93, 32.86),
                new ZoneEntry("Minsk", "MSQ", "Belarus", "Belarus Standard Time", 53.90, 27.57),
                new ZoneEntry("Moscow", "MOW", "Russia", "Russian Standard Time", 55.76, 37.62),
                new ZoneEntry("St. Petersburg", "LED", "Russia", "Russian Standard Time", 59.93, 30.32),
                new ZoneEntry("Kaliningrad", "KGD", "Russia", "Kaliningrad Standard Time", 54.71, 20.51),
                new ZoneEntry("Samara", "KUF", "Russia", "Russia Time Zone 3", 53.20, 50.15),
                new ZoneEntry("Yekaterinburg", "SVX", "Russia", "Ekaterinburg Standard Time", 56.84, 60.60),
                new ZoneEntry("Omsk", "OMS", "Russia", "Omsk Standard Time", 54.99, 73.37),
                new ZoneEntry("Novosibirsk", "OVB", "Russia", "N. Central Asia Standard Time", 55.03, 82.92),
                new ZoneEntry("Krasnoyarsk", "KJA", "Russia", "North Asia Standard Time", 56.01, 92.87),
                new ZoneEntry("Irkutsk", "IKT", "Russia", "North Asia East Standard Time", 52.29, 104.28),
                new ZoneEntry("Yakutsk", "YKS", "Russia", "Yakutsk Standard Time", 62.04, 129.73),
                new ZoneEntry("Vladivostok", "VVO", "Russia", "Vladivostok Standard Time", 43.12, 131.89),
                new ZoneEntry("Magadan", "GDX", "Russia", "Magadan Standard Time", 59.56, 150.80),
                new ZoneEntry("Kamchatka", "PKC", "Russia", "Russia Time Zone 11", 53.02, 158.65)),
            new ZoneRegion("Africa",
                new ZoneEntry("Casablanca", "CAS", "Morocco", "Morocco Standard Time", 33.57, -7.59),
                new ZoneEntry("Dakar", "DKR", "Senegal", "Greenwich Standard Time", 14.69, -17.44),
                new ZoneEntry("Accra", "ACC", "Ghana", "Greenwich Standard Time", 5.60, -0.19),
                new ZoneEntry("Abidjan", "ABJ", "Côte d'Ivoire", "Greenwich Standard Time", 5.36, -4.01),
                new ZoneEntry("Lagos", "LOS", "Nigeria", "W. Central Africa Standard Time", 6.52, 3.38),
                new ZoneEntry("Abuja", "ABV", "Nigeria", "W. Central Africa Standard Time", 9.06, 7.49),
                new ZoneEntry("Algiers", "ALG", "Algeria", "W. Central Africa Standard Time", 36.75, 3.06),
                new ZoneEntry("Tunis", "TUN", "Tunisia", "W. Central Africa Standard Time", 36.80, 10.18),
                new ZoneEntry("Kinshasa", "FIH", "DR Congo", "W. Central Africa Standard Time", -4.44, 15.27),
                new ZoneEntry("Luanda", "LAD", "Angola", "W. Central Africa Standard Time", -8.84, 13.23),
                new ZoneEntry("Douala", "DLA", "Cameroon", "W. Central Africa Standard Time", 4.05, 9.70),
                new ZoneEntry("Cairo", "CAI", "Egypt", "Egypt Standard Time", 30.04, 31.24),
                new ZoneEntry("Tripoli", "TIP", "Libya", "Libya Standard Time", 32.89, 13.19),
                new ZoneEntry("Khartoum", "KRT", "Sudan", "Sudan Standard Time", 15.50, 32.56),
                new ZoneEntry("Johannesburg", "JNB", "South Africa", "South Africa Standard Time", -26.20, 28.05),
                new ZoneEntry("Cape Town", "CPT", "South Africa", "South Africa Standard Time", -33.93, 18.42),
                new ZoneEntry("Durban", "DUR", "South Africa", "South Africa Standard Time", -29.86, 31.02),
                new ZoneEntry("Harare", "HRE", "Zimbabwe", "South Africa Standard Time", -17.83, 31.05),
                new ZoneEntry("Lusaka", "LUN", "Zambia", "South Africa Standard Time", -15.39, 28.32),
                new ZoneEntry("Maputo", "MPM", "Mozambique", "South Africa Standard Time", -25.97, 32.57),
                new ZoneEntry("Gaborone", "GBE", "Botswana", "South Africa Standard Time", -24.65, 25.91),
                new ZoneEntry("Kigali", "KGL", "Rwanda", "South Africa Standard Time", -1.94, 30.06),
                new ZoneEntry("Windhoek", "WDH", "Namibia", "Namibia Standard Time", -22.56, 17.08),
                new ZoneEntry("Nairobi", "NBO", "Kenya", "E. Africa Standard Time", -1.29, 36.82),
                new ZoneEntry("Addis Ababa", "ADD", "Ethiopia", "E. Africa Standard Time", 9.03, 38.74),
                new ZoneEntry("Dar es Salaam", "DAR", "Tanzania", "E. Africa Standard Time", -6.79, 39.28),
                new ZoneEntry("Kampala", "KLA", "Uganda", "E. Africa Standard Time", 0.35, 32.58),
                new ZoneEntry("Mogadishu", "MGQ", "Somalia", "E. Africa Standard Time", 2.05, 45.32),
                new ZoneEntry("Antananarivo", "TNR", "Madagascar", "E. Africa Standard Time", -18.88, 47.51),
                new ZoneEntry("Port Louis", "MRU", "Mauritius", "Mauritius Standard Time", -20.16, 57.50)),
            new ZoneRegion("Middle East & Caucasus",
                new ZoneEntry("Jerusalem", "JRS", "Israel", "Israel Standard Time", 31.77, 35.21),
                new ZoneEntry("Tel Aviv", "TLV", "Israel", "Israel Standard Time", 32.07, 34.78),
                new ZoneEntry("Amman", "AMM", "Jordan", "Jordan Standard Time", 31.95, 35.93),
                new ZoneEntry("Beirut", "BEY", "Lebanon", "Middle East Standard Time", 33.89, 35.50),
                new ZoneEntry("Damascus", "DAM", "Syria", "Syria Standard Time", 33.51, 36.29),
                new ZoneEntry("Baghdad", "BGW", "Iraq", "Arabic Standard Time", 33.34, 44.40),
                new ZoneEntry("Riyadh", "RUH", "Saudi Arabia", "Arab Standard Time", 24.71, 46.68),
                new ZoneEntry("Jeddah", "JED", "Saudi Arabia", "Arab Standard Time", 21.49, 39.19),
                new ZoneEntry("Kuwait City", "KWI", "Kuwait", "Arab Standard Time", 29.38, 47.99),
                new ZoneEntry("Doha", "DOH", "Qatar", "Arab Standard Time", 25.29, 51.53),
                new ZoneEntry("Manama", "BAH", "Bahrain", "Arab Standard Time", 26.23, 50.59),
                new ZoneEntry("Sana'a", "SAH", "Yemen", "Arab Standard Time", 15.37, 44.19),
                new ZoneEntry("Dubai", "DXB", "United Arab Emirates", "Arabian Standard Time", 25.20, 55.27),
                new ZoneEntry("Abu Dhabi", "AUH", "United Arab Emirates", "Arabian Standard Time", 24.45, 54.65),
                new ZoneEntry("Muscat", "MCT", "Oman", "Arabian Standard Time", 23.59, 58.41),
                new ZoneEntry("Tehran", "THR", "Iran", "Iran Standard Time", 35.69, 51.39),
                new ZoneEntry("Baku", "BAK", "Azerbaijan", "Azerbaijan Standard Time", 40.41, 49.87),
                new ZoneEntry("Tbilisi", "TBS", "Georgia", "Georgian Standard Time", 41.72, 44.79),
                new ZoneEntry("Yerevan", "EVN", "Armenia", "Caucasus Standard Time", 40.18, 44.51)),
            new ZoneRegion("South & Central Asia",
                new ZoneEntry("Kabul", "KBL", "Afghanistan", "Afghanistan Standard Time", 34.53, 69.17),
                new ZoneEntry("Karachi", "KHI", "Pakistan", "Pakistan Standard Time", 24.86, 67.00),
                new ZoneEntry("Lahore", "LHE", "Pakistan", "Pakistan Standard Time", 31.55, 74.34),
                new ZoneEntry("Islamabad", "ISB", "Pakistan", "Pakistan Standard Time", 33.69, 73.04),
                new ZoneEntry("Tashkent", "TAS", "Uzbekistan", "West Asia Standard Time", 41.30, 69.24),
                new ZoneEntry("Ashgabat", "ASB", "Turkmenistan", "West Asia Standard Time", 37.96, 58.33),
                new ZoneEntry("Dushanbe", "DYU", "Tajikistan", "West Asia Standard Time", 38.56, 68.79),
                new ZoneEntry("Malé", "MLE", "Maldives", "West Asia Standard Time", 4.18, 73.51),
                new ZoneEntry("Astana", "NQZ", "Kazakhstan", "Central Asia Standard Time", 51.17, 71.43),
                new ZoneEntry("Almaty", "ALA", "Kazakhstan", "Central Asia Standard Time", 43.24, 76.95),
                new ZoneEntry("Bishkek", "FRU", "Kyrgyzstan", "Central Asia Standard Time", 42.87, 74.59),
                new ZoneEntry("Mumbai", "BOM", "India", "India Standard Time", 19.08, 72.88),
                new ZoneEntry("New Delhi", "DEL", "India", "India Standard Time", 28.61, 77.21),
                new ZoneEntry("Bengaluru", "BLR", "India", "India Standard Time", 12.97, 77.59),
                new ZoneEntry("Chennai", "MAA", "India", "India Standard Time", 13.08, 80.27),
                new ZoneEntry("Kolkata", "CCU", "India", "India Standard Time", 22.57, 88.36),
                new ZoneEntry("Hyderabad", "HYD", "India", "India Standard Time", 17.39, 78.49),
                new ZoneEntry("Pune", "PNQ", "India", "India Standard Time", 18.52, 73.86),
                new ZoneEntry("Ahmedabad", "AMD", "India", "India Standard Time", 23.02, 72.57),
                new ZoneEntry("Colombo", "CMB", "Sri Lanka", "Sri Lanka Standard Time", 6.93, 79.86),
                new ZoneEntry("Kathmandu", "KTM", "Nepal", "Nepal Standard Time", 27.72, 85.32),
                new ZoneEntry("Dhaka", "DAC", "Bangladesh", "Bangladesh Standard Time", 23.81, 90.41),
                new ZoneEntry("Thimphu", "PBH", "Bhutan", "Bangladesh Standard Time", 27.47, 89.64)),
            new ZoneRegion("East & Southeast Asia",
                new ZoneEntry("Yangon", "RGN", "Myanmar", "Myanmar Standard Time", 16.87, 96.20),
                new ZoneEntry("Bangkok", "BKK", "Thailand", "SE Asia Standard Time", 13.76, 100.50),
                new ZoneEntry("Hanoi", "HAN", "Vietnam", "SE Asia Standard Time", 21.03, 105.85),
                new ZoneEntry("Ho Chi Minh City", "SGN", "Vietnam", "SE Asia Standard Time", 10.82, 106.63),
                new ZoneEntry("Phnom Penh", "PNH", "Cambodia", "SE Asia Standard Time", 11.56, 104.92),
                new ZoneEntry("Vientiane", "VTE", "Laos", "SE Asia Standard Time", 17.97, 102.63),
                new ZoneEntry("Jakarta", "JKT", "Indonesia", "SE Asia Standard Time", -6.21, 106.85),
                new ZoneEntry("Surabaya", "SUB", "Indonesia", "SE Asia Standard Time", -7.26, 112.75),
                new ZoneEntry("Bali (Denpasar)", "DPS", "Indonesia", "Singapore Standard Time", -8.65, 115.22),
                new ZoneEntry("Kuala Lumpur", "KUL", "Malaysia", "Singapore Standard Time", 3.14, 101.69),
                new ZoneEntry("Singapore", "SIN", "Singapore", "Singapore Standard Time", 1.35, 103.82),
                new ZoneEntry("Manila", "MNL", "Philippines", "Singapore Standard Time", 14.60, 120.98),
                new ZoneEntry("Cebu", "CEB", "Philippines", "Singapore Standard Time", 10.32, 123.89),
                new ZoneEntry("Bandar Seri Begawan", "BWN", "Brunei", "Singapore Standard Time", 4.89, 114.94),
                new ZoneEntry("Hong Kong", "HKG", "Hong Kong", "China Standard Time", 22.32, 114.17),
                new ZoneEntry("Macau", "MFM", "Macau", "China Standard Time", 22.20, 113.54),
                new ZoneEntry("Shanghai", "SHA", "China", "China Standard Time", 31.23, 121.47),
                new ZoneEntry("Beijing", "BJS", "China", "China Standard Time", 39.90, 116.40),
                new ZoneEntry("Shenzhen", "SZX", "China", "China Standard Time", 22.54, 114.06),
                new ZoneEntry("Guangzhou", "CAN", "China", "China Standard Time", 23.13, 113.26),
                new ZoneEntry("Chengdu", "CTU", "China", "China Standard Time", 30.57, 104.07),
                new ZoneEntry("Chongqing", "CKG", "China", "China Standard Time", 29.56, 106.55),
                new ZoneEntry("Wuhan", "WUH", "China", "China Standard Time", 30.59, 114.31),
                new ZoneEntry("Hangzhou", "HGH", "China", "China Standard Time", 30.27, 120.15),
                new ZoneEntry("Nanjing", "NKG", "China", "China Standard Time", 32.06, 118.80),
                new ZoneEntry("Xi'an", "XIY", "China", "China Standard Time", 34.34, 108.94),
                new ZoneEntry("Tianjin", "TSN", "China", "China Standard Time", 39.08, 117.20),
                new ZoneEntry("Taipei", "TPE", "Taiwan", "Taipei Standard Time", 25.03, 121.57),
                new ZoneEntry("Kaohsiung", "KHH", "Taiwan", "Taipei Standard Time", 22.63, 120.30),
                new ZoneEntry("Ulaanbaatar", "ULN", "Mongolia", "Ulaanbaatar Standard Time", 47.89, 106.91),
                new ZoneEntry("Seoul", "SEL", "South Korea", "Korea Standard Time", 37.57, 126.98),
                new ZoneEntry("Busan", "PUS", "South Korea", "Korea Standard Time", 35.18, 129.08),
                new ZoneEntry("Incheon", "ICN", "South Korea", "Korea Standard Time", 37.46, 126.71),
                new ZoneEntry("Pyongyang", "FNJ", "North Korea", "North Korea Standard Time", 39.02, 125.75),
                new ZoneEntry("Tokyo", "TYO", "Japan", "Tokyo Standard Time", 35.68, 139.69),
                new ZoneEntry("Osaka", "OSA", "Japan", "Tokyo Standard Time", 34.69, 135.50),
                new ZoneEntry("Nagoya", "NGO", "Japan", "Tokyo Standard Time", 35.18, 136.91),
                new ZoneEntry("Sapporo", "SPK", "Japan", "Tokyo Standard Time", 43.06, 141.35),
                new ZoneEntry("Fukuoka", "FUK", "Japan", "Tokyo Standard Time", 33.59, 130.40),
                new ZoneEntry("Kyoto", "UKY", "Japan", "Tokyo Standard Time", 35.01, 135.77)),
            new ZoneRegion("Australia & Pacific",
                new ZoneEntry("Perth", "PER", "Australia", "W. Australia Standard Time", -31.95, 115.86),
                new ZoneEntry("Darwin", "DRW", "Australia", "AUS Central Standard Time", -12.46, 130.84),
                new ZoneEntry("Adelaide", "ADL", "Australia", "Cen. Australia Standard Time", -34.93, 138.60),
                new ZoneEntry("Brisbane", "BNE", "Australia", "E. Australia Standard Time", -27.47, 153.03),
                new ZoneEntry("Gold Coast", "OOL", "Australia", "E. Australia Standard Time", -28.02, 153.40),
                new ZoneEntry("Sydney", "SYD", "Australia", "AUS Eastern Standard Time", -33.87, 151.21),
                new ZoneEntry("Melbourne", "MEL", "Australia", "AUS Eastern Standard Time", -37.81, 144.96),
                new ZoneEntry("Canberra", "CBR", "Australia", "AUS Eastern Standard Time", -35.28, 149.13),
                new ZoneEntry("Hobart", "HBA", "Australia", "Tasmania Standard Time", -42.88, 147.33),
                new ZoneEntry("Port Moresby", "POM", "Papua New Guinea", "West Pacific Standard Time", -9.44, 147.18),
                new ZoneEntry("Guam", "GUM", "Guam", "West Pacific Standard Time", 13.44, 144.79),
                new ZoneEntry("Nouméa", "NOU", "New Caledonia", "Central Pacific Standard Time", -22.28, 166.46),
                new ZoneEntry("Honiara", "HIR", "Solomon Islands", "Central Pacific Standard Time", -9.43, 159.95),
                new ZoneEntry("Auckland", "AKL", "New Zealand", "New Zealand Standard Time", -36.85, 174.76),
                new ZoneEntry("Wellington", "WLG", "New Zealand", "New Zealand Standard Time", -41.29, 174.78),
                new ZoneEntry("Christchurch", "CHC", "New Zealand", "New Zealand Standard Time", -43.53, 172.64),
                new ZoneEntry("Suva", "SUV", "Fiji", "Fiji Standard Time", -18.14, 178.44),
                new ZoneEntry("Nuku'alofa", "TBU", "Tonga", "Tonga Standard Time", -21.14, -175.20),
                new ZoneEntry("Apia", "APW", "Samoa", "Samoa Standard Time", -13.83, -171.76),
                new ZoneEntry("Papeete", "PPT", "French Polynesia", "Hawaiian Standard Time", -17.54, -149.57),
                new ZoneEntry("Pago Pago", "PPG", "American Samoa", "UTC-11", -14.28, -170.70))
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
