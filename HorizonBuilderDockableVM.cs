using CommunityToolkit.Mvvm.Input;
using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Equipment.Equipment.MyTelescope;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace InteractiveHorizonBuilder {

    /// <summary>
    /// Dockable panel view-model.
    /// Phases 1-2: live mount readout + Sky Dome / Strip drawn from an editable horizon model.
    /// Phase 3: mount control (slew / stop / tracking).
    /// Phase 4: editing loop - click to select or place a point, nudge, confirm, delete, undo, and
    /// save the .hrz back into N.I.N.A. All equipment access is through N.I.N.A. mediators.
    /// </summary>
    [Export(typeof(IDockableVM))]
    public class HorizonBuilderDockableVM : DockableVM, ITelescopeConsumer {
        private readonly ITelescopeMediator telescopeMediator;
        private readonly IFocuserMediator focuserMediator;
        private readonly IRotatorMediator rotatorMediator;
        private readonly IFlatDeviceMediator flatDeviceMediator;
        private readonly ISwitchMediator switchMediator;
        private CancellationTokenSource slewCts;
        private readonly Stack<HorizonModel> undoStack = new Stack<HorizonModel>();
        private static readonly IProgress<ApplicationStatus> NoProgress = new Progress<ApplicationStatus>();

        [ImportingConstructor]
        public HorizonBuilderDockableVM(
            IProfileService profileService,
            ITelescopeMediator telescopeMediator,
            IFocuserMediator focuserMediator,
            IRotatorMediator rotatorMediator,
            IFlatDeviceMediator flatDeviceMediator,
            ISwitchMediator switchMediator)
            : base(profileService) {
            this.telescopeMediator = telescopeMediator;
            this.focuserMediator = focuserMediator;
            this.rotatorMediator = rotatorMediator;
            this.flatDeviceMediator = flatDeviceMediator;
            this.switchMediator = switchMediator;

            Title = "Interactive Horizon Builder";

            Model = HorizonModel.Load(profileService.ActiveProfile.AstrometrySettings.HorizonFilePath);
            HorizonLoaded = Model.Points.Count > 0;

            SlewCommand = new AsyncRelayCommand(SlewToTargetAsync);
            StopCommand = new RelayCommand(Stop);
            TrackingOnCommand = new RelayCommand(() => SetTracking(true));
            TrackingOffCommand = new RelayCommand(() => SetTracking(false));
            TargetToMountCommand = new RelayCommand(() => { TargetAzimuth = MountAzimuth; TargetAltitude = MountAltitude; });

            PointPickedCommand = new RelayCommand<Point>(OnPointPicked);
            PlaceConfirmCommand = new RelayCommand(PlaceOrConfirm);
            DeleteCommand = new RelayCommand(DeleteSelected);
            UndoCommand = new RelayCommand(Undo);
            SaveCommand = new RelayCommand(Save);
            AzMinusCommand = new RelayCommand(() => NudgeTarget(-NudgeStep, 0));
            AzPlusCommand = new RelayCommand(() => NudgeTarget(NudgeStep, 0));
            AltMinusCommand = new RelayCommand(() => NudgeTarget(0, -NudgeStep));
            AltPlusCommand = new RelayCommand(() => NudgeTarget(0, NudgeStep));
            TimeMinusCommand = new RelayCommand(() => NudgeTime(-TimeStepMinutes));
            TimePlusCommand = new RelayCommand(() => NudgeTime(TimeStepMinutes));

            MoveFocuserCommand = new AsyncRelayCommand(MoveFocuserAsync);
            RotateToCommand = new AsyncRelayCommand<string>(RotateToAsync);
            FlatOpenCommand = new AsyncRelayCommand(() => FlatCoverAsync(true));
            FlatCloseCommand = new AsyncRelayCommand(() => FlatCoverAsync(false));
            DustCoverOnCommand = new AsyncRelayCommand(() => DustCoverAsync(true));
            DustCoverOffCommand = new AsyncRelayCommand(() => DustCoverAsync(false));

            telescopeMediator.RegisterConsumer(this);
        }

        // ---- Horizon model + editing state -------------------------------------

        private HorizonModel model;
        public HorizonModel Model {
            get => model;
            set { model = value; RaisePropertyChanged(); }
        }

        private int editVersion;
        public int EditVersion {
            get => editVersion;
            set { editVersion = value; RaisePropertyChanged(); }
        }

        private int selectedIndex = -1;
        public int SelectedIndex {
            get => selectedIndex;
            set { selectedIndex = value; RaisePropertyChanged(); RaisePropertyChanged(nameof(HasSelection)); }
        }
        public bool HasSelection => SelectedIndex >= 0;

        private double nudgeStep = 1.0;
        public double NudgeStep {
            get => nudgeStep;
            set { nudgeStep = value; RaisePropertyChanged(); }
        }

        private double timeStepMinutes = 5.0;
        public double TimeStepMinutes {
            get => timeStepMinutes;
            set { timeStepMinutes = value; RaisePropertyChanged(); }
        }

        private string editStatus = "Loaded horizon";
        public string EditStatus {
            get => editStatus;
            set { editStatus = value; RaisePropertyChanged(); }
        }

        // ---- Live mount state --------------------------------------------------

        private bool mountConnected;
        public bool MountConnected { get => mountConnected; set { mountConnected = value; RaisePropertyChanged(); } }

        private double mountAltitude = double.NaN;
        public double MountAltitude { get => mountAltitude; set { mountAltitude = value; RaisePropertyChanged(); } }

        private double mountAzimuth = double.NaN;
        public double MountAzimuth { get => mountAzimuth; set { mountAzimuth = value; RaisePropertyChanged(); } }

        private bool horizonLoaded;
        public bool HorizonLoaded { get => horizonLoaded; set { horizonLoaded = value; RaisePropertyChanged(); } }

        private double horizonAltitudeAtMount;
        public double HorizonAltitudeAtMount { get => horizonAltitudeAtMount; set { horizonAltitudeAtMount = value; RaisePropertyChanged(); } }

        // ---- Target / crosshair + mount control --------------------------------

        private double targetAzimuth = 0;
        public double TargetAzimuth { get => targetAzimuth; set { targetAzimuth = value; RaisePropertyChanged(); } }

        private double targetAltitude = 30;
        public double TargetAltitude { get => targetAltitude; set { targetAltitude = value; RaisePropertyChanged(); } }

        private string mountStatus = "Idle";
        public string MountStatus { get => mountStatus; set { mountStatus = value; RaisePropertyChanged(); } }

        public ICommand SlewCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand TrackingOnCommand { get; }
        public ICommand TrackingOffCommand { get; }
        public ICommand TargetToMountCommand { get; }

        public ICommand PointPickedCommand { get; }
        public ICommand PlaceConfirmCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand AzMinusCommand { get; }
        public ICommand AzPlusCommand { get; }
        public ICommand AltMinusCommand { get; }
        public ICommand AltPlusCommand { get; }
        public ICommand TimeMinusCommand { get; }
        public ICommand TimePlusCommand { get; }

        public ICommand MoveFocuserCommand { get; }
        public ICommand RotateToCommand { get; }
        public ICommand FlatOpenCommand { get; }
        public ICommand FlatCloseCommand { get; }
        public ICommand DustCoverOnCommand { get; }
        public ICommand DustCoverOffCommand { get; }

        private const double SelectAzTolerance = 5.0;
        private const double SelectAltTolerance = 8.0;
        private const double LowAltitudeWarningDeg = 20.0;

        // ---- Editing -----------------------------------------------------------

        private void OnPointPicked(Point azAlt) {
            double az = azAlt.X, alt = azAlt.Y;
            int found = FindNearest(az, alt);
            if (found >= 0) {
                SelectedIndex = found;
                TargetAzimuth = Model.Points[found].Azimuth;
                TargetAltitude = Model.Points[found].Altitude;
                EditStatus = $"Selected point {found + 1}/{Model.Points.Count}";
            } else {
                SelectedIndex = -1;
                TargetAzimuth = az;
                TargetAltitude = alt;
                EditStatus = "Crosshair placed - Confirm to add a point here";
            }
        }

        private int FindNearest(double az, double alt) {
            int best = -1;
            double bestScore = double.MaxValue;
            for (int i = 0; i < Model.Points.Count; i++) {
                var p = Model.Points[i];
                double dAz = Math.Abs(((p.Azimuth - az + 540) % 360) - 180);
                double dAlt = Math.Abs(p.Altitude - alt);
                if (dAz <= SelectAzTolerance && dAlt <= SelectAltTolerance) {
                    double score = dAz + dAlt;
                    if (score < bestScore) { bestScore = score; best = i; }
                }
            }
            return best;
        }

        private void PlaceOrConfirm() {
            PushUndo();
            HorizonPoint edited;
            if (SelectedIndex >= 0 && SelectedIndex < Model.Points.Count) {
                edited = Model.Points[SelectedIndex];
                edited.Azimuth = ((TargetAzimuth % 360) + 360) % 360;
                edited.Altitude = TargetAltitude;
                edited.Unsaved = true;
                EditStatus = "Point updated (unsaved)";
            } else {
                edited = new HorizonPoint(((TargetAzimuth % 360) + 360) % 360, TargetAltitude, true);
                Model.Points.Add(edited);
                EditStatus = "Point added (unsaved)";
            }
            Model.Sort();
            SelectedIndex = Model.Points.IndexOf(edited);
            HorizonLoaded = Model.Points.Count > 0;
            Bump();
        }

        private void DeleteSelected() {
            if (SelectedIndex < 0 || SelectedIndex >= Model.Points.Count) {
                EditStatus = "No point selected";
                return;
            }
            PushUndo();
            Model.Points.RemoveAt(SelectedIndex);
            SelectedIndex = -1;
            EditStatus = "Point deleted (unsaved)";
            Bump();
        }

        private void Undo() {
            if (undoStack.Count == 0) {
                EditStatus = "Nothing to undo";
                return;
            }
            Model = undoStack.Pop();
            SelectedIndex = -1;
            HorizonLoaded = Model.Points.Count > 0;
            EditStatus = "Undo";
            Bump();
        }

        private void Save() {
            var path = profileService.ActiveProfile.AstrometrySettings.HorizonFilePath;
            if (string.IsNullOrWhiteSpace(path)) {
                EditStatus = "No horizon file set (Options -> General -> Horizon)";
                return;
            }
            try {
                Model.Save(path);
                var settings = profileService.ActiveProfile.AstrometrySettings;
                settings.Horizon = CustomHorizon.FromFilePath(path);
                settings.HorizonFilePath = path;
                EditStatus = $"Saved {Model.Points.Count} points to {System.IO.Path.GetFileName(path)}";
                Bump();
            } catch (Exception ex) {
                EditStatus = "Save error: " + ex.Message;
            }
        }

        private void NudgeTarget(double dAz, double dAlt) {
            TargetAzimuth = ((TargetAzimuth + dAz) % 360 + 360) % 360;
            TargetAltitude = Math.Max(0, Math.Min(90, TargetAltitude + dAlt));
        }

        /// <summary>
        /// Time nudge: move the crosshair along the sky track a celestial target at the current
        /// Az/Alt would follow. Holds the point's declination fixed, advances the hour angle by the
        /// sidereal amount for the time step, and converts back to Az/Alt at the site latitude.
        /// Positive minutes = later (the point moves west, as the sky rotates).
        /// </summary>
        private void NudgeTime(double deltaMinutes) {
            double lat = profileService.ActiveProfile.AstrometrySettings.Latitude;
            const double d2r = Math.PI / 180.0, r2d = 180.0 / Math.PI;
            double phi = lat * d2r, a = TargetAltitude * d2r, az = TargetAzimuth * d2r;

            double sinDec = Math.Sin(phi) * Math.Sin(a) + Math.Cos(phi) * Math.Cos(a) * Math.Cos(az);
            sinDec = Math.Max(-1, Math.Min(1, sinDec));
            double dec = Math.Asin(sinDec);
            double cosDec = Math.Cos(dec);
            if (Math.Abs(cosDec) < 1e-6 || Math.Abs(Math.Cos(phi)) < 1e-6) {
                EditStatus = "Time nudge not defined near the pole/zenith here";
                return;
            }

            double sinH = -Math.Sin(az) * Math.Cos(a) / cosDec;
            double cosH = (Math.Sin(a) - sinDec * Math.Sin(phi)) / (cosDec * Math.Cos(phi));
            double H = Math.Atan2(sinH, cosH);

            double dTheta = (15.041 / 60.0) * deltaMinutes * d2r; // sidereal deg/min -> rad
            double H2 = H + dTheta;

            double sinA2 = Math.Sin(phi) * sinDec + Math.Cos(phi) * cosDec * Math.Cos(H2);
            sinA2 = Math.Max(-1, Math.Min(1, sinA2));
            double alt2 = Math.Asin(sinA2);
            double cosAlt2 = Math.Cos(alt2);
            if (Math.Abs(cosAlt2) < 1e-6) { EditStatus = "Time nudge hit the zenith"; return; }

            double sinAz2 = -Math.Sin(H2) * cosDec / cosAlt2;
            double cosAz2 = (sinDec - Math.Sin(phi) * Math.Sin(alt2)) / (Math.Cos(phi) * cosAlt2);
            double az2 = Math.Atan2(sinAz2, cosAz2) * r2d;

            TargetAzimuth = ((az2 % 360) + 360) % 360;
            TargetAltitude = Math.Max(0, Math.Min(90, alt2 * r2d));
            EditStatus = $"Time {(deltaMinutes >= 0 ? "+" : "")}{deltaMinutes:F0} min  (Dec held {dec * r2d:F1}°)";
        }

        private void PushUndo() => undoStack.Push(Model.Clone());

        private void Bump() {
            EditVersion++;
            if (Model.Points.Count > 0 && !double.IsNaN(MountAzimuth)) {
                HorizonAltitudeAtMount = Model.GetAltitude(MountAzimuth);
            }
        }

        // ---- Devices: focuser / rotator / flat / dust-cover switch -------------

        private int focuserTargetPosition;
        public int FocuserTargetPosition { get => focuserTargetPosition; set { focuserTargetPosition = value; RaisePropertyChanged(); } }

        private string focuserState = "-";
        public string FocuserState { get => focuserState; set { focuserState = value; RaisePropertyChanged(); } }

        private string rotatorState = "-";
        public string RotatorState { get => rotatorState; set { rotatorState = value; RaisePropertyChanged(); } }

        private string flatState = "-";
        public string FlatState { get => flatState; set { flatState = value; RaisePropertyChanged(); } }

        private string dustCoverState = "-";
        public string DustCoverState { get => dustCoverState; set { dustCoverState = value; RaisePropertyChanged(); } }

        private string deviceStatus = "Idle";
        public string DeviceStatus { get => deviceStatus; set { deviceStatus = value; RaisePropertyChanged(); } }

        private void RefreshDevices() {
            try {
                var f = focuserMediator.GetInfo();
                FocuserState = f != null && f.Connected ? f.Position.ToString() : "not connected";
            } catch { FocuserState = "-"; }
            try {
                var r = rotatorMediator.GetInfo();
                RotatorState = r != null && r.Connected ? $"{r.MechanicalPosition:F1}°" : "not connected";
            } catch { RotatorState = "-"; }
            try {
                var fl = flatDeviceMediator.GetInfo();
                FlatState = fl != null && fl.Connected ? fl.CoverState.ToString() : "not connected";
            } catch { FlatState = "-"; }
            try {
                var sw = FindDustCover();
                DustCoverState = sw == null ? "not found"
                    : (Math.Abs(sw.Value - sw.Maximum) < Math.Abs(sw.Value - sw.Minimum) ? "On" : "Off");
            } catch { DustCoverState = "-"; }
        }

        private IWritableSwitch FindDustCover() {
            var info = switchMediator.GetInfo();
            if (info == null || !info.Connected || info.WritableSwitches == null) return null;
            return info.WritableSwitches.FirstOrDefault(s =>
                (s.Name ?? "").IndexOf("dust", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (s.Name ?? "").IndexOf("cover", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private async Task MoveFocuserAsync() {
            try {
                var f = focuserMediator.GetInfo();
                if (f == null || !f.Connected) { DeviceStatus = "Focuser not connected"; return; }
                DeviceStatus = $"Focuser -> {FocuserTargetPosition}...";
                await focuserMediator.MoveFocuser(FocuserTargetPosition, CancellationToken.None);
                DeviceStatus = "Focuser moved";
                RefreshDevices();
            } catch (Exception ex) { DeviceStatus = "Focuser error: " + ex.Message; }
        }

        private async Task RotateToAsync(string angleText) {
            try {
                if (!float.TryParse(angleText, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var angle)) return;
                var r = rotatorMediator.GetInfo();
                if (r == null || !r.Connected) { DeviceStatus = "Rotator not connected"; return; }
                DeviceStatus = $"Rotator -> {angle:F0}°...";
                await rotatorMediator.MoveMechanical(angle, CancellationToken.None);
                DeviceStatus = $"Rotator at {angle:F0}°";
                RefreshDevices();
            } catch (Exception ex) { DeviceStatus = "Rotator error: " + ex.Message; }
        }

        private async Task FlatCoverAsync(bool open) {
            try {
                var fl = flatDeviceMediator.GetInfo();
                if (fl == null || !fl.Connected) { DeviceStatus = "Flat panel not connected"; return; }
                DeviceStatus = open ? "Opening flat..." : "Closing flat...";
                if (open) await flatDeviceMediator.OpenCover(NoProgress, CancellationToken.None);
                else await flatDeviceMediator.CloseCover(NoProgress, CancellationToken.None);
                DeviceStatus = open ? "Flat opened" : "Flat closed";
                RefreshDevices();
            } catch (Exception ex) { DeviceStatus = "Flat error: " + ex.Message; }
        }

        private async Task DustCoverAsync(bool on) {
            try {
                var sw = FindDustCover();
                if (sw == null) { DeviceStatus = "Dust-cover switch not found"; return; }
                double value = on ? sw.Maximum : sw.Minimum;
                DeviceStatus = on ? "Dust cover on..." : "Dust cover off...";
                await switchMediator.SetSwitchValue(sw.Id, value, NoProgress, CancellationToken.None);
                DeviceStatus = on ? "Dust cover on" : "Dust cover off";
                RefreshDevices();
            } catch (Exception ex) { DeviceStatus = "Dust-cover error: " + ex.Message; }
        }

        // ---- Mount control (Phase 3) -------------------------------------------

        private async Task SlewToTargetAsync() {
            if (!MountConnected) { MountStatus = "Mount not connected"; return; }

            if (TargetAltitude < LowAltitudeWarningDeg) {
                var answer = MessageBox.Show(
                    $"Target altitude is {TargetAltitude:F1}° - below {LowAltitudeWarningDeg:F0}°. Slew anyway?",
                    "Low-altitude slew", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (answer != MessageBoxResult.Yes) { MountStatus = "Slew cancelled"; return; }
            }

            try {
                var settings = profileService.ActiveProfile.AstrometrySettings;
                var coords = new TopocentricCoordinates(
                    Angle.ByDegree(((TargetAzimuth % 360) + 360) % 360),
                    Angle.ByDegree(TargetAltitude),
                    Angle.ByDegree(settings.Latitude),
                    Angle.ByDegree(settings.Longitude),
                    settings.Elevation);

                slewCts?.Dispose();
                slewCts = new CancellationTokenSource();
                MountStatus = $"Slewing to Az {((TargetAzimuth % 360) + 360) % 360:F1}° / Alt {TargetAltitude:F1}°...";
                var ok = await telescopeMediator.SlewToTopocentricCoordinates(coords, slewCts.Token);
                MountStatus = ok ? "Slew complete" : "Slew failed";
            } catch (OperationCanceledException) {
                MountStatus = "Slew stopped";
            } catch (Exception ex) {
                MountStatus = "Slew error: " + ex.Message;
            }
        }

        private void Stop() {
            try {
                slewCts?.Cancel();
                telescopeMediator.StopSlew();
                MountStatus = "Stop sent";
            } catch (Exception ex) {
                MountStatus = "Stop error: " + ex.Message;
            }
        }

        private void SetTracking(bool enabled) {
            if (!MountConnected) { MountStatus = "Mount not connected"; return; }
            var ok = telescopeMediator.SetTrackingEnabled(enabled);
            MountStatus = ok ? (enabled ? "Tracking on" : "Tracking off") : "Tracking change failed";
        }

        // ---- ITelescopeConsumer ------------------------------------------------

        public void UpdateDeviceInfo(TelescopeInfo deviceInfo) {
            MountConnected = deviceInfo.Connected;
            MountAltitude = deviceInfo.Altitude;
            MountAzimuth = deviceInfo.Azimuth;

            if (Model.Points.Count > 0) {
                HorizonAltitudeAtMount = Model.GetAltitude(deviceInfo.Azimuth);
            }

            RefreshDevices();
        }

        public void Dispose() {
            telescopeMediator.RemoveConsumer(this);
        }
    }
}
