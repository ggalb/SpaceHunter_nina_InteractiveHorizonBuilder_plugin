// Copyright © 2026 Space Hunter - Georg G Albrecht
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile.Interfaces;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace InteractiveHorizonBuilder {

    /// <summary>
    /// Plugin entry point and DataContext for the Options -> Plugins -> "Interactive Horizon Builder"
    /// tab. Phase 1 holds no settings yet; per-session knobs (gain, exposure, nudge step) will live
    /// on the dockable panel itself so they can be changed live at the eyepiece.
    /// </summary>
    [Export(typeof(IPluginManifest))]
    public class InteractiveHorizonBuilderPlugin : PluginBase, INotifyPropertyChanged {

        [ImportingConstructor]
        public InteractiveHorizonBuilderPlugin(IProfileService profileService) {
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
