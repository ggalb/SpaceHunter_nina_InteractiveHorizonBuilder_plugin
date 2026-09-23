using NINA.Core.Model;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace InteractiveHorizonBuilder {

    /// <summary>
    /// Phase 2 Strip view: azimuth (x, 0-360, N-E-S-W-N) by altitude (y, 0-90) - the single-valued
    /// horizon exactly as N.I.N.A. reads it. Obstruction below the horizon line is filled; the live
    /// mount position is a vertical marker + dot. Companion to <see cref="SkyDomeView"/>.
    /// </summary>
    public class StripView : FrameworkElement {

        public static readonly DependencyProperty HorizonProperty =
            DependencyProperty.Register(nameof(Horizon), typeof(CustomHorizon), typeof(StripView),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MountAzimuthProperty =
            DependencyProperty.Register(nameof(MountAzimuth), typeof(double), typeof(StripView),
                new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MountAltitudeProperty =
            DependencyProperty.Register(nameof(MountAltitude), typeof(double), typeof(StripView),
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
        private static readonly Pen GridPen = FrozenPen(Color.FromRgb(0x2A, 0x36, 0x46), 1.0);
        private static readonly Pen AxisPen = FrozenPen(Color.FromRgb(0x8A, 0x5A, 0x2E), 1.5);
        private static readonly Brush GridLabelBrush = Frozen(Color.FromRgb(0x9A, 0xA4, 0xB2));
        private static readonly Brush CardinalBrush = Frozen(Color.FromRgb(0xE0, 0xA4, 0x00));
        private static readonly Pen MountPen = FrozenPen(Color.FromRgb(0x3F, 0xC8, 0xE0), 1.0);
        private static readonly Brush MountBrush = Frozen(Color.FromRgb(0x3F, 0xC8, 0xE0));

        private static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
        private static Pen FrozenPen(Color c, double w) { var p = new Pen(Frozen(c), w); p.Freeze(); return p; }

        protected override void OnRender(DrawingContext dc) {
            double w = ActualWidth, h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            double left = 28, right = 8, top = 6, bottom = 20;
            double plotW = w - left - right;
            double plotH = h - top - bottom;
            if (plotW <= 0 || plotH <= 0) return;

            double X(double az) => left + az / 360.0 * plotW;
            double Y(double alt) => top + (90.0 - Math.Max(0, Math.Min(90, alt))) / 90.0 * plotH;

            dc.DrawRectangle(SkyBrush, null, new Rect(left, top, plotW, plotH));

            for (int alt = 0; alt <= 90; alt += 15) {
                double y = Y(alt);
                dc.DrawLine(GridPen, new Point(left, y), new Point(left + plotW, y));
                DrawText(dc, alt.ToString(), new Point(2, y - 7), GridLabelBrush, 10);
            }
            for (int az = 0; az <= 360; az += 45) {
                double x = X(az);
                dc.DrawLine(GridPen, new Point(x, top), new Point(x, top + plotH));
            }

            if (Horizon != null) {
                var fig = new PathFigure { IsClosed = true, StartPoint = new Point(X(0), Y(0)) };
                for (int az = 0; az <= 360; az++) {
                    fig.Segments.Add(new LineSegment(new Point(X(az), Y(SafeAlt(az % 360))), true));
                }
                fig.Segments.Add(new LineSegment(new Point(X(360), Y(0)), true));
                var geo = new PathGeometry();
                geo.Figures.Add(fig);
                dc.DrawGeometry(TerrainBrush, null, geo);

                var line = new PathFigure { StartPoint = new Point(X(0), Y(SafeAlt(0))) };
                for (int az = 1; az <= 360; az++) {
                    line.Segments.Add(new LineSegment(new Point(X(az), Y(SafeAlt(az % 360))), true));
                }
                var lineGeo = new PathGeometry();
                lineGeo.Figures.Add(line);
                dc.DrawGeometry(null, HorizonPen, lineGeo);
            }

            dc.DrawRectangle(null, AxisPen, new Rect(left, top, plotW, plotH));

            DrawText(dc, "N", new Point(X(0) - 4, top + plotH + 4), CardinalBrush, 11, true);
            DrawText(dc, "E", new Point(X(90) - 4, top + plotH + 4), CardinalBrush, 11, true);
            DrawText(dc, "S", new Point(X(180) - 4, top + plotH + 4), CardinalBrush, 11, true);
            DrawText(dc, "W", new Point(X(270) - 4, top + plotH + 4), CardinalBrush, 11, true);
            DrawText(dc, "N", new Point(X(360) - 4, top + plotH + 4), CardinalBrush, 11, true);

            if (!double.IsNaN(MountAzimuth) && !double.IsNaN(MountAltitude)) {
                double x = X(((MountAzimuth % 360) + 360) % 360);
                dc.DrawLine(MountPen, new Point(x, top), new Point(x, top + plotH));
                var p = new Point(x, Y(MountAltitude));
                dc.DrawEllipse(MountBrush, null, p, 3.5, 3.5);
            }
        }

        private double SafeAlt(int az) {
            try { return Horizon.GetAltitude(az); } catch { return 0; }
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
