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
    /// Editable single-valued horizon: a list of azimuth/altitude points. Altitude lookup mirrors
    /// N.I.N.A.'s CustomHorizon exactly (NINA.Core/Model/CustomHorizon.cs: GroomHorizonData +
    /// linear Interpolate1D), so the views show what N.I.N.A. and Target Scheduler will use.
    /// N.I.N.A. keeps one altitude per azimuth (last line wins), so vertical steps are stored as
    /// two points <see cref="StepOffset"/> apart - see <see cref="ResolveStep"/>.
    /// Reads/writes the .hrz text format ("azimuth altitude" pairs).
    /// </summary>
    public class HorizonModel {
        /// <summary>Azimuth resolution = the precision Save writes ("0.##").</summary>
        public const double StepOffset = 0.01;
        private const double SameAzTolerance = 0.005;

        public List<HorizonPoint> Points { get; private set; } = new List<HorizonPoint>();

        public bool HasUnsaved => Points.Any(p => p.Unsaved);

        public void Sort() => Points = Points.OrderBy(p => p.Azimuth).ToList();

        /// <summary>Wrap into [0, 360) but keep exactly 360 - N.I.N.A. treats 0 and 360 as separate points.</summary>
        public static double NormalizeAzimuth(double az) {
            az = Math.Round(az, 2);
            if (az == 360) return 360;
            az = ((az % 360) + 360) % 360;
            return Math.Round(az, 2);
        }

        private static bool SameAz(double a, double b) => Math.Abs(a - b) < SameAzTolerance;

        /// <summary>Horizon altitude at an azimuth, computed exactly as N.I.N.A.'s CustomHorizon does.</summary>
        public double GetAltitude(double azimuth) {
            if (Points.Count == 0) return 0;
            if (Points.Count == 1) return Points[0].Altitude; // N.I.N.A. rejects this; flat line for editing only

            // CustomHorizon.GetAltitude: wrap only outside [0, 359]
            if (azimuth < 0 || azimuth > 359) azimuth = ((azimuth % 360) + 360) % 360;

            var map = new SortedDictionary<double, double>();
            foreach (var p in Points) map[p.Azimuth] = p.Altitude; // duplicate azimuth: last wins

            // CustomHorizon.GroomHorizonData: no interpolation across the seam
            if (!map.ContainsKey(0) && !map.ContainsKey(360)) {
                var nearest0 = map.Keys.OrderBy(x => Math.Abs(x)).First();
                var nearest360 = map.Keys.OrderByDescending(x => Math.Abs(x)).First();
                var key = 360 - nearest360 < nearest0 ? nearest360 : nearest0;
                map[0] = map[key];
                map[360] = map[key];
            } else if (!map.ContainsKey(0)) {
                map[0] = map[360];
            } else if (!map.ContainsKey(360)) {
                map[360] = map[0];
            }

            // Accord.Math.Tools.Interpolate1D(value, x, y, lower: 0, upper: 0)
            var xs = map.Keys.ToArray();
            var ys = map.Values.ToArray();
            for (int i = 0; i < xs.Length; i++) {
                if (azimuth < xs[i]) {
                    if (i == 0) return 0;
                    double t = (azimuth - xs[i - 1]) / (xs[i] - xs[i - 1]);
                    return ys[i - 1] + (ys[i] - ys[i - 1]) * t;
                }
            }
            return 0;
        }

        /// <summary>
        /// Call after <paramref name="p"/> was added or moved. If another point shares its azimuth,
        /// turn the pair into a vertical step N.I.N.A. can represent: the higher point stays, the lower
        /// one moves <see cref="StepOffset"/> towards the open side (the lower neighbour), so the
        /// obstruction is never under-protected. Returns false (model unchanged) if it can't be resolved.
        /// </summary>
        public bool ResolveStep(HorizonPoint p, out string message) {
            message = null;
            var others = Points.Where(o => o != p && SameAz(o.Azimuth, p.Azimuth)).ToList();
            if (others.Count == 0) return true;
            if (others.Count > 1) {
                message = $"Az {p.Azimuth:F2} already has {others.Count} points - move in Az first";
                return false;
            }
            var o = others[0];
            if (Math.Abs(o.Altitude - p.Altitude) < 0.005) {
                message = $"A point already exists at Az {p.Azimuth:F2} / Alt {p.Altitude:F2}";
                return false;
            }
            var x = o.Azimuth;
            if (Points.Any(q => q != p && q != o &&
                    (SameAz(q.Azimuth, NormalizeAzimuth(x + StepOffset)) || SameAz(q.Azimuth, NormalizeAzimuth(x - StepOffset))))) {
                message = $"Az {x:F2} already has a vertical step - move in Az first";
                return false;
            }

            var hi = p.Altitude >= o.Altitude ? p : o;
            var lo = hi == p ? o : p;
            double sign = OpenSide(x, p, o);
            double target = NormalizeAzimuth(x + sign * StepOffset);
            if (Points.Any(q => q != lo && SameAz(q.Azimuth, target))) {
                target = NormalizeAzimuth(x - sign * StepOffset);
                if (Points.Any(q => q != lo && SameAz(q.Azimuth, target))) {
                    message = $"No room for a vertical step at Az {x:F2}";
                    return false;
                }
            }
            hi.Azimuth = x;
            lo.Azimuth = target;
            lo.Unsaved = true;
            Sort();
            message = $"Vertical step at Az {x:F2}: lower point set to Az {target:F2}";
            return true;
        }

        /// <summary>+1 if the open (lower) side of a step at <paramref name="x"/> is towards higher azimuth, else -1.</summary>
        private double OpenSide(double x, HorizonPoint a, HorizonPoint b) {
            var rest = Points.Where(q => q != a && q != b).ToList();
            if (rest.Count == 0) return 1;
            var prev = rest.Where(q => q.Azimuth < x).OrderByDescending(q => q.Azimuth).FirstOrDefault()
                       ?? rest.OrderByDescending(q => q.Azimuth).First();
            var next = rest.Where(q => q.Azimuth > x).OrderBy(q => q.Azimuth).FirstOrDefault()
                       ?? rest.OrderBy(q => q.Azimuth).First();
            return next.Altitude <= prev.Altitude ? 1 : -1;
        }

        public HorizonModel Clone() {
            var m = new HorizonModel {
                Points = Points.Select(p => new HorizonPoint(p.Azimuth, p.Altitude, p.Unsaved)).ToList()
            };
            return m;
        }

        public static HorizonModel Load(string filePath) => Load(filePath, out _);

        /// <summary>
        /// Reads a .hrz like N.I.N.A. does (only lines with exactly 2 values, '#' = comment), then makes
        /// it representable: azimuths wrapped/rounded to 0.01°, exact duplicates dropped, duplicate
        /// azimuths turned into vertical steps. Points changed that way are marked unsaved; a summary
        /// is returned in <paramref name="report"/> (null if the file needed no changes).
        /// </summary>
        public static HorizonModel Load(string filePath, out string report) {
            report = null;
            var m = new HorizonModel();
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return m;

            int skipped = 0, wrapped = 0, dropped = 0, steps = 0, unresolved = 0;
            foreach (var raw in File.ReadAllLines(filePath)) {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var parts = line.Split(new[] { ' ', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2 ||
                    !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var az) ||
                    !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var alt)) {
                    skipped++;
                    continue;
                }
                var norm = NormalizeAzimuth(az);
                bool changed = Math.Abs(norm - az) >= SameAzTolerance;
                if (changed) wrapped++;
                m.Points.Add(new HorizonPoint(norm, alt, changed));
            }
            m.Sort();

            // Exact duplicates (same az + alt) carry no information - drop them.
            for (int i = m.Points.Count - 1; i > 0; i--) {
                var a = m.Points[i]; var b = m.Points[i - 1];
                if (SameAz(a.Azimuth, b.Azimuth) && Math.Abs(a.Altitude - b.Altitude) < 0.005) {
                    m.Points.RemoveAt(i);
                    dropped++;
                }
            }

            // Same azimuth, different altitude: keep the extremes as a vertical step.
            foreach (var group in m.Points.GroupBy(p => Math.Round(p.Azimuth, 2)).Where(g => g.Count() > 1).ToList()) {
                var pts = group.OrderBy(p => p.Altitude).ToList();
                for (int i = 1; i < pts.Count - 1; i++) { m.Points.Remove(pts[i]); dropped++; }
                if (m.ResolveStep(pts[0], out _)) steps++;
                else { m.Points.Remove(pts[0]); unresolved++; }
            }
            m.Sort();

            var notes = new List<string>();
            if (steps > 0) notes.Add($"{steps} vertical step(s) offset {StepOffset}°");
            if (dropped > 0) notes.Add($"{dropped} duplicate point(s) removed");
            if (unresolved > 0) notes.Add($"{unresolved} conflicting point(s) removed");
            if (wrapped > 0) notes.Add($"{wrapped} azimuth(s) wrapped into 0-360");
            if (skipped > 0) notes.Add($"{skipped} line(s) ignored (N.I.N.A. ignores them too)");
            if (notes.Count > 0) report = "Loaded with fixes: " + string.Join(", ", notes) + (m.HasUnsaved ? " - review red points and Save" : "");
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
