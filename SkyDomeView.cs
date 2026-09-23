using NINA.Core.Model;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace InteractiveHorizonBuilder {

    /// <summary>
    /// Phase 2 Sky Dome: a polar plot of the custom horizon. Zenith at centre, altitude 0 at the
    /// rim, azimuth measured clockwise from North (N up, E right, S down, W left) - matching
    /// N.I.N.A.'s Azimuth Chart. Obstruction (below the horizon line) is filled; the live mount
    /// position is a marker. Drawn directly with a DrawingContext; no external chart library.
    /// </summary>
    public class SkyDomeView : FrameworkElement {

        public static readonly DependencyProperty HorizonProperty =
            DependencyProperty.Register(nameof(Horizon), typeof(CustomHorizon), typeof(SkyDomeView),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MountAzimuthProperty =
            DependencyProperty.Register(nameof(MountAzimuth), typeof(double), typeof(SkyDomeView),
                new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MountAltitudeProperty =
            DependencyProperty.Register(nameof(MountAltitude), typeof(double), typeof(SkyDomeView),
                new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

        public CustomHorizon Horizon {
            get => (CustomHorizon)GetValue(HorizonProperty);
            set => SetValue(HorizonProperty, value);
        }

        public double MountAzimuth {
            get => (double)GetValue(MountAzimuthProperty);
            set => SetValue(MountAzimuthProperty, value);
        }

        public double MountAltitude {
            get => (double)GetValue(MountAltitudeProperty);
            set => SetValue(MountAltitudeProperty, value);
        }

        private static readonly Brush SkyBrush = Frozen(Color.FromRgb(0x0E, 0x16, 0x26));
        private static readonly Brush TerrainBrush = Frozen(Color.FromRgb(0x3A, 0x2A, 0x18));
        private static readonly Pen HorizonPen = FrozenPen(Color.FromRgb(0xC8, 0x79, 0x2E), 2.0);
        private static readonly Pen GridPen = FrozenPen(Color.FromRgb(0x33, 0x41, 0x52), 1.0);
        private static readonly Pen RimPen = FrozenPen(Color.FromRgb(0x8A, 0x5A, 0x2E), 1.5);
        private static readonly Brush GridLabelBrush = Frozen(Color.FromRgb(0x9A, 0xA4, 0xB2));
        private static readonly Brush CardinalBrush = Frozen(Color.FromRgb(0xE0, 0xA4, 0x00));
        private static readonly Brush MountBrush = Frozen(Color.FromRgb(0x3F, 0xC8, 0xE0));
        private static readonly Pen MountPen = FrozenPen(Color.FromRgb(0x3F, 0xC8, 0xE0), 1.5);

        private static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
        private static Pen FrozenPen(Color c, double w) { var p = new Pen(Frozen(c), w); p.Freeze(); return p; }

        private static Point Polar(Point center, double radius, double alt, double az) {
            if (alt < 0) alt = 0;
            if (alt > 90) alt = 90;
            double r = radius * (90.0 - alt) / 90.0;
            double a = az * Math.PI / 180.0;
            return new Point(center.X + r * Math.Sin(a), center.Y - r * Math.Cos(a));
        }

        protected override void OnRender(DrawingContext dc) {
            double w = ActualWidth, h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            var center = new Point(w / 2.0, h / 2.0);
            double radius = Math.Min(w, h) / 2.0 - 18;
            if (radius <= 0) return;

            dc.DrawEllipse(SkyBrush, null, center, radius, radius);

            if (Horizon != null) {
                var horizonFig = new PathFigure { IsClosed = true, StartPoint = Polar(center, radius, SafeAlt(0), 0) };
                for (int az = 1; az <= 360; az++) {
                    horizonFig.Segments.Add(new LineSegment(Polar(center, radius, SafeAlt(az % 360), az), true));
                }
                var horizonGeo = new PathGeometry();
                horizonGeo.Figures.Add(horizonFig);

                var ring = new GeometryGroup { FillRule = FillRule.EvenOdd };
                ring.Children.Add(new EllipseGeometry(center, radius, radius));
                ring.Children.Add(horizonGeo);
                dc.DrawGeometry(TerrainBrush, null, ring);
                dc.DrawGeometry(null, HorizonPen, horizonGeo);
            }

            for (int alt = 15; alt < 90; alt += 15) {
                double rr = radius * (90.0 - alt) / 90.0;
                dc.DrawEllipse(null, GridPen, center, rr, rr);
                DrawText(dc, alt + "°", new Point(center.X + 4, center.Y - rr - 1), GridLabelBrush, 10);
            }
            dc.DrawEllipse(null, RimPen, center, radius, radius);

            for (int az = 0; az < 360; az += 30) {
                var outer = Polar(center, radius, 0, az);
                dc.DrawLine(GridPen, center, outer);
            }

            DrawCardinal(dc, "N", center, radius, 0);
            DrawCardinal(dc, "E", center, radius, 90);
            DrawCardinal(dc, "S", center, radius, 180);
            DrawCardinal(dc, "W", center, radius, 270);

            if (!double.IsNaN(MountAzimuth) && !double.IsNaN(MountAltitude)) {
                var p = Polar(center, radius, MountAltitude, MountAzimuth);
                double s = 6;
                var diamond = new StreamGeometry();
                using (var ctx = diamond.Open()) {
                    ctx.BeginFigure(new Point(p.X, p.Y - s), true, true);
                    ctx.LineTo(new Point(p.X + s, p.Y), true, false);
                    ctx.LineTo(new Point(p.X, p.Y + s), true, false);
                    ctx.LineTo(new Point(p.X - s, p.Y), true, false);
                }
                diamond.Freeze();
                dc.DrawGeometry(MountBrush, MountPen, diamond);
            }
        }

        private double SafeAlt(int az) {
            try { return Horizon.GetAltitude(az); } catch { return 0; }
        }

        private void DrawCardinal(DrawingContext dc, string label, Point center, double radius, double az) {
            var p = Polar(center, radius + 12, 0, az);
            DrawText(dc, label, new Point(p.X - 5, p.Y - 8), CardinalBrush, 14, true);
        }

        private void DrawText(DrawingContext dc, string text, Point at, Brush brush, double size, bool bold = false) {
            var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal,
                    bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal),
                size, brush, 1.0);
            dc.DrawText(ft, at);
        }
    }
}
