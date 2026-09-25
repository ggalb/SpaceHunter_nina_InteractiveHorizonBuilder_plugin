using CommunityToolkit.Mvvm.Input;
using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Core.Model.Equipment;
using NINA.Equipment.Equipment.MyTelescope;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

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
        private readonly IFilterWheelMediator filterWheelMediator;
        private readonly ICameraMediator cameraMediator;
        private readonly IImagingMediator imagingMediator;
        private readonly IGuiderMediator guiderMediator;
        private CancellationTokenSource slewCts;
        private CancellationTokenSource captureCts;
        private CancellationTokenSource rotatorCts;
        private readonly Stack<HorizonModel> undoStack = new Stack<HorizonModel>();
        private readonly DispatcherTimer sunTimer;
        private DispatcherTimer deviceTimer;
        private readonly IPluginOptionsAccessor pluginSettings;
        private static readonly Guid PluginGuid = Guid.Parse("b3d4e2a1-7c6f-4a2b-9e10-2f8c5a1d3b44");
        private static readonly IProgress<ApplicationStatus> NoProgress = new Progress<ApplicationStatus>();

        [ImportingConstructor]
        public HorizonBuilderDockableVM(
            IProfileService profileService,
            ITelescopeMediator telescopeMediator,
            IFocuserMediator focuserMediator,
            IRotatorMediator rotatorMediator,
            IFlatDeviceMediator flatDeviceMediator,
            ISwitchMediator switchMediator,
            IFilterWheelMediator filterWheelMediator,
            ICameraMediator cameraMediator,
            IImagingMediator imagingMediator,
            IGuiderMediator guiderMediator)
            : base(profileService) {
            this.telescopeMediator = telescopeMediator;
            this.focuserMediator = focuserMediator;
            this.rotatorMediator = rotatorMediator;
            this.flatDeviceMediator = flatDeviceMediator;
            this.switchMediator = switchMediator;
            this.filterWheelMediator = filterWheelMediator;
            this.cameraMediator = cameraMediator;
            this.imagingMediator = imagingMediator;
            this.guiderMediator = guiderMediator;

            Title = "Interactive Horizon Builder";

            pluginSettings = new PluginOptionsAccessor(profileService, PluginGuid);
            notes = pluginSettings.GetValueString(nameof(Notes), "");

            Model = HorizonModel.Load(profileService.ActiveProfile.AstrometrySettings.HorizonFilePath);
            HorizonLoaded = Model.Points.Count > 0;

            SlewCommand = new AsyncRelayCommand(SlewToTargetAsync);
            StopCommand = new RelayCommand(Stop);
            TrackingOnCommand = new RelayCommand(() => SetTracking(true));
            TrackingOffCommand = new RelayCommand(() => SetTracking(false));
            TargetToMountCommand = new RelayCommand(() => { TargetAzimuth = MountAzimuth; TargetAltitude = MountAltitude; });

            PointPickedCommand = new RelayCommand<PickResult>(OnPointPicked);
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
            StopRotatorCommand = new RelayCommand(StopRotator);
            FlatOpenCommand = new AsyncRelayCommand(() => FlatCoverAsync(true));
            FlatCloseCommand = new AsyncRelayCommand(() => FlatCoverAsync(false));
            DustCoverOnCommand = new AsyncRelayCommand(() => DustCoverAsync(true));
            DustCoverOffCommand = new AsyncRelayCommand(() => DustCoverAsync(false));

            CaptureCommand = new AsyncRelayCommand(CaptureAsync);
            AbortCaptureCommand = new RelayCommand(() => captureCts?.Cancel());
            ConnectGuiderCommand = new AsyncRelayCommand(ConnectGuiderAsync);

            ConvertRaDecCommand = new RelayCommand(ConvertRaDec);

            // Both timers are bound to the UI dispatcher EXPLICITLY. N.I.N.A. may construct this
            // VM on a background (MEF) thread; a DispatcherTimer created there binds to a thread
            // with no running message pump and never ticks (that froze the Sun position). The
            // 4-arg ctor also auto-starts the timer.
            var uiDispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

            BuildSunPath();
            UpdateSun();
            sunTimer = new DispatcherTimer(TimeSpan.FromSeconds(20), DispatcherPriority.Background,
                (s, e) => UpdateSun(), uiDispatcher);

            // Passively detect equipment connection/state so the panel is correct on open
            // and updates on its own - without the user having to click a device button
            // (which would also fire that button's action). The mount consumer callback also
            // refreshes devices, but only while the mount is connected and broadcasting.
            RefreshDevices();
            deviceTimer = new DispatcherTimer(TimeSpan.FromSeconds(5), DispatcherPriority.Background,
                (s, e) => RefreshDevices(), uiDispatcher);

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

        private string notes;
        public string Notes {
            get => notes;
            set { notes = value; RaisePropertyChanged(); pluginSettings?.SetValueString(nameof(Notes), value ?? ""); }
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
        public ICommand StopRotatorCommand { get; }
        public ICommand FlatOpenCommand { get; }
        public ICommand FlatCloseCommand { get; }
        public ICommand DustCoverOnCommand { get; }
        public ICommand DustCoverOffCommand { get; }
        public ICommand CaptureCommand { get; }
        public ICommand AbortCaptureCommand { get; }
        public ICommand ConnectGuiderCommand { get; }
        public ICommand ConvertRaDecCommand { get; }

        private const double LowAltitudeWarningDeg = 20.0;

        // ---- Editing -----------------------------------------------------------

        private void OnPointPicked(PickResult pick) {
            if (pick == null) return;
            if (pick.Index >= 0 && pick.Index < Model.Points.Count) {
                SelectedIndex = pick.Index;
                TargetAzimuth = Model.Points[pick.Index].Azimuth;
                TargetAltitude = Model.Points[pick.Index].Altitude;
                EditStatus = $"Selected point {pick.Index + 1}/{Model.Points.Count}";
            } else {
                SelectedIndex = -1;
                TargetAzimuth = pick.Azimuth;
                TargetAltitude = pick.Altitude;
                EditStatus = "Crosshair placed - Confirm to add a point here";
            }
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

        private string filterState = "-";
        public string FilterState { get => filterState; set { filterState = value; RaisePropertyChanged(); } }

        private string deviceStatus = "Idle";
        public string DeviceStatus { get => deviceStatus; set { deviceStatus = value; RaisePropertyChanged(); } }

        public System.Collections.IEnumerable Filters =>
            profileService.ActiveProfile.FilterWheelSettings.FilterWheelFilters;

        private FilterInfo selectedFilter;
        public FilterInfo SelectedFilter {
            get => selectedFilter;
            set {
                selectedFilter = value;
                RaisePropertyChanged();
                if (value != null) ChangeFilterAsync(value);
            }
        }

        private async void ChangeFilterAsync(FilterInfo filter) {
            try {
                var info = filterWheelMediator.GetInfo();
                if (info == null || !info.Connected) { DeviceStatus = "Filter wheel not connected"; return; }
                DeviceStatus = $"Filter -> {filter.Name}...";
                await filterWheelMediator.ChangeFilter(filter, CancellationToken.None, NoProgress);
                DeviceStatus = $"Filter: {filter.Name}";
            } catch (Exception ex) { DeviceStatus = "Filter error: " + ex.Message; }
        }

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
            try {
                var fw = filterWheelMediator.GetInfo();
                FilterState = fw == null || !fw.Connected ? "not connected"
                    : (fw.IsMoving ? "moving..." : (fw.SelectedFilter?.Name ?? "-"));
            } catch { FilterState = "-"; }
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
                rotatorCts?.Cancel();
                rotatorCts = new CancellationTokenSource();
                DeviceStatus = $"Rotator -> {angle:F0}°...";
                await rotatorMediator.MoveMechanical(angle, rotatorCts.Token);
                DeviceStatus = $"Rotator at {angle:F0}°";
                RefreshDevices();
            } catch (OperationCanceledException) {
                DeviceStatus = "Rotator stopped";
                RefreshDevices();
            } catch (Exception ex) { DeviceStatus = "Rotator error: " + ex.Message; }
        }

        // Stop an in-flight rotator move. There is no Halt on IRotatorMediator, so cancel the
        // move's token AND call Halt() on the underlying device (ASCOM IRotator). Halt is optional
        // at the driver level - if it is not implemented it throws, which we swallow.
        private void StopRotator() {
            rotatorCts?.Cancel();
            try {
                (rotatorMediator.GetDevice() as IRotator)?.Halt();
                DeviceStatus = "Rotator stopped";
            } catch (Exception ex) {
                DeviceStatus = "Rotator stop (Halt not supported): " + ex.Message;
            }
            RefreshDevices();
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

        // ---- Main camera check frame + PHD2 ------------------------------------

        private double cameraExposure = 2.0;
        public double CameraExposure { get => cameraExposure; set { cameraExposure = value; RaisePropertyChanged(); } }

        private int cameraGain = 100;
        public int CameraGain { get => cameraGain; set { cameraGain = value; RaisePropertyChanged(); } }

        private System.Windows.Media.Imaging.BitmapSource lastFrame;
        public System.Windows.Media.Imaging.BitmapSource LastFrame { get => lastFrame; set { lastFrame = value; RaisePropertyChanged(); } }

        private string cameraStatus = "Idle";
        public string CameraStatus { get => cameraStatus; set { cameraStatus = value; RaisePropertyChanged(); } }

        private string guiderState = "-";
        public string GuiderState { get => guiderState; set { guiderState = value; RaisePropertyChanged(); } }

        private async Task CaptureAsync() {
            try {
                var info = cameraMediator.GetInfo();
                if (info == null || !info.Connected) { CameraStatus = "Camera not connected"; return; }

                var seq = new CaptureSequence {
                    ExposureTime = CameraExposure,
                    ImageType = CaptureSequence.ImageTypes.SNAPSHOT,
                    Gain = CameraGain,
                    TotalExposureCount = 1
                };

                captureCts?.Dispose();
                captureCts = new CancellationTokenSource();
                CameraStatus = $"Capturing {CameraExposure:0.##}s @ gain {CameraGain}...";
                var rendered = await imagingMediator.CaptureAndPrepareImage(
                    seq, new NINA.Core.Utility.PrepareImageParameters(true, false), captureCts.Token, NoProgress);

                var image = rendered?.Image;
                if (image != null && image.CanFreeze && !image.IsFrozen) image.Freeze();
                LastFrame = image;
                CameraStatus = image != null ? "Frame captured - judge clear/obstructed by eye" : "No image returned";
            } catch (OperationCanceledException) {
                CameraStatus = "Capture aborted";
            } catch (Exception ex) {
                CameraStatus = "Capture error: " + ex.Message;
            }
        }

        private async Task ConnectGuiderAsync() {
            try {
                GuiderState = "Connecting PHD2...";
                var ok = await guiderMediator.Connect();
                GuiderState = ok ? "PHD2 connected" : "PHD2 connect failed";
            } catch (Exception ex) {
                GuiderState = "PHD2 error: " + ex.Message;
            }
        }

        // ---- RA/Dec -> Alt/Az converter ----------------------------------------

        private string raInput = "";
        public string RaInput { get => raInput; set { raInput = value; RaisePropertyChanged(); } }

        private string decInput = "";
        public string DecInput { get => decInput; set { decInput = value; RaisePropertyChanged(); } }

        private string timeInput = "";
        public string TimeInput { get => timeInput; set { timeInput = value; RaisePropertyChanged(); } }

        private bool raInHours = true;
        public bool RaInHours { get => raInHours; set { raInHours = value; RaisePropertyChanged(); } }

        private string converterStatus = "Paste RA/Dec + time from an imaging log";
        public string ConverterStatus { get => converterStatus; set { converterStatus = value; RaisePropertyChanged(); } }

        private void ConvertRaDec() {
            if (!TryParseAngle(RaInput, RaInHours, out double raDeg)) { ConverterStatus = "Could not parse RA"; return; }
            if (!TryParseAngle(DecInput, false, out double decDeg)) { ConverterStatus = "Could not parse Dec"; return; }
            DateTime when;
            if (string.IsNullOrWhiteSpace(TimeInput)) when = DateTime.Now;
            else if (!DateTime.TryParse(TimeInput, CultureInfo.CurrentCulture, DateTimeStyles.None, out when)) {
                ConverterStatus = "Could not parse date/time"; return;
            }
            var s = profileService.ActiveProfile.AstrometrySettings;
            var (az, alt) = EqToHoriz(raDeg, decDeg, s.Latitude, s.Longitude, when.ToUniversalTime());
            TargetAzimuth = az;
            TargetAltitude = alt;
            SelectedIndex = -1;
            ConverterStatus = $"Crosshair -> Az {az:F1}° / Alt {alt:F1}°  (Time ± to trace the track)";
        }

        // ---- Sun position + safety rings ---------------------------------------

        private double sunAzimuth = double.NaN;
        public double SunAzimuth { get => sunAzimuth; set { sunAzimuth = value; RaisePropertyChanged(); } }

        private double sunAltitude = double.NaN;
        public double SunAltitude { get => sunAltitude; set { sunAltitude = value; RaisePropertyChanged(); } }

        private PointCollection sunPath;
        public PointCollection SunPath { get => sunPath; set { sunPath = value; RaisePropertyChanged(); } }

        public bool SunUp => !double.IsNaN(SunAltitude) && SunAltitude >= 0;

        private void UpdateSun() {
            var s = profileService.ActiveProfile.AstrometrySettings;
            var (az, alt) = SunAltAz(DateTime.UtcNow, s.Latitude, s.Longitude);
            SunAzimuth = az;
            SunAltitude = alt;
            RaisePropertyChanged(nameof(SunUp));
        }

        private void BuildSunPath() {
            var s = profileService.ActiveProfile.AstrometrySettings;
            var pts = new PointCollection();
            var midnight = DateTime.Now.Date;
            for (int m = 0; m <= 1440; m += 10) {
                var (az, alt) = SunAltAz(midnight.AddMinutes(m).ToUniversalTime(), s.Latitude, s.Longitude);
                if (alt >= 0) pts.Add(new Point(az, alt));
            }
            pts.Freeze();
            SunPath = pts;
        }

        // ---- Astronomy helpers (self-contained, LST-based) ---------------------

        private const double D2R = Math.PI / 180.0, R2D = 180.0 / Math.PI;

        private static double Norm360(double d) => ((d % 360) + 360) % 360;

        private static double ToJulianUtc(DateTime utc) {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return 2440587.5 + (utc - epoch).TotalDays;
        }

        /// <summary>Equatorial (RA/Dec, degrees) to horizontal (az from N, alt) at a site and UTC.</summary>
        private static (double az, double alt) EqToHoriz(double raDeg, double decDeg, double latDeg, double lonEastDeg, DateTime utc) {
            double jd = ToJulianUtc(utc);
            double gmst = Norm360(280.46061837 + 360.98564736629 * (jd - 2451545.0));
            double lst = Norm360(gmst + lonEastDeg);
            double ha = (lst - raDeg) * D2R;
            double lat = latDeg * D2R, dec = decDeg * D2R;
            double sinAlt = Math.Sin(lat) * Math.Sin(dec) + Math.Cos(lat) * Math.Cos(dec) * Math.Cos(ha);
            sinAlt = Math.Max(-1, Math.Min(1, sinAlt));
            double alt = Math.Asin(sinAlt);
            double cosAlt = Math.Cos(alt);
            double az = 0;
            if (Math.Abs(cosAlt) > 1e-9) {
                double sinAz = -Math.Cos(dec) * Math.Sin(ha) / cosAlt;
                double cosAz = (Math.Sin(dec) - Math.Sin(lat) * sinAlt) / (Math.Cos(lat) * cosAlt);
                az = Math.Atan2(sinAz, cosAz) * R2D;
            }
            return (Norm360(az), alt * R2D);
        }

        /// <summary>Low-precision Sun position (Meeus) -> horizontal az/alt.</summary>
        private static (double az, double alt) SunAltAz(DateTime utc, double latDeg, double lonEastDeg) {
            double n = ToJulianUtc(utc) - 2451545.0;
            double L = Norm360(280.460 + 0.9856474 * n);
            double g = Norm360(357.528 + 0.9856003 * n) * D2R;
            double lambda = (L + 1.915 * Math.Sin(g) + 0.020 * Math.Sin(2 * g)) * D2R;
            double eps = (23.439 - 0.0000004 * n) * D2R;
            double raDeg = Norm360(Math.Atan2(Math.Cos(eps) * Math.Sin(lambda), Math.Cos(lambda)) * R2D);
            double decDeg = Math.Asin(Math.Sin(eps) * Math.Sin(lambda)) * R2D;
            return EqToHoriz(raDeg, decDeg, latDeg, lonEastDeg, utc);
        }

        private static double AngularDistanceDeg(double alt1, double az1, double alt2, double az2) {
            double a1 = alt1 * D2R, a2 = alt2 * D2R, dAz = (az1 - az2) * D2R;
            double cosd = Math.Sin(a1) * Math.Sin(a2) + Math.Cos(a1) * Math.Cos(a2) * Math.Cos(dAz);
            cosd = Math.Max(-1, Math.Min(1, cosd));
            return Math.Acos(cosd) * R2D;
        }

        private static bool TryParseAngle(string text, bool hours, out double degrees) {
            degrees = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var t = text.Trim().Replace(":", " ");
            var parts = t.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            double sign = t.StartsWith("-") ? -1 : 1;
            if (parts.Length == 1) {
                if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out degrees)) return false;
            } else {
                if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var a)) return false;
                double b = 0, c = 0;
                if (parts.Length > 1) double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out b);
                if (parts.Length > 2) double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out c);
                degrees = sign * (Math.Abs(a) + b / 60.0 + c / 3600.0);
            }
            if (hours) degrees *= 15.0;
            return true;
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

            if (SunUp) {
                double sunDist = AngularDistanceDeg(TargetAltitude, TargetAzimuth, SunAltitude, SunAzimuth);
                if (sunDist < 30.0) {
                    var answer = MessageBox.Show(
                        $"Target is {sunDist:F0}° from the Sun (below 30°). The scope's path may cross the Sun - protect the optics. Slew anyway?",
                        "Near-Sun slew", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (answer != MessageBoxResult.Yes) { MountStatus = "Slew cancelled (near Sun)"; return; }
                }
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
            sunTimer?.Stop();
            deviceTimer?.Stop();
            telescopeMediator.RemoveConsumer(this);
        }
    }
}
