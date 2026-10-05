// Copyright © 2026 Space Hunter - Georg G Albrecht
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace InteractiveHorizonBuilder {

    /// <summary>
    /// Result of a click on a chart: the az/alt under the cursor, plus the index of an existing
    /// point if the click landed within the pixel threshold of one (-1 = empty space -> place).
    /// </summary>
    public class PickResult {
        public double Azimuth { get; set; }
        public double Altitude { get; set; }
        public int Index { get; set; } = -1;

        public PickResult(double az, double alt, int index) {
            Azimuth = az; Altitude = alt; Index = index;
        }
    }

    /// <summary>One editable horizon vertex. Unsaved = added/changed since the last file save.</summary>
    public class HorizonPoint {
        public double Azimuth { get; set; }
        public double Altitude { get; set; }
        public bool Unsaved { get; set; }

        public HorizonPoint(double az, double alt, bool unsaved = false) {
            Azimuth = az; Altitude = alt; Unsaved = unsaved;
        }
    }

    /// <summary>
    /// Editable single-valued horizon: a list of azimuth/altitude points plus linear interpolation
    /// with wrap-around (matching how N.I.N.A. reads a .hrz). Kept separate from N.I.N.A.'s own
    /// CustomHorizon so edits render live before saving. Reads/writes the .hrz text format
    /// ("azimuth altitude" pairs).
    /// </summary>
    public class HorizonModel {
        public List<HorizonPoint> Points { get; private set; } = new List<HorizonPoint>();

        public bool HasUnsaved => Points.Any(p => p.Unsaved);

        public void Sort() => Points = Points.OrderBy(p => p.Azimuth).ToList();

        /// <summary>Linear-interpolated altitude at an azimuth, wrapping across 360/0.</summary>
        public double GetAltitude(double azimuth) {
            if (Points.Count == 0) return 0;
            if (Points.Count == 1) return Points[0].Altitude;

            double az = ((azimuth % 360) + 360) % 360;
            var pts = Points; // assumed sorted by Sort()

            for (int i = 0; i < pts.Count - 1; i++) {
                if (az >= pts[i].Azimuth && az <= pts[i + 1].Azimuth) {
                    return Lerp(pts[i], pts[i + 1], az, pts[i].Azimuth, pts[i + 1].Azimuth);
                }
            }
            // Wrap segment: from the last point (>= its az) around through 0 to the first point.
            var last = pts[pts.Count - 1];
            var first = pts[0];
            double span = (360 - last.Azimuth) + first.Azimuth;
            if (span <= 0) return first.Altitude;
            double along = az >= last.Azimuth ? az - last.Azimuth : (360 - last.Azimuth) + az;
            return last.Altitude + (first.Altitude - last.Altitude) * (along / span);
        }

        private static double Lerp(HorizonPoint a, HorizonPoint b, double az, double a0, double a1) {
            if (a1 <= a0) return a.Altitude;
            return a.Altitude + (b.Altitude - a.Altitude) * ((az - a0) / (a1 - a0));
        }

        public HorizonModel Clone() {
            var m = new HorizonModel {
                Points = Points.Select(p => new HorizonPoint(p.Azimuth, p.Altitude, p.Unsaved)).ToList()
            };
            return m;
        }

        public static HorizonModel Load(string filePath) {
            var m = new HorizonModel();
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return m;

            foreach (var raw in File.ReadAllLines(filePath)) {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("//")) continue;
                var parts = line.Split(new[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var az) &&
                    double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var alt)) {
                    m.Points.Add(new HorizonPoint(((az % 360) + 360) % 360, alt));
                }
            }
            m.Sort();
            return m;
        }

        public void Save(string filePath) {
            Sort();
            using (var sw = new StreamWriter(filePath, false)) {
                foreach (var p in Points) {
                    sw.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0:0.##} {1:0.##}", p.Azimuth, p.Altitude));
                }
            }
            foreach (var p in Points) p.Unsaved = false;
        }
    }
}
