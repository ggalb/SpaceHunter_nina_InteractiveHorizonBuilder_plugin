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
    /// Strip view: azimuth (x) by altitude (y) - the single-valued horizon as N.I.N.A. reads it.
    /// Draws the editable model (fill + line + point dots), the live mount, a crosshair, and the
    /// selected point; clicking reports an az/alt back via PointPickedCommand. Mouse wheel zooms
    /// around the pointer; left-drag pans; right-click resets to the full 0-360 / 0-90 view.
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

        // Colours: Classic (own) or N.I.N.A.'s colour schema, rebuilt per render (see ChartPalette).
        public static readonly DependencyProperty FollowThemeProperty =
            DependencyProperty.Register(nameof(FollowTheme), typeof(bool), typeof(StripView),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
        // Bound to {DynamicResource PrimaryBrush} / BackgroundBrush: only there to repaint when
        // N.I.N.A. switches colour schema (brush replaced or its colour changed).
        public static readonly DependencyProperty ThemeProbeProperty =
            DependencyProperty.Register(nameof(ThemeProbe), typeof(Brush), typeof(StripView),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty ThemeProbe2Property =
            DependencyProperty.Register(nameof(ThemeProbe2), typeof(Brush), typeof(StripView),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public bool FollowTheme { get => (bool)GetValue(FollowThemeProperty); set => SetValue(FollowThemeProperty, value); }
        public Brush ThemeProbe { get => (Brush)GetValue(ThemeProbeProperty); set => SetValue(ThemeProbeProperty, value); }
        public Brush ThemeProbe2 { get => (Brush)GetValue(ThemeProbe2Property); set => SetValue(ThemeProbe2Property, value); }

        private Brush SkyBrush;
        private Brush TerrainBrush;
        private Pen HorizonPen;
        private Pen GridPen;
        private Pen AxisPen;
        private Brush GridLabelBrush;
        private Brush CardinalBrush;
        private Pen MountPen;
        private Brush MountBrush;
        private Brush PointBrush;
        private Brush UnsavedBrush;
        private Pen SelectedPen;
        private Pen CrosshairPen;
        private Brush SunBrush;
        private Pen SunPen;
        private Pen Ring15Pen;
        private Pen Ring30Pen;

        private void ApplyPalette() {
            var p = FollowTheme ? ChartPalette.FromTheme(this) : ChartPalette.Classic;
            SkyBrush = ChartPalette.Brush(p.Sky);
            TerrainBrush = ChartPalette.Brush(p.Terrain);
            HorizonPen = ChartPalette.Pen(p.Horizon, 2.0);
            GridPen = ChartPalette.Pen(p.StripGrid, 1.0);
            AxisPen = ChartPalette.Pen(p.Rim, 1.5);
            GridLabelBrush = ChartPalette.Brush(p.GridLabel);
            CardinalBrush = ChartPalette.Brush(p.Cardinal);
            MountPen = ChartPalette.Pen(p.Mount, 1.0);
            MountBrush = ChartPalette.Brush(p.Mount);
            PointBrush = ChartPalette.Brush(p.Point);
            UnsavedBrush = ChartPalette.Brush(p.Unsaved);
            SelectedPen = ChartPalette.Pen(p.Selected, 2.0);
            CrosshairPen = ChartPalette.Pen(p.Crosshair, 1.0);
            SunBrush = ChartPalette.Brush(p.Sun);
            SunPen = ChartPalette.DashPen(p.SunPath, 1.0);
            Ring15Pen = ChartPalette.DashPen(p.Ring15, 1.5);
            Ring30Pen = ChartPalette.DashPen(p.Ring30, 1.5);
        }

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
        private const double DragThresholdPixels = 4.0;

        // Left-button press: a click (no real movement) picks/places on release; a drag pans the view.
        private bool leftDown, panning;
        private Point downPoint, lastPanPoint;

        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e) {
            base.OnPreviewMouseLeftButtonDown(e);
            if (PlotW <= 0 || PlotH <= 0) return;
            leftDown = true;
            panning = false;
            downPoint = lastPanPoint = e.GetPosition(this);
            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnPreviewMouseMove(MouseEventArgs e) {
            base.OnPreviewMouseMove(e);
            if (!leftDown) return;
            var p = e.GetPosition(this);
            if (!panning) {
                if ((p - downPoint).Length < DragThresholdPixels) return;
                panning = true;
                Cursor = Cursors.SizeAll;
            }
            double dAz = -(p.X - lastPanPoint.X) / PlotW * (viewAzHi - viewAzLo);
            double dAlt = (p.Y - lastPanPoint.Y) / PlotH * (viewAltHi - viewAltLo);
            PanAxis(ref viewAzLo, ref viewAzHi, dAz, 0, 360);
            PanAxis(ref viewAltLo, ref viewAltHi, dAlt, 0, 90);
            lastPanPoint = p;
            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e) {
            base.OnPreviewMouseLeftButtonUp(e);
            if (!leftDown) return;
            bool wasPanning = panning;
            leftDown = panning = false;
            Cursor = Cursors.Cross;
            ReleaseMouseCapture();
            e.Handled = true;
            if (!wasPanning) Pick(downPoint);
        }

        protected override void OnLostMouseCapture(MouseEventArgs e) {
            base.OnLostMouseCapture(e);
            leftDown = panning = false;
            Cursor = Cursors.Cross;
        }

        private static void PanAxis(ref double lo, ref double hi, double delta, double boundLo, double boundHi) {
            double span = hi - lo;
            lo = Math.Max(boundLo, Math.Min(boundHi - span, lo + delta));
            hi = lo + span;
        }

        private void Pick(Point p) {
            if (PlotW <= 0 || PlotH <= 0) return;
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
            ApplyPalette();
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
