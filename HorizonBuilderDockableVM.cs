using CommunityToolkit.Mvvm.Input;
using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Equipment.Equipment.MyTelescope;
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
        private CancellationTokenSource slewCts;
        private readonly Stack<HorizonModel> undoStack = new Stack<HorizonModel>();

        [ImportingConstructor]
        public HorizonBuilderDockableVM(IProfileService profileService, ITelescopeMediator telescopeMediator)
            : base(profileService) {
            this.telescopeMediator = telescopeMediator;

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

        private void PushUndo() => undoStack.Push(Model.Clone());

        private void Bump() {
            EditVersion++;
            if (Model.Points.Count > 0 && !double.IsNaN(MountAzimuth)) {
                HorizonAltitudeAtMount = Model.GetAltitude(MountAzimuth);
            }
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
        }

        public void Dispose() {
            telescopeMediator.RemoveConsumer(this);
        }
    }
}
