using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace TimeZoneWidgets
{
    /// <summary>One clock widget: which zone it shows, its custom label and its screen position.</summary>
    [DataContract]
    public class WidgetConfig
    {
        [DataMember(Order = 0)] public string TimeZoneId { get; set; }
        [DataMember(Order = 1)] public string Label { get; set; }
        [DataMember(Order = 2)] public int X { get; set; }
        [DataMember(Order = 3)] public int Y { get; set; }
    }

    /// <summary>Everything the user can configure. Persisted as JSON under %APPDATA%\TimeZoneWidgets.</summary>
    [DataContract]
    public class AppSettings
    {
        public const string LayerNormal = "normal";
        public const string LayerTop = "top";
        public const string LayerBottom = "bottom";
        public const string LayoutFloating = "floating";
        public const string LayoutRibbon = "ribbon";
        public const string EdgeTop = "top";
        public const string EdgeBottom = "bottom";
        public const string EdgeLeft = "left";
        public const string EdgeRight = "right";
        public const int MinRibbonHeight = 26, MaxRibbonHeight = 240;
        public const int DefaultRibbonHeight = 44;
        public const int CurrentVersion = 3;
        public const int MinRibbonWidth = 120, MaxRibbonWidth = 420;

        [DataMember(Order = 0)] public int Version { get; set; }
        [DataMember(Order = 1)] public bool Use24Hour { get; set; }
        [DataMember(Order = 2)] public bool ShowSeconds { get; set; }
        [DataMember(Order = 3)] public bool ShowUtcOffset { get; set; }
        /// <summary>Widget size in percent (50..200).</summary>
        [DataMember(Order = 4)] public int Scale { get; set; }
        /// <summary>Widget opacity in percent (20..100). 100 = fully opaque.</summary>
        [DataMember(Order = 5)] public int Opacity { get; set; }
        /// <summary>When true, colors are derived from the current wallpaper.</summary>
        [DataMember(Order = 6)] public bool AutoTheme { get; set; }
        [DataMember(Order = 7)] public string BackgroundColor { get; set; }
        [DataMember(Order = 8)] public string TextColor { get; set; }
        [DataMember(Order = 9)] public string AccentColor { get; set; }
        /// <summary>"normal", "top" (always on top) or "bottom" (pinned behind other windows).</summary>
        [DataMember(Order = 10)] public string Layer { get; set; }
        [DataMember(Order = 11)] public bool LockPositions { get; set; }
        [DataMember(Order = 12)] public List<WidgetConfig> Widgets { get; set; }
        /// <summary>Whether the day/night globe widget is shown.</summary>
        [DataMember(Order = 13)] public bool ShowGlobe { get; set; }
        /// <summary>Whether the globe turns with real time (one rotation per day, sun fixed on screen).
        /// Off: the view stays on the chosen longitude and only the day/night line moves.</summary>
        [DataMember(Order = 14)] public bool GlobeAutoRotate { get; set; }
        [DataMember(Order = 15)] public int GlobeX { get; set; }
        [DataMember(Order = 16)] public int GlobeY { get; set; }
        /// <summary>"floating" (separate widgets) or "ribbon" (one bar docked to a screen edge).</summary>
        [DataMember(Order = 17)] public string LayoutMode { get; set; }
        [DataMember(Order = 18)] public string RibbonEdge { get; set; }
        /// <summary>Ribbon height (top/bottom) and width (left/right), in design pixels at 100% scale.</summary>
        [DataMember(Order = 19)] public int RibbonHeight { get; set; }
        [DataMember(Order = 20)] public int RibbonWidth { get; set; }
        /// <summary>True: register the ribbon as an appbar that reserves screen space like the taskbar
        /// (maximized windows shrink to sit beside it). False (default): overlay the screen.</summary>
        [DataMember(Order = 21)] public bool RibbonReserveSpace { get; set; }
        /// <summary>Show three-letter city codes (NYC, TPE, AMS) on the ribbon instead of full names.</summary>
        [DataMember(Order = 22)] public bool RibbonCityCodes { get; set; }
        /// <summary>Slide the ribbon off-screen (leaving a thin line) and bring it back when the mouse
        /// touches that screen edge, like the taskbar's auto-hide. Implies overlay and always-on-top.</summary>
        [DataMember(Order = 23)] public bool RibbonAutoHide { get; set; }

        public static string Directory
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TimeZoneWidgets"); }
        }

        public static string FilePath
        {
            get { return Path.Combine(Directory, "settings.json"); }
        }

        public static AppSettings CreateDefault()
        {
            var s = new AppSettings();
            s.Version = CurrentVersion;
            s.Use24Hour = false;
            s.ShowSeconds = false;
            s.ShowUtcOffset = true;
            s.Scale = 100;
            s.Opacity = 92;
            s.AutoTheme = true;
            s.BackgroundColor = "#16182A";
            s.TextColor = "#F2F4FA";
            s.AccentColor = "#6FB1FF";
            s.Layer = LayerNormal;
            s.LockPositions = false;
            s.Widgets = new List<WidgetConfig>();
            s.ShowGlobe = true;
            s.GlobeAutoRotate = true;
            s.GlobeX = -1;
            s.GlobeY = -1;
            s.LayoutMode = LayoutFloating;
            s.RibbonEdge = EdgeTop;
            s.RibbonHeight = DefaultRibbonHeight;
            s.RibbonWidth = 210;
            s.RibbonReserveSpace = false;
            s.RibbonCityCodes = true;
            s.RibbonAutoHide = false;
            return s;
        }

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    // Tolerate a UTF-8 byte-order mark (Notepad and PowerShell like to add one).
                    var bytes = File.ReadAllBytes(FilePath);
                    int skip = (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) ? 3 : 0;
                    using (var stream = new MemoryStream(bytes, skip, bytes.Length - skip))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(AppSettings));
                        var loaded = (AppSettings)serializer.ReadObject(stream);
                        if (loaded != null)
                        {
                            loaded.Normalize();
                            return loaded;
                        }
                    }
                }
            }
            catch
            {
                // A corrupt file should never stop the app from starting; fall back to defaults.
            }
            return CreateDefault();
        }

        /// <summary>Fill in anything missing from an older or hand-edited settings file.</summary>
        public void Normalize()
        {
            var d = CreateDefault();
            if (Version <= 0)
            {
                // Settings written before the globe existed: turn it on like a fresh install would.
                Version = 1;
                ShowGlobe = true;
                GlobeAutoRotate = true;
                GlobeX = -1;
                GlobeY = -1;
            }
            if (Version < 2)
            {
                // v1.2: the horizontal ribbon became a thinner two-row strip that overlays the
                // screen by default. Move an untouched old default height to the new one.
                if (RibbonHeight == 84) RibbonHeight = d.RibbonHeight;
                RibbonReserveSpace = false;
                Version = 2;
            }
            if (Version < 3)
            {
                // v1.2: city codes on the ribbon (a missing JSON member deserializes as false).
                RibbonCityCodes = true;
                Version = 3;
            }
            if (Version < CurrentVersion) Version = CurrentVersion;
            if (Scale < 50 || Scale > 200) Scale = d.Scale;
            if (Opacity <= 0) Opacity = d.Opacity;
            if (Opacity < 20) Opacity = 20;
            if (Opacity > 100) Opacity = 100;
            if (string.IsNullOrEmpty(BackgroundColor)) BackgroundColor = d.BackgroundColor;
            if (string.IsNullOrEmpty(TextColor)) TextColor = d.TextColor;
            if (string.IsNullOrEmpty(AccentColor)) AccentColor = d.AccentColor;
            if (Layer != LayerTop && Layer != LayerBottom) Layer = LayerNormal;
            if (Widgets == null) Widgets = new List<WidgetConfig>();
            if (LayoutMode != LayoutRibbon) LayoutMode = LayoutFloating;
            if (RibbonEdge != EdgeBottom && RibbonEdge != EdgeLeft && RibbonEdge != EdgeRight) RibbonEdge = EdgeTop;
            if (RibbonHeight <= 0) RibbonHeight = d.RibbonHeight;
            if (RibbonWidth <= 0) RibbonWidth = d.RibbonWidth;
            RibbonHeight = Math.Max(MinRibbonHeight, Math.Min(MaxRibbonHeight, RibbonHeight));
            RibbonWidth = Math.Max(MinRibbonWidth, Math.Min(MaxRibbonWidth, RibbonWidth));
            Widgets.RemoveAll(delegate(WidgetConfig w) { return w == null || string.IsNullOrEmpty(w.TimeZoneId); });
            // Older versions labelled the PC's own zone "Local"; show the city instead.
            foreach (var w in Widgets)
                if (w.Label == "Local" && w.TimeZoneId == TimeZoneInfo.Local.Id) w.Label = ZoneCatalog.LocalCity();
        }

        public void Save()
        {
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                var tmp = FilePath + ".tmp";
                using (var stream = File.Create(tmp))
                {
                    var serializer = new DataContractJsonSerializer(typeof(AppSettings));
                    serializer.WriteObject(stream, this);
                }
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch
            {
                // Saving is best-effort; a locked file should not crash the widgets.
            }
        }
    }
}
