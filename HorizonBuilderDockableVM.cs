using NINA.Equipment.Equipment.MyTelescope;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using System.ComponentModel.Composition;

namespace InteractiveHorizonBuilder {

    /// <summary>
    /// Phase 1 dockable panel. Proves the SDK wiring only: it registers as a telescope consumer,
    /// shows the live mount Alt/Az, and reads the active profile's custom horizon (altitude at the
    /// mount's current azimuth, plus whether a horizon is loaded). No drawing, no editing yet.
    /// </summary>
    [Export(typeof(IDockableVM))]
    public class HorizonBuilderDockableVM : DockableVM, ITelescopeConsumer {
        private readonly ITelescopeMediator telescopeMediator;

        [ImportingConstructor]
        public HorizonBuilderDockableVM(IProfileService profileService, ITelescopeMediator telescopeMediator)
            : base(profileService) {
            this.telescopeMediator = telescopeMediator;

            Title = "Interactive Horizon Builder";

            telescopeMediator.RegisterConsumer(this);
        }

        private bool mountConnected;
        public bool MountConnected {
            get => mountConnected;
            set { mountConnected = value; RaisePropertyChanged(); }
        }

        private double mountAltitude;
        public double MountAltitude {
            get => mountAltitude;
            set { mountAltitude = value; RaisePropertyChanged(); }
        }

        private double mountAzimuth;
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

        public void UpdateDeviceInfo(TelescopeInfo deviceInfo) {
            MountConnected = deviceInfo.Connected;
            MountAltitude = deviceInfo.Altitude;
            MountAzimuth = deviceInfo.Azimuth;

            // profileService is the protected field on BaseVM.
            var horizon = profileService.ActiveProfile.AstrometrySettings.Horizon;
            HorizonLoaded = horizon != null;
            if (horizon != null) {
                HorizonAltitudeAtMount = horizon.GetAltitude(deviceInfo.Azimuth);
            }
        }

        public void Dispose() {
            telescopeMediator.RemoveConsumer(this);
        }
    }
}
