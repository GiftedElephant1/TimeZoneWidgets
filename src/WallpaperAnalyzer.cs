using System;
using System.Drawing;
using System.IO;
using Microsoft.Win32;

namespace TimeZoneWidgets
{
    /// <summary>The three colors a widget is painted with.</summary>
    public class Theme
    {
        public Color Background;
        public Color Text;
        public Color Accent;
        /// <summary>True when the panel is dark with light text.</summary>
        public bool DarkPanel;

        public static Theme FromSettings(AppSettings s)
        {
            var t = new Theme();
            t.Background = ParseColor(s.BackgroundColor, Color.FromArgb(22, 24, 42));
            t.Text = ParseColor(s.TextColor, Color.FromArgb(242, 244, 250));
            t.Accent = ParseColor(s.AccentColor, Color.FromArgb(111, 177, 255));
            t.DarkPanel = Luminance(t.Background) < 0.5;
            return t;
        }

        public static Color ParseColor(string html, Color fallback)
        {
            try
            {
                if (!string.IsNullOrEmpty(html)) return ColorTranslator.FromHtml(html);
            }
            catch { }
            return fallback;
        }

        public static string ToHtml(Color c)
        {
            return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }

        public static double Luminance(Color c)
        {
            return (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
        }
    }

    /// <summary>
    /// Looks at the current desktop wallpaper (entirely offline) and derives a theme that
    /// contrasts with it: a dark panel on light wallpapers, a light panel on dark ones, with the
    /// clock colored by the wallpaper's dominant hue so it feels like it belongs.
    /// </summary>
    public static class WallpaperAnalyzer
    {
        public static string GetWallpaperPath()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"))
                {
                    if (key != null)
                    {
                        var path = key.GetValue("WallPaper") as string;
                        if (!string.IsNullOrEmpty(path) && File.Exists(path)) return path;
                    }
                }
            }
            catch { }

            // Slideshows, Spotlight and some picture formats only exist as the transcoded copy.
            var transcoded = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Themes\TranscodedWallpaper");
            if (File.Exists(transcoded)) return transcoded;
            return null;
        }

        /// <summary>A cheap fingerprint of the wallpaper so we only re-analyze when it changes.</summary>
        public static string Signature()
        {
            try
            {
                var path = GetWallpaperPath();
                if (path == null) return "none:" + AppsUseLightTheme();
                var info = new FileInfo(path);
                return path + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks;
            }
            catch
            {
                return "error";
            }
        }

        public static Theme ComputeTheme()
        {
            bool wallpaperIsLight;
            Color dominant;
            if (!TryAnalyze(out wallpaperIsLight, out dominant))
            {
                // No readable wallpaper (solid color, locked file...): fall back to the Windows app theme
                // and the user's accent color so the result still looks native.
                wallpaperIsLight = AppsUseLightTheme();
                dominant = DwmAccent();
            }
            return Build(wallpaperIsLight, dominant);
        }

