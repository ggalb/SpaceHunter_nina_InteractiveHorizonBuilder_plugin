// Copyright © 2026 Space Hunter - Georg G Albrecht
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace InteractiveHorizonBuilder {

    /// <summary>
    /// Packs the horizon as a Stellarium landscape ZIP (Sky and viewing options > Landscape >
    /// Add/remove landscapes > Install from ZIP): landscape.ini + the horizon list + a fully
    /// transparent panorama, so Stellarium shows only the horizon line, the same line N.I.N.A. uses.
    /// </summary>
    internal static class StellariumExport {

        public const string DefaultLineColor = ".55,.55,.10";

        /// <param name="name">Landscape name = folder name inside the ZIP = list file name.</param>
        /// <param name="lineColor">"r,g,b" with values 0-1, as Stellarium expects.</param>
        public static void Write(string zipPath, string name, IEnumerable<HorizonPoint> points,
                                 string lineColor, double latitude, double longitude, double elevation) {
            var listName = name + ".txt";
            var list = new StringBuilder();
            foreach (var p in points) {
                list.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0:0.##}\t{1:0.##}", p.Azimuth, p.Altitude));
            }

            var ini = new StringBuilder();
            ini.AppendLine("[landscape]");
            ini.AppendLine("name = " + name);
            ini.AppendLine("author = Interactive Horizon Builder (N.I.N.A. plugin)");
            ini.AppendLine("description = N.I.N.A. horizon exported " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            ini.AppendLine("type = spherical");
            ini.AppendLine("maptex = Transparent.png");
            ini.AppendLine("maptex_top = 40");
            ini.AppendLine("maptex_bottom = -40");
            ini.AppendLine("angle_rotatez = 90");
            ini.AppendLine("tesselat_rows = 40");
            ini.AppendLine("tesselat_cols = 40");
            ini.AppendLine("polygonal_horizon_list = " + listName);
            ini.AppendLine("polygonal_angle_rotatez = 0.00001");
            ini.AppendLine("polygonal_horizon_list_mode = azDeg_altDeg");
            ini.AppendLine("horizon_line_color = " + (string.IsNullOrWhiteSpace(lineColor) ? DefaultLineColor : lineColor.Trim()));
            ini.AppendLine();
            // Only used when "Location from landscape" is ticked in Stellarium.
            ini.AppendLine("[location]");
            ini.AppendLine("planet = Earth");
            ini.AppendLine("latitude = " + Dms(latitude));
            ini.AppendLine("longitude = " + Dms(longitude));
            ini.AppendLine("altitude = " + Math.Round(elevation).ToString(CultureInfo.InvariantCulture));

            if (File.Exists(zipPath)) File.Delete(zipPath);
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create)) {
                AddText(zip, name + "/landscape.ini", ini.ToString());
                AddText(zip, name + "/" + listName, list.ToString());
                using (var s = zip.CreateEntry(name + "/Transparent.png").Open()) {
                    WriteTransparentPng(s);
                }
            }
        }

        private static void AddText(ZipArchive zip, string entry, string text) {
            using (var w = new StreamWriter(zip.CreateEntry(entry).Open(), new UTF8Encoding(false))) {
                w.Write(text);
            }
        }

        private static void WriteTransparentPng(Stream s) {
            const int w = 64, h = 32;
            var pixels = new byte[w * h * 4]; // all zero = fully transparent
            var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, w * 4);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            enc.Save(s);
        }

        /// <summary>Stellarium's ini angle format, e.g. +33d47'52.55" (negative longitude = west).</summary>
        private static string Dms(double deg) {
            var sign = deg < 0 ? "-" : "+";
            var a = Math.Abs(deg);
            int d = (int)a;
            int m = (int)((a - d) * 60);
            double sec = ((a - d) * 60 - m) * 60;
            return string.Format(CultureInfo.InvariantCulture, "{0}{1}d{2}'{3:0.00}\"", sign, d, m, sec);
        }
    }
}
