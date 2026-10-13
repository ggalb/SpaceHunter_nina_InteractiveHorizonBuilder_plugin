// Copyright © 2026 Space Hunter - Georg G Albrecht
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Windows;
using System.Windows.Media;

namespace InteractiveHorizonBuilder {

    /// <summary>
    /// Colours of the Sky Dome and Strip. <see cref="Classic"/> = the plugin's own fixed colours;
    /// <see cref="FromTheme"/> = built from N.I.N.A.'s colour-schema brushes (PrimaryBrush etc.),
    /// so the charts follow N.I.N.A.'s day / night (alternative) colours.
    /// </summary>
    public class ChartPalette {
        public Color Sky, Terrain, Horizon, DomeGrid, StripGrid, Rim, GridLabel, Cardinal,
                     Mount, Point, Unsaved, Selected, Crosshair, Sun, SunPath, Ring15, Ring30;

        public static readonly ChartPalette Classic = new ChartPalette {
            Sky = Color.FromRgb(0x0E, 0x16, 0x26),
            Terrain = Color.FromRgb(0x3A, 0x2A, 0x18),
            Horizon = Color.FromRgb(0xC8, 0x79, 0x2E),
            DomeGrid = Color.FromRgb(0x33, 0x41, 0x52),
            StripGrid = Color.FromRgb(0x2A, 0x36, 0x46),
            Rim = Color.FromRgb(0x8A, 0x5A, 0x2E),
            GridLabel = Color.FromRgb(0x9A, 0xA4, 0xB2),
            Cardinal = Color.FromRgb(0xE0, 0xA4, 0x00),
            Mount = Color.FromRgb(0x3F, 0xC8, 0xE0),
            Point = Color.FromRgb(0xC8, 0xCD, 0xD5),
            Unsaved = Color.FromRgb(0xE2, 0x4B, 0x4A),
            Selected = Color.FromRgb(0xED, 0xA1, 0x00),
            Crosshair = Color.FromRgb(0xFF, 0xFF, 0xFF),
            Sun = Color.FromRgb(0xFF, 0xD1, 0x00),
            SunPath = Color.FromRgb(0xE0, 0xA4, 0x00),
            Ring15 = Color.FromRgb(0xE2, 0x4B, 0x4A),
            Ring30 = Color.FromRgb(0xED, 0xA1, 0x00),
        };

        /// <summary>Palette from N.I.N.A.'s current colour schema; falls back to Classic per colour.</summary>
        public static ChartPalette FromTheme(FrameworkElement fe) {
            Color C(string key, Color fallback) =>
                fe.TryFindResource(key) is SolidColorBrush b ? b.Color : fallback;
            Color A(Color c, byte alpha) => Color.FromArgb(alpha, c.R, c.G, c.B);

            var k = Classic;
            var primary = C("PrimaryBrush", k.Horizon);
            var secondary = C("SecondaryBrush", k.Point);
            var border = C("BorderBrush", k.Rim);
            var warning = C("NotificationWarningBrush", k.Sun);
            var error = C("NotificationErrorBrush", k.Unsaved);
            return new ChartPalette {
                Sky = C("SecondaryBackgroundBrush", k.Sky),
                Terrain = C("ButtonBackgroundBrush", k.Terrain),
                Horizon = primary,
                DomeGrid = A(primary, 60),
                StripGrid = A(primary, 60),
                Rim = border,
                GridLabel = secondary,
                Cardinal = primary,
                Mount = warning,
                Point = secondary,
                Unsaved = error,
                Selected = primary,
                Crosshair = primary,
                Sun = warning,
                SunPath = A(warning, 180),
                Ring15 = error,
                Ring30 = warning,
            };
        }

        public static Brush Brush(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
        public static Pen Pen(Color c, double w) { var p = new Pen(Brush(c), w); p.Freeze(); return p; }
        public static Pen DashPen(Color c, double w) { var p = new Pen(Brush(c), w) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) }; p.Freeze(); return p; }
    }
}
