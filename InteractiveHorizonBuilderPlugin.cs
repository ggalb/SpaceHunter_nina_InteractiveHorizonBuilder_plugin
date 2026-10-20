// Copyright © 2026 Space Hunter - Georg G Albrecht
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile;
using NINA.Profile.Interfaces;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace InteractiveHorizonBuilder {

    /// <summary>
    /// Plugin entry point and DataContext for the Options -> Plugins -> "Interactive Horizon Builder"
    /// tab. Only the Stellarium export settings live here; per-session knobs (gain, exposure, nudge
    /// step) live on the dockable panel itself so they can be changed live at the eyepiece.
    /// </summary>
    [Export(typeof(IPluginManifest))]
    public class InteractiveHorizonBuilderPlugin : PluginBase, INotifyPropertyChanged {

        [ImportingConstructor]
        public InteractiveHorizonBuilderPlugin(IProfileService profileService) {
            pluginSettings = new PluginOptionsAccessor(profileService, System.Guid.Parse("b3d4e2a1-7c6f-4a2b-9e10-2f8c5a1d3b44")); // same store as the panel
        }

        private readonly IPluginOptionsAccessor pluginSettings;

        /// <summary>Stellarium horizon_line_color, "r,g,b" 0-1. Read by the panel at each export.</summary>
        public string StellariumLineColor {
            get => pluginSettings.GetValueString(nameof(StellariumLineColor), StellariumExport.DefaultLineColor);
            set {
                pluginSettings.SetValueString(nameof(StellariumLineColor),
                    string.IsNullOrWhiteSpace(value) ? StellariumExport.DefaultLineColor : value.Trim());
                RaisePropertyChanged();
            }
        }

        public override Task Teardown() {
            return base.Teardown();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null) {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
