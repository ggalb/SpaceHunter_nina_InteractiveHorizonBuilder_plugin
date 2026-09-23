using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace InteractiveHorizonBuilder {

    /// <summary>
    /// Sky Dome: polar plot of the editable horizon. Zenith centre, altitude 0 at the rim, azimuth
    /// clockwise from North (N up, E right, S down, W left) - matching N.I.N.A.'s Azimuth Chart.
    /// Draws obstruction fill, point dots, the live mount, a crosshair, and the selected point;
    /// clicking reports an az/alt back via PointPickedCommand.
    /// </summary>
    public class SkyDomeView : FrameworkElement {

        public static readonly DependencyProperty HorizonProperty =
            DependencyProperty.Register(nameof(Horizon), typeof(HorizonModel), typeof(SkyDomeView),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty MountAzimuthProperty =
            DependencyProperty.Register(nameof(MountAzimuth), typeof(double), typeof(SkyDomeView),
                new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty MountAltitudeProperty =
            DependencyProperty.Register(nameof(MountAltitude), typeof(double), typeof(SkyDomeView),
                new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty CrosshairAzimuthProperty =
            DependencyProperty.Register(nameof(CrosshairAzimuth), typeof(double), typeof(SkyDomeView),
                new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty CrosshairAltitudeProperty =
            DependencyProperty.Register(nameof(CrosshairAltitude), typeof(double), typeof(SkyDomeView),
                new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty SelectedIndexProperty =
            DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(SkyDomeView),
                new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty RedrawTriggerProperty =
            DependencyProperty.Register(nameof(RedrawTrigger), typeof(int), typeof(SkyDomeView),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty PointPickedCommandProperty =
            DependencyProperty.Register(nameof(PointPickedCommand), typeof(ICommand), typeof(SkyDomeView),
                new PropertyMetadata(null));

        public HorizonModel Horizon { get => (HorizonModel)GetValue(HorizonProperty); set => SetValue(HorizonProperty, value); }
        public double MountAzimuth { get => (double)GetValue(MountAzimuthProperty); set => SetValue(MountAzimuthProperty, value); }
        public double MountAltitude { get => (double)GetValue(MountAltitudeProperty); set => SetValue(MountAltitudeProperty, value); }
        public double CrosshairAzimuth { get => (double)GetValue(CrosshairAzimuthProperty); set => SetValue(CrosshairAzimuthProperty, value); }
        public double CrosshairAltitude { get => (double)GetValue(CrosshairAltitudeProperty); set => SetValue(CrosshairAltitudeProperty, value); }
        public int SelectedIndex { get => (int)GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
        public int RedrawTrigger { get => (int)GetValue(RedrawTriggerProperty); set => SetValue(RedrawTriggerProperty, value); }
        public ICommand PointPickedCommand { get => (ICommand)GetValue(PointPickedCommandProperty); set => SetValue(PointPickedCommandProperty, value); }

        private static readonly Brush SkyBrush = Frozen(Color.FromRgb(0x0E, 0x16, 0x26));
        private static readonly Brush TerrainBrush = Frozen(Color.FromRgb(0x3A, 0x2A, 0x18));
        private static readonly Pen HorizonPen = FrozenPen(Color.FromRgb(0xC8, 0x79, 0x2E), 2.0);
        private static readonly Pen GridPen = FrozenPen(Color.FromRgb(0x33, 0x41, 0x52), 1.0);
        private static readonly Pen RimPen = FrozenPen(Color.FromRgb(0x8A, 0x5A, 0x2E), 1.5);
        private static readonly Brush GridLabelBrush = Frozen(Color.FromRgb(0x9A, 0xA4, 0xB2));
        private static readonly Brush CardinalBrush = Frozen(Color.FromRgb(0xE0, 0xA4, 0x00));
        private static readonly Brush MountBrush = Frozen(Color.FromRgb(0x3F, 0xC8, 0xE0));
        private static readonly Pen MountPen = FrozenPen(Color.FromRgb(0x3F, 0xC8, 0xE0), 1.5);
        private static readonly Brush PointBrush = Frozen(Color.FromRgb(0xC8, 0xCD, 0xD5));
        private static readonly Brush UnsavedBrush = Frozen(Color.FromRgb(0xE2, 0x4B, 0x4A));
        private static readonly Pen SelectedPen = FrozenPen(Color.FromRgb(0xED, 0xA1, 0x00), 2.0);
        private static readonly Pen CrosshairPen = FrozenPen(Color.FromRgb(0xFF, 0xFF, 0xFF), 1.0);

        private static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
        private static Pen FrozenPen(Color c, double w) { var p = new Pen(Frozen(c), w); p.Freeze(); return p; }

        public SkyDomeView() {
            Cursor = Cursors.Cross;
            Focusable = true;
        }

        private Point Center => new Point(ActualWidth / 2.0, ActualHeight / 2.0);
        private double Radius => Math.Min(ActualWidth, ActualHeight) / 2.0 - 18;

        private static Point Polar(Point center, double radius, double alt, double az) {
            if (alt < 0) alt = 0; if (alt > 90) alt = 90;
            double r = radius * (90.0 - alt) / 90.0;
            double a = az * Math.PI / 180.0;
            return new Point(center.X + r * Math.Sin(a), center.Y - r * Math.Cos(a));
        }

        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e) {
            base.OnPreviewMouseLeftButtonDown(e);
            double radius = Radius;
            if (radius <= 0) return;
            var center = Center;
            var p = e.GetPosition(this);
            double dx = p.X - center.X, dy = p.Y - center.Y;
            double r = Math.Sqrt(dx * dx + dy * dy);
            if (r > radius) return;
            double alt = 90.0 - (r / radius) * 90.0;
            double az = Math.Atan2(dx, -dy) * 180.0 / Math.PI;
            az = ((az % 360) + 360) % 360;
            alt = Math.Max(0, Math.Min(90, alt));
            if (PointPickedCommand?.CanExecute(null) == true) {
                PointPickedCommand.Execute(new Point(az, alt));
                e.Handled = true;
            }
        }

        protected override void OnRender(DrawingContext dc) {
            double w = ActualWidth, h = ActualHeight;
            if (w <= 0 || h <= 0) return;
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));
            var center = Center;
            double radius = Radius;
            if (radius <= 0) return;

            dc.DrawEllipse(SkyBrush, null, center, radius, radius);

            var horizon = Horizon;
            if (horizon != null && horizon.Points.Count > 0) {
                var fig = new PathFigure { IsClosed = true, StartPoint = Polar(center, radius, horizon.GetAltitude(0), 0) };
                for (int az = 1; az <= 360; az++)
                    fig.Segments.Add(new LineSegment(Polar(center, radius, horizon.GetAltitude(az % 360), az), true));
                var horizonGeo = new PathGeometry(); horizonGeo.Figures.Add(fig);

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
            for (int az = 0; az < 360; az += 30)
                dc.DrawLine(GridPen, center, Polar(center, radius, 0, az));

            DrawCardinal(dc, "N", center, radius, 0);
            DrawCardinal(dc, "E", center, radius, 90);
            DrawCardinal(dc, "S", center, radius, 180);
            DrawCardinal(dc, "W", center, radius, 270);

            if (horizon != null) {
                for (int i = 0; i < horizon.Points.Count; i++) {
                    var pt = horizon.Points[i];
                    var c = Polar(center, radius, pt.Altitude, pt.Azimuth);
                    dc.DrawEllipse(pt.Unsaved ? UnsavedBrush : PointBrush, null, c, 3, 3);
                    if (i == SelectedIndex) dc.DrawEllipse(null, SelectedPen, c, 6, 6);
                }
            }

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

            if (!double.IsNaN(CrosshairAzimuth) && !double.IsNaN(CrosshairAltitude)) {
                var c = Polar(center, radius, CrosshairAltitude, CrosshairAzimuth);
                dc.DrawLine(CrosshairPen, new Point(c.X - 7, c.Y), new Point(c.X + 7, c.Y));
                dc.DrawLine(CrosshairPen, new Point(c.X, c.Y - 7), new Point(c.X, c.Y + 7));
            }
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
