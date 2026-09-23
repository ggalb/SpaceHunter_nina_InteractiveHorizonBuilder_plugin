using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace InteractiveHorizonBuilder {

    /// <summary>
    /// Strip view: azimuth (x, 0-360, N-E-S-W-N) by altitude (y, 0-90) - the single-valued horizon
    /// as N.I.N.A. reads it. Draws the editable model (fill + line + point dots), the live mount, a
    /// crosshair (edit cursor), and the selected point; clicking reports an az/alt back via
    /// PointPickedCommand. Companion to <see cref="SkyDomeView"/>.
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

        private static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
        private static Pen FrozenPen(Color c, double w) { var p = new Pen(Frozen(c), w); p.Freeze(); return p; }

        private double left = 28, right = 8, top = 6, bottom = 20;

        private double PlotW => ActualWidth - left - right;
        private double PlotH => ActualHeight - top - bottom;
        private double X(double az) => left + az / 360.0 * PlotW;
        private double Y(double alt) => top + (90.0 - Math.Max(0, Math.Min(90, alt))) / 90.0 * PlotH;

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) {
            base.OnMouseLeftButtonDown(e);
            if (PlotW <= 0 || PlotH <= 0) return;
            var p = e.GetPosition(this);
            double az = ((p.X - left) / PlotW) * 360.0;
            double alt = 90.0 - ((p.Y - top) / PlotH) * 90.0;
            az = ((az % 360) + 360) % 360;
            alt = Math.Max(0, Math.Min(90, alt));
            if (PointPickedCommand?.CanExecute(null) == true) {
                PointPickedCommand.Execute(new Point(az, alt));
            }
        }

        protected override void OnRender(DrawingContext dc) {
            double w = ActualWidth, h = ActualHeight;
            if (w <= 0 || h <= 0 || PlotW <= 0 || PlotH <= 0) return;

            dc.DrawRectangle(SkyBrush, null, new Rect(left, top, PlotW, PlotH));

            for (int alt = 0; alt <= 90; alt += 15) {
                double y = Y(alt);
                dc.DrawLine(GridPen, new Point(left, y), new Point(left + PlotW, y));
                DrawText(dc, alt.ToString(), new Point(2, y - 7), GridLabelBrush, 10);
            }
            for (int az = 0; az <= 360; az += 45) {
                double x = X(az);
                dc.DrawLine(GridPen, new Point(x, top), new Point(x, top + PlotH));
            }

            var horizon = Horizon;
            if (horizon != null && horizon.Points.Count > 0) {
                var fig = new PathFigure { IsClosed = true, StartPoint = new Point(X(0), Y(0)) };
                for (int az = 0; az <= 360; az++)
                    fig.Segments.Add(new LineSegment(new Point(X(az), Y(horizon.GetAltitude(az % 360))), true));
                fig.Segments.Add(new LineSegment(new Point(X(360), Y(0)), true));
                var geo = new PathGeometry(); geo.Figures.Add(fig);
                dc.DrawGeometry(TerrainBrush, null, geo);

                var line = new PathFigure { StartPoint = new Point(X(0), Y(horizon.GetAltitude(0))) };
                for (int az = 1; az <= 360; az++)
                    line.Segments.Add(new LineSegment(new Point(X(az), Y(horizon.GetAltitude(az % 360))), true));
                var lineGeo = new PathGeometry(); lineGeo.Figures.Add(line);
                dc.DrawGeometry(null, HorizonPen, lineGeo);

                for (int i = 0; i < horizon.Points.Count; i++) {
                    var pt = horizon.Points[i];
                    var c = new Point(X(pt.Azimuth), Y(pt.Altitude));
                    dc.DrawEllipse(pt.Unsaved ? UnsavedBrush : PointBrush, null, c, 3, 3);
                    if (i == SelectedIndex) dc.DrawEllipse(null, SelectedPen, c, 6, 6);
                }
            }

            dc.DrawRectangle(null, AxisPen, new Rect(left, top, PlotW, PlotH));

            DrawText(dc, "N", new Point(X(0) - 4, top + PlotH + 4), CardinalBrush, 11, true);
            DrawText(dc, "E", new Point(X(90) - 4, top + PlotH + 4), CardinalBrush, 11, true);
            DrawText(dc, "S", new Point(X(180) - 4, top + PlotH + 4), CardinalBrush, 11, true);
            DrawText(dc, "W", new Point(X(270) - 4, top + PlotH + 4), CardinalBrush, 11, true);
            DrawText(dc, "N", new Point(X(360) - 4, top + PlotH + 4), CardinalBrush, 11, true);

            if (!double.IsNaN(MountAzimuth) && !double.IsNaN(MountAltitude)) {
                double x = X(((MountAzimuth % 360) + 360) % 360);
                dc.DrawLine(MountPen, new Point(x, top), new Point(x, top + PlotH));
                dc.DrawEllipse(MountBrush, null, new Point(x, Y(MountAltitude)), 3.5, 3.5);
            }

            if (!double.IsNaN(CrosshairAzimuth) && !double.IsNaN(CrosshairAltitude)) {
                var c = new Point(X(((CrosshairAzimuth % 360) + 360) % 360), Y(CrosshairAltitude));
                dc.DrawLine(CrosshairPen, new Point(c.X - 7, c.Y), new Point(c.X + 7, c.Y));
                dc.DrawLine(CrosshairPen, new Point(c.X, c.Y - 7), new Point(c.X, c.Y + 7));
            }
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
