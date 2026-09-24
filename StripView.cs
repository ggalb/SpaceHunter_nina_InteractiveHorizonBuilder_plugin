using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace InteractiveHorizonBuilder {

    /// <summary>
    /// Strip view: azimuth (x) by altitude (y) - the single-valued horizon as N.I.N.A. reads it.
    /// Draws the editable model (fill + line + point dots), the live mount, a crosshair, and the
    /// selected point; clicking reports an az/alt back via PointPickedCommand. Mouse wheel zooms
    /// around the pointer; right-click resets to the full 0-360 / 0-90 view.
    /// </summary>
    public class StripView : FrameworkElement {

        public static readonly DependencyProperty HorizonProperty =
            DependencyProperty.Register(nameof(Horizon), typeof(HorizonModel), typeof(StripView),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty MountAzimuthProperty =
            DependencyProperty.Register(nameof(MountAzimuth), typeof(double), typeof(StripView),
                new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty MountAltitudeProperty =
            DependencyProperty.Register(nameof(MountAltitude), typeof(double), typeof(StripView),
                new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty CrosshairAzimuthProperty =
            DependencyProperty.Register(nameof(CrosshairAzimuth), typeof(double), typeof(StripView),
                new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty CrosshairAltitudeProperty =
            DependencyProperty.Register(nameof(CrosshairAltitude), typeof(double), typeof(StripView),
                new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty SelectedIndexProperty =
            DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(StripView),
                new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty RedrawTriggerProperty =
            DependencyProperty.Register(nameof(RedrawTrigger), typeof(int), typeof(StripView),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty PointPickedCommandProperty =
            DependencyProperty.Register(nameof(PointPickedCommand), typeof(ICommand), typeof(StripView),
                new PropertyMetadata(null));
        public static readonly DependencyProperty SunAzimuthProperty =
            DependencyProperty.Register(nameof(SunAzimuth), typeof(double), typeof(StripView),
                new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty SunAltitudeProperty =
            DependencyProperty.Register(nameof(SunAltitude), typeof(double), typeof(StripView),
                new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SunPathProperty =
            DependencyProperty.Register(nameof(SunPath), typeof(PointCollection), typeof(StripView),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public double SunAzimuth { get => (double)GetValue(SunAzimuthProperty); set => SetValue(SunAzimuthProperty, value); }
        public double SunAltitude { get => (double)GetValue(SunAltitudeProperty); set => SetValue(SunAltitudeProperty, value); }
        public PointCollection SunPath { get => (PointCollection)GetValue(SunPathProperty); set => SetValue(SunPathProperty, value); }

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
        private static readonly Pen GridPen = FrozenPen(Color.FromRgb(0x2A, 0x36, 0x46), 1.0);
        private static readonly Pen AxisPen = FrozenPen(Color.FromRgb(0x8A, 0x5A, 0x2E), 1.5);
        private static readonly Brush GridLabelBrush = Frozen(Color.FromRgb(0x9A, 0xA4, 0xB2));
        private static readonly Brush CardinalBrush = Frozen(Color.FromRgb(0xE0, 0xA4, 0x00));
        private static readonly Pen MountPen = FrozenPen(Color.FromRgb(0x3F, 0xC8, 0xE0), 1.0);
        private static readonly Brush MountBrush = Frozen(Color.FromRgb(0x3F, 0xC8, 0xE0));
        private static readonly Brush PointBrush = Frozen(Color.FromRgb(0xC8, 0xCD, 0xD5));
        private static readonly Brush UnsavedBrush = Frozen(Color.FromRgb(0xE2, 0x4B, 0x4A));
        private static readonly Pen SelectedPen = FrozenPen(Color.FromRgb(0xED, 0xA1, 0x00), 2.0);
        private static readonly Pen CrosshairPen = FrozenPen(Color.FromRgb(0xFF, 0xFF, 0xFF), 1.0);
        private static readonly Brush SunBrush = Frozen(Color.FromRgb(0xFF, 0xD1, 0x00));
        private static readonly Pen SunPen = FrozenDashPen(Color.FromRgb(0xE0, 0xA4, 0x00), 1.0);
        private static readonly Pen Ring15Pen = FrozenDashPen(Color.FromRgb(0xE2, 0x4B, 0x4A), 1.5);
        private static readonly Pen Ring30Pen = FrozenDashPen(Color.FromRgb(0xED, 0xA1, 0x00), 1.5);

        private static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
        private static Pen FrozenPen(Color c, double w) { var p = new Pen(Frozen(c), w); p.Freeze(); return p; }
        private static Pen FrozenDashPen(Color c, double w) { var p = new Pen(Frozen(c), w) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) }; p.Freeze(); return p; }

        private double left = 28, right = 8, top = 6, bottom = 20;
        private double viewAzLo = 0, viewAzHi = 360, viewAltLo = 0, viewAltHi = 90;

        private double PlotW => ActualWidth - left - right;
        private double PlotH => ActualHeight - top - bottom;
        private double X(double az) => left + (az - viewAzLo) / (viewAzHi - viewAzLo) * PlotW;
        private double Y(double alt) => top + (viewAltHi - alt) / (viewAltHi - viewAltLo) * PlotH;

        public StripView() {
            Cursor = Cursors.Cross;
            Focusable = true;
        }

        private const double PickPixelRadius = 8.0;

        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e) {
            base.OnPreviewMouseLeftButtonDown(e);
            if (PlotW <= 0 || PlotH <= 0) return;
            var p = e.GetPosition(this);
            double az = viewAzLo + (p.X - left) / PlotW * (viewAzHi - viewAzLo);
            double alt = viewAltHi - (p.Y - top) / PlotH * (viewAltHi - viewAltLo);
            az = ((az % 360) + 360) % 360;
            alt = Math.Max(0, Math.Min(90, alt));

            int hit = -1;
            var horizon = Horizon;
            if (horizon != null) {
                double best = PickPixelRadius * PickPixelRadius;
                for (int i = 0; i < horizon.Points.Count; i++) {
                    var pt = horizon.Points[i];
                    if (pt.Azimuth < viewAzLo || pt.Azimuth > viewAzHi) continue;
                    double dx = X(pt.Azimuth) - p.X, dy = Y(pt.Altitude) - p.Y;
                    double d2 = dx * dx + dy * dy;
                    if (d2 <= best) { best = d2; hit = i; }
                }
            }

            var arg = new PickResult(az, alt, hit);
            if (PointPickedCommand != null && PointPickedCommand.CanExecute(arg)) {
                PointPickedCommand.Execute(arg);
                e.Handled = true;
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e) {
            base.OnMouseWheel(e);
            if (PlotW <= 0 || PlotH <= 0) return;
            var p = e.GetPosition(this);
            double azP = viewAzLo + (p.X - left) / PlotW * (viewAzHi - viewAzLo);
            double altP = viewAltHi - (p.Y - top) / PlotH * (viewAltHi - viewAltLo);
            double f = e.Delta > 0 ? 0.85 : 1.0 / 0.85;
            ZoomAxis(ref viewAzLo, ref viewAzHi, azP, f, 10, 360, 0, 360);
            ZoomAxis(ref viewAltLo, ref viewAltHi, altP, f, 5, 90, 0, 90);
            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e) {
            base.OnMouseRightButtonDown(e);
            viewAzLo = 0; viewAzHi = 360; viewAltLo = 0; viewAltHi = 90;
            InvalidateVisual();
            e.Handled = true;
        }

        private static void ZoomAxis(ref double lo, ref double hi, double center, double f,
                                     double minSpan, double maxSpan, double boundLo, double boundHi) {
            double span = (hi - lo) * f;
            span = Math.Max(minSpan, Math.Min(maxSpan, span));
            double frac = (hi > lo) ? (center - lo) / (hi - lo) : 0.5;
            lo = center - frac * span;
            hi = lo + span;
            if (lo < boundLo) { lo = boundLo; hi = lo + span; }
            if (hi > boundHi) { hi = boundHi; lo = hi - span; }
            if (lo < boundLo) lo = boundLo;
        }

        protected override void OnRender(DrawingContext dc) {
            double w = ActualWidth, h = ActualHeight;
            if (w <= 0 || h <= 0 || PlotW <= 0 || PlotH <= 0) return;

            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));
            var plotRect = new Rect(left, top, PlotW, PlotH);
            dc.DrawRectangle(SkyBrush, null, plotRect);

            for (int alt = 0; alt <= 90; alt += 15) {
                if (alt < viewAltLo - 0.001 || alt > viewAltHi + 0.001) continue;
                double y = Y(alt);
                dc.DrawLine(GridPen, new Point(left, y), new Point(left + PlotW, y));
                DrawText(dc, alt.ToString(), new Point(2, y - 7), GridLabelBrush, 10);
            }
            for (int az = 0; az <= 360; az += 45) {
                if (az < viewAzLo - 0.001 || az > viewAzHi + 0.001) continue;
                double x = X(az);
                dc.DrawLine(GridPen, new Point(x, top), new Point(x, top + PlotH));
            }

            dc.PushClip(new RectangleGeometry(plotRect));
            var horizon = Horizon;
            if (horizon != null && horizon.Points.Count > 0) {
                int samples = Math.Max(2, (int)PlotW);
                var fig = new PathFigure { IsClosed = true, StartPoint = new Point(left, top + PlotH) };
                for (int i = 0; i <= samples; i++) {
                    double az = viewAzLo + (viewAzHi - viewAzLo) * i / samples;
                    fig.Segments.Add(new LineSegment(new Point(X(az), Y(horizon.GetAltitude(((az % 360) + 360) % 360))), true));
                }
                fig.Segments.Add(new LineSegment(new Point(left + PlotW, top + PlotH), true));
                var geo = new PathGeometry(); geo.Figures.Add(fig);
                dc.DrawGeometry(TerrainBrush, null, geo);

                var line = new PathFigure { StartPoint = new Point(left, Y(horizon.GetAltitude(((viewAzLo % 360) + 360) % 360))) };
                for (int i = 1; i <= samples; i++) {
                    double az = viewAzLo + (viewAzHi - viewAzLo) * i / samples;
                    line.Segments.Add(new LineSegment(new Point(X(az), Y(horizon.GetAltitude(((az % 360) + 360) % 360))), true));
                }
                var lineGeo = new PathGeometry(); lineGeo.Figures.Add(line);
                dc.DrawGeometry(null, HorizonPen, lineGeo);

                for (int i = 0; i < horizon.Points.Count; i++) {
                    var pt = horizon.Points[i];
                    if (pt.Azimuth < viewAzLo - 2 || pt.Azimuth > viewAzHi + 2) continue;
                    var c = new Point(X(pt.Azimuth), Y(pt.Altitude));
                    dc.DrawEllipse(pt.Unsaved ? UnsavedBrush : PointBrush, null, c, 3, 3);
                    if (i == SelectedIndex) dc.DrawEllipse(null, SelectedPen, c, 6, 6);
                }
            }

            if (!double.IsNaN(MountAzimuth) && !double.IsNaN(MountAltitude)) {
                double maz = ((MountAzimuth % 360) + 360) % 360;
                if (maz >= viewAzLo && maz <= viewAzHi) {
                    double x = X(maz);
                    dc.DrawLine(MountPen, new Point(x, top), new Point(x, top + PlotH));
                    dc.DrawEllipse(MountBrush, null, new Point(x, Y(MountAltitude)), 3.5, 3.5);
                }
            }

            if (SunPath != null && SunPath.Count > 1) {
                double prevAz = double.NaN;
                Point prev = default;
                foreach (var sp in SunPath) {
                    double az = ((sp.X % 360) + 360) % 360;
                    var cur = new Point(X(az), Y(sp.Y));
                    if (!double.IsNaN(prevAz) && Math.Abs(az - prevAz) <= 180 &&
                        az >= viewAzLo && az <= viewAzHi && prevAz >= viewAzLo && prevAz <= viewAzHi) {
                        dc.DrawLine(SunPen, prev, cur);
                    }
                    prevAz = az; prev = cur;
                }
            }

            if (!double.IsNaN(SunAzimuth) && !double.IsNaN(SunAltitude) && SunAltitude >= 0) {
                DrawSphericalRing(dc, SunAltitude, SunAzimuth, 30, Ring30Pen);
                DrawSphericalRing(dc, SunAltitude, SunAzimuth, 15, Ring15Pen);
                double saz = ((SunAzimuth % 360) + 360) % 360;
                if (saz >= viewAzLo && saz <= viewAzHi) {
                    double x = X(saz);
                    dc.DrawLine(SunPen, new Point(x, top), new Point(x, top + PlotH));
                    dc.DrawEllipse(SunBrush, null, new Point(x, Y(SunAltitude)), 4, 4);
                }
            }

            if (!double.IsNaN(CrosshairAzimuth) && !double.IsNaN(CrosshairAltitude)) {
                double caz = ((CrosshairAzimuth % 360) + 360) % 360;
                if (caz >= viewAzLo && caz <= viewAzHi) {
                    var c = new Point(X(caz), Y(CrosshairAltitude));
                    dc.DrawLine(CrosshairPen, new Point(c.X - 7, c.Y), new Point(c.X + 7, c.Y));
                    dc.DrawLine(CrosshairPen, new Point(c.X, c.Y - 7), new Point(c.X, c.Y + 7));
                }
            }
            dc.Pop();

            dc.DrawRectangle(null, AxisPen, plotRect);

            DrawCardinal(dc, "N", 0);
            DrawCardinal(dc, "E", 90);
            DrawCardinal(dc, "S", 180);
            DrawCardinal(dc, "W", 270);
            DrawCardinal(dc, "N", 360);
        }

        private void DrawSphericalRing(DrawingContext dc, double altC, double azC, double theta, Pen pen) {
            double d2r = Math.PI / 180.0;
            double aC = altC * d2r, thr = theta * d2r;
            double prevAz = double.NaN;
            Point prev = default;
            for (int b = 0; b <= 360; b += 6) {
                double br = b * d2r;
                double sinAlt = Math.Sin(aC) * Math.Cos(thr) + Math.Cos(aC) * Math.Sin(thr) * Math.Cos(br);
                sinAlt = Math.Max(-1, Math.Min(1, sinAlt));
                double alt = Math.Asin(sinAlt) / d2r;
                double az = azC + Math.Atan2(Math.Sin(br) * Math.Sin(thr) * Math.Cos(aC),
                                             Math.Cos(thr) - Math.Sin(aC) * sinAlt) / d2r;
                az = ((az % 360) + 360) % 360;
                var cur = new Point(X(az), Y(alt));
                if (!double.IsNaN(prevAz) && Math.Abs(az - prevAz) <= 180 &&
                    az >= viewAzLo && az <= viewAzHi && prevAz >= viewAzLo && prevAz <= viewAzHi) {
                    dc.DrawLine(pen, prev, cur);
                }
                prevAz = az; prev = cur;
            }
        }

        private void DrawCardinal(DrawingContext dc, string label, double az) {
            if (az < viewAzLo - 0.001 || az > viewAzHi + 0.001) return;
            DrawText(dc, label, new Point(X(az) - 4, top + PlotH + 4), CardinalBrush, 11, true);
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
