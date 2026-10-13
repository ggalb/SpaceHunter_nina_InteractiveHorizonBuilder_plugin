// Copyright © 2026 Space Hunter - Georg G Albrecht
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

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
        public static readonly DependencyProperty SunAzimuthProperty =
            DependencyProperty.Register(nameof(SunAzimuth), typeof(double), typeof(SkyDomeView),
                new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty SunAltitudeProperty =
            DependencyProperty.Register(nameof(SunAltitude), typeof(double), typeof(SkyDomeView),
                new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty SunPathProperty =
            DependencyProperty.Register(nameof(SunPath), typeof(PointCollection), typeof(SkyDomeView),
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

        // Colours: Classic (own) or N.I.N.A.'s colour schema, rebuilt per render (see ChartPalette).
        public static readonly DependencyProperty FollowThemeProperty =
            DependencyProperty.Register(nameof(FollowTheme), typeof(bool), typeof(SkyDomeView),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
        // Bound to {DynamicResource PrimaryBrush} / BackgroundBrush: only there to repaint when
        // N.I.N.A. switches colour schema (brush replaced or its colour changed).
        public static readonly DependencyProperty ThemeProbeProperty =
            DependencyProperty.Register(nameof(ThemeProbe), typeof(Brush), typeof(SkyDomeView),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty ThemeProbe2Property =
            DependencyProperty.Register(nameof(ThemeProbe2), typeof(Brush), typeof(SkyDomeView),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public bool FollowTheme { get => (bool)GetValue(FollowThemeProperty); set => SetValue(FollowThemeProperty, value); }
        public Brush ThemeProbe { get => (Brush)GetValue(ThemeProbeProperty); set => SetValue(ThemeProbeProperty, value); }
        public Brush ThemeProbe2 { get => (Brush)GetValue(ThemeProbe2Property); set => SetValue(ThemeProbe2Property, value); }

        private Brush SkyBrush;
        private Brush TerrainBrush;
        private Pen HorizonPen;
        private Pen GridPen;
        private Pen RimPen;
        private Brush GridLabelBrush;
        private Brush CardinalBrush;
        private Brush MountBrush;
        private Pen MountPen;
        private Brush PointBrush;
        private Brush UnsavedBrush;
        private Pen SelectedPen;
        private Pen CrosshairPen;
        private Brush SunBrush;
        private Pen SunPathPen;
        private Pen Ring15Pen;
        private Pen Ring30Pen;

        private void ApplyPalette() {
            var p = FollowTheme ? ChartPalette.FromTheme(this) : ChartPalette.Classic;
            SkyBrush = ChartPalette.Brush(p.Sky);
            TerrainBrush = ChartPalette.Brush(p.Terrain);
            HorizonPen = ChartPalette.Pen(p.Horizon, 2.0);
            GridPen = ChartPalette.Pen(p.DomeGrid, 1.0);
            RimPen = ChartPalette.Pen(p.Rim, 1.5);
            GridLabelBrush = ChartPalette.Brush(p.GridLabel);
            CardinalBrush = ChartPalette.Brush(p.Cardinal);
            MountBrush = ChartPalette.Brush(p.Mount);
            MountPen = ChartPalette.Pen(p.Mount, 1.5);
            PointBrush = ChartPalette.Brush(p.Point);
            UnsavedBrush = ChartPalette.Brush(p.Unsaved);
            SelectedPen = ChartPalette.Pen(p.Selected, 2.0);
            CrosshairPen = ChartPalette.Pen(p.Crosshair, 1.0);
            SunBrush = ChartPalette.Brush(p.Sun);
            SunPathPen = ChartPalette.DashPen(p.SunPath, 1.0);
            Ring15Pen = ChartPalette.DashPen(p.Ring15, 1.5);
            Ring30Pen = ChartPalette.DashPen(p.Ring30, 1.5);
        }

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

            int hit = -1;
            var horizon = Horizon;
            if (horizon != null) {
                double best = 8.0 * 8.0;
                for (int i = 0; i < horizon.Points.Count; i++) {
                    var pt = horizon.Points[i];
                    var sp = Polar(center, radius, pt.Altitude, pt.Azimuth);
                    double pdx = sp.X - p.X, pdy = sp.Y - p.Y;
                    double d2 = pdx * pdx + pdy * pdy;
                    if (d2 <= best) { best = d2; hit = i; }
                }
            }

            var arg = new PickResult(az, alt, hit);
            if (PointPickedCommand != null && PointPickedCommand.CanExecute(arg)) {
                PointPickedCommand.Execute(arg);
                e.Handled = true;
            }
        }

        protected override void OnRender(DrawingContext dc) {
            ApplyPalette();
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

            if (SunPath != null && SunPath.Count > 1) {
                var fig = new PathFigure { StartPoint = Polar(center, radius, SunPath[0].Y, SunPath[0].X) };
                for (int i = 1; i < SunPath.Count; i++)
                    fig.Segments.Add(new LineSegment(Polar(center, radius, SunPath[i].Y, SunPath[i].X), true));
                var g = new PathGeometry(); g.Figures.Add(fig);
                dc.DrawGeometry(null, SunPathPen, g);
            }

            if (!double.IsNaN(SunAzimuth) && !double.IsNaN(SunAltitude) && SunAltitude >= 0) {
                DrawSphericalRing(dc, center, radius, SunAltitude, SunAzimuth, 30, Ring30Pen);
                DrawSphericalRing(dc, center, radius, SunAltitude, SunAzimuth, 15, Ring15Pen);
                dc.DrawEllipse(SunBrush, null, Polar(center, radius, SunAltitude, SunAzimuth), 6, 6);
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

        private void DrawSphericalRing(DrawingContext dc, Point center, double radius, double altC, double azC, double theta, Pen pen) {
            double d2r = Math.PI / 180.0;
            double aC = altC * d2r, thr = theta * d2r;
            PathFigure fig = null;
            for (int b = 0; b <= 360; b += 10) {
                double br = b * d2r;
                double sinAlt = Math.Sin(aC) * Math.Cos(thr) + Math.Cos(aC) * Math.Sin(thr) * Math.Cos(br);
                sinAlt = Math.Max(-1, Math.Min(1, sinAlt));
                double alt = Math.Asin(sinAlt);
                double az = azC + Math.Atan2(Math.Sin(br) * Math.Sin(thr) * Math.Cos(aC),
                                             Math.Cos(thr) - Math.Sin(aC) * sinAlt) / d2r;
                var p = Polar(center, radius, Math.Max(0, alt / d2r), az);
                if (fig == null) fig = new PathFigure { StartPoint = p };
                else fig.Segments.Add(new LineSegment(p, true));
            }
            if (fig != null) {
                fig.IsClosed = true;
                var geo = new PathGeometry(); geo.Figures.Add(fig);
                dc.DrawGeometry(null, pen, geo);
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
