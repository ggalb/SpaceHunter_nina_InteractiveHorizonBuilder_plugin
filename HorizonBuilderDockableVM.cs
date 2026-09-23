using CommunityToolkit.Mvvm.Input;
using NINA.Astrometry;
using NINA.Core.Model;
using NINA.Equipment.Equipment.MyTelescope;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace InteractiveHorizonBuilder {

    /// <summary>
    /// Dockable panel view-model. Phases 1-2: shows live mount Alt/Az and draws the custom horizon
    /// in the Sky Dome + Strip. Phase 3: mount control - slew to a target Alt/Az, stop, and tracking
    /// on/off, all through N.I.N.A.'s telescope mediator (no external bridge).
    /// </summary>
    [Export(typeof(IDockableVM))]
    public class HorizonBuilderDockableVM : DockableVM, ITelescopeConsumer {
        private readonly ITelescopeMediator telescopeMediator;
        private CancellationTokenSource slewCts;

        [ImportingConstructor]
        public HorizonBuilderDockableVM(IProfileService profileService, ITelescopeMediator telescopeMediator)
            : base(profileService) {
            this.telescopeMediator = telescopeMediator;

            Title = "Interactive Horizon Builder";

            Horizon = profileService.ActiveProfile.AstrometrySettings.Horizon;

            SlewCommand = new AsyncRelayCommand(SlewToTargetAsync);
            StopCommand = new RelayCommand(Stop);
            TrackingOnCommand = new RelayCommand(() => SetTracking(true));
            TrackingOffCommand = new RelayCommand(() => SetTracking(false));
            TargetToMountCommand = new RelayCommand(() => { TargetAzimuth = MountAzimuth; TargetAltitude = MountAltitude; });

            telescopeMediator.RegisterConsumer(this);
        }

        // ---- Live state (from the telescope consumer) --------------------------

        private CustomHorizon horizon;
        public CustomHorizon Horizon {
            get => horizon;
            set { horizon = value; RaisePropertyChanged(); }
        }

        private bool mountConnected;
        public bool MountConnected {
            get => mountConnected;
            set { mountConnected = value; RaisePropertyChanged(); }
        }

        private double mountAltitude = double.NaN;
        public double MountAltitude {
            get => mountAltitude;
            set { mountAltitude = value; RaisePropertyChanged(); }
        }

        private double mountAzimuth = double.NaN;
        public double MountAzimuth {
            get => mountAzimuth;
            set { mountAzimuth = value; RaisePropertyChanged(); }
        }

        private bool horizonLoaded;
        public bool HorizonLoaded {
            get => horizonLoaded;
            set { horizonLoaded = value; RaisePropertyChanged(); }
        }

        private double horizonAltitudeAtMount;
        public double HorizonAltitudeAtMount {
            get => horizonAltitudeAtMount;
            set { horizonAltitudeAtMount = value; RaisePropertyChanged(); }
        }

        // ---- Mount control (Phase 3) -------------------------------------------

        private double targetAzimuth = 0;
        public double TargetAzimuth {
            get => targetAzimuth;
            set { targetAzimuth = value; RaisePropertyChanged(); }
        }

        private double targetAltitude = 30;
        public double TargetAltitude {
            get => targetAltitude;
            set { targetAltitude = value; RaisePropertyChanged(); }
        }

        private string mountStatus = "Idle";
        public string MountStatus {
            get => mountStatus;
            set { mountStatus = value; RaisePropertyChanged(); }
        }

        public ICommand SlewCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand TrackingOnCommand { get; }
        public ICommand TrackingOffCommand { get; }
        public ICommand TargetToMountCommand { get; }

        private const double LowAltitudeWarningDeg = 20.0;

        private async Task SlewToTargetAsync() {
            if (!MountConnected) {
                MountStatus = "Mount not connected";
                return;
            }

            if (TargetAltitude < LowAltitudeWarningDeg) {
                var answer = MessageBox.Show(
                    $"Target altitude is {TargetAltitude:F1}° - below {LowAltitudeWarningDeg:F0}°. Slew anyway?",
                    "Low-altitude slew", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (answer != MessageBoxResult.Yes) {
                    MountStatus = "Slew cancelled";
                    return;
                }
            }

            try {
                var settings = profileService.ActiveProfile.AstrometrySettings;
                var coords = new TopocentricCoordinates(
                    Angle.ByDegree(NormalizeAzimuth(TargetAzimuth)),
                    Angle.ByDegree(TargetAltitude),
                    Angle.ByDegree(settings.Latitude),
                    Angle.ByDegree(settings.Longitude),
                    settings.Elevation);

                slewCts?.Dispose();
                slewCts = new CancellationTokenSource();
                MountStatus = $"Slewing to Az {NormalizeAzimuth(TargetAzimuth):F1}° / Alt {TargetAltitude:F1}°...";
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
            if (!MountConnected) {
                MountStatus = "Mount not connected";
                return;
            }
            var ok = telescopeMediator.SetTrackingEnabled(enabled);
            MountStatus = ok ? (enabled ? "Tracking on" : "Tracking off")
                             : "Tracking change failed";
        }

        private static double NormalizeAzimuth(double az) => ((az % 360) + 360) % 360;

        // ---- ITelescopeConsumer ------------------------------------------------

        public void UpdateDeviceInfo(TelescopeInfo deviceInfo) {
            MountConnected = deviceInfo.Connected;
            MountAltitude = deviceInfo.Altitude;
            MountAzimuth = deviceInfo.Azimuth;

            var current = profileService.ActiveProfile.AstrometrySettings.Horizon;
            if (!ReferenceEquals(current, Horizon)) {
                Horizon = current;
            }
            HorizonLoaded = current != null;
            if (current != null) {
                HorizonAltitudeAtMount = current.GetAltitude(deviceInfo.Azimuth);
            }
        }

        public void Dispose() {
            telescopeMediator.RemoveConsumer(this);
        }
    }
}