        static bool TryAnalyze(out bool isLight, out Color dominant)
        {
            isLight = true;
            dominant = DwmAccent();
            var path = GetWallpaperPath();
            if (path == null) return false;
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var image = Image.FromStream(stream, false, false))
                using (var small = new Bitmap(image, new Size(64, 36)))
                {
                    double lumSum = 0;
                    int count = 0;
                    // 12 hue buckets: find the most common vivid hue and average those pixels.
                    var bucketCount = new int[12];
                    var bucketR = new long[12];
                    var bucketG = new long[12];
                    var bucketB = new long[12];
                    for (int y = 0; y < small.Height; y++)
                    {
                        for (int x = 0; x < small.Width; x++)
                        {
                            var c = small.GetPixel(x, y);
                            lumSum += Theme.Luminance(c);
                            count++;
                            float sat = c.GetSaturation();
                            float bri = c.GetBrightness();
                            if (sat >= 0.30f && bri >= 0.15f && bri <= 0.90f)
                            {
                                int bucket = (int)(c.GetHue() / 30f) % 12;
                                bucketCount[bucket]++;
                                bucketR[bucket] += c.R;
                                bucketG[bucket] += c.G;
                                bucketB[bucket] += c.B;
                            }
                        }
                    }
                    if (count == 0) return false;
                    isLight = (lumSum / count) >= 0.45;

                    int best = -1;
                    for (int i = 0; i < 12; i++)
                        if (bucketCount[i] > 0 && (best < 0 || bucketCount[i] > bucketCount[best])) best = i;
                    // Require at least 4% vivid pixels before trusting the hue; otherwise keep the accent color.
                    if (best >= 0 && bucketCount[best] >= count * 0.04)
                    {
                        dominant = Color.FromArgb(
                            (int)(bucketR[best] / bucketCount[best]),
                            (int)(bucketG[best] / bucketCount[best]),
                            (int)(bucketB[best] / bucketCount[best]));
                    }
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        static Theme Build(bool wallpaperIsLight, Color dominant)
        {
            var t = new Theme();
            if (wallpaperIsLight)
            {
                // Dark, slightly tinted glass with a bright accent for the time.
                t.DarkPanel = true;
                t.Background = Mix(Color.FromArgb(20, 22, 32), dominant, 0.14);
                t.Text = Color.FromArgb(243, 245, 250);
                t.Accent = WithHsl(dominant, 0.55f, 0.62f, 0.78f);
            }
            else
            {
                // Light frosted panel with a deep accent so it stands out on a dark wallpaper.
                t.DarkPanel = false;
                t.Background = Mix(Color.FromArgb(247, 248, 252), dominant, 0.10);
                t.Text = Color.FromArgb(24, 26, 34);
                t.Accent = WithHsl(dominant, 0.60f, 0.28f, 0.42f);
            }
            return t;
        }

        public static bool AppsUseLightTheme()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        var v = key.GetValue("AppsUseLightTheme");
                        if (v is int) return (int)v != 0;
                    }
                }
            }
            catch { }
            return true;
        }

        /// <summary>The Windows accent color (which users can set to follow their wallpaper).</summary>
        public static Color DwmAccent()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
                {
                    if (key != null)
                    {
                        var v = key.GetValue("AccentColor");
                        if (v is int)
                        {
                            uint abgr = unchecked((uint)(int)v);
                            return Color.FromArgb((int)(abgr & 0xFF), (int)((abgr >> 8) & 0xFF), (int)((abgr >> 16) & 0xFF));
                        }
                    }
                }
            }
            catch { }
            return Color.FromArgb(0, 120, 212);
        }

        static Color Mix(Color a, Color b, double amountOfB)
        {
            double k = Math.Max(0, Math.Min(1, amountOfB));
            return Color.FromArgb(
                (int)Math.Round(a.R * (1 - k) + b.R * k),
                (int)Math.Round(a.G * (1 - k) + b.G * k),
                (int)Math.Round(a.B * (1 - k) + b.B * k));
        }

        /// <summary>Keeps the hue but forces saturation to at least minSat and lightness into [minL, maxL].</summary>
        static Color WithHsl(Color c, float minSat, float minL, float maxL)
        {
            float h = c.GetHue();
            float s = Math.Max(c.GetSaturation(), minSat);
            float l = c.GetBrightness();
            if (l < minL) l = minL;
            if (l > maxL) l = maxL;
            return FromHsl(h, s, l);
        }

        public static Color FromHsl(float h, float s, float l)
        {
            h = ((h % 360f) + 360f) % 360f;
            float c = (1 - Math.Abs(2 * l - 1)) * s;
            float x = c * (1 - Math.Abs((h / 60f) % 2 - 1));
            float m = l - c / 2;
            float r, g, b;
            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }
            return Color.FromArgb(Clamp((r + m) * 255), Clamp((g + m) * 255), Clamp((b + m) * 255));
        }

        static int Clamp(float v)
        {
            return (int)Math.Max(0, Math.Min(255, Math.Round(v)));
        }
    }
}
