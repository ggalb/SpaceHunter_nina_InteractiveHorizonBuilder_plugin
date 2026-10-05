// Copyright © 2026 Space Hunter - Georg G Albrecht
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Reflection;
using System.Runtime.InteropServices;

// [MANDATORY] Unique plugin identifier. Generated once for this plugin - do not change after
// the first release, or N.I.N.A. will treat every future build as a different plugin.
[assembly: Guid("b3d4e2a1-7c6f-4a2b-9e10-2f8c5a1d3b44")]

// Version: set ONLY in PluginVersion.Value (end of this file); both attributes read it. N.I.N.A. compares this
// to decide what is "newer", so it must go up with every release (see CHANGELOG.md):
//   Major.Minor.Patch.Build - Major = breaking/big redesign, Minor = new features,
//   Patch = bug fixes, Build = re-release of the same code (e.g. a manifest fix).
// Never reuse a number that was already released; tag each release vX.Y.Z.B in git.
[assembly: AssemblyVersion(PluginVersion.Value)]
[assembly: AssemblyFileVersion(PluginVersion.Value)]

[assembly: AssemblyTitle("Interactive Horizon Builder")]
[assembly: AssemblyDescription("Edits or creates a horizon within N.I.N.A. based on manual data entry or by using connected gear (mount, camera etc).")]
[assembly: AssemblyCompany("Space Hunter - Georg G Albrecht")]
[assembly: AssemblyProduct("Interactive Horizon Builder")]
[assembly: AssemblyCopyright("Copyright © 2026 Space Hunter - Georg G Albrecht")]

// Minimum N.I.N.A. version this plugin was built and tested against.
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.2.0.9001")]

[assembly: AssemblyMetadata("License", "MPL-2.0")]
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
[assembly: AssemblyMetadata("Repository", "https://github.com/ggalb/SpaceHunter_nina_InteractiveHorizonBuilder_plugin")]
[assembly: AssemblyMetadata("Homepage", "https://github.com/ggalb/SpaceHunter_nina_InteractiveHorizonBuilder_plugin")]
[assembly: AssemblyMetadata("Tags", "Horizon,Custom Horizon,Sky Dome,Azimuth,Obstruction")]
[assembly: AssemblyMetadata("ChangelogURL", "https://github.com/ggalb/SpaceHunter_nina_InteractiveHorizonBuilder_plugin/blob/master/CHANGELOG.md")]
[assembly: AssemblyMetadata("FeaturedImageURL", "https://raw.githubusercontent.com/ggalb/SpaceHunter_nina_InteractiveHorizonBuilder_plugin/master/assets/Logo.png")]
[assembly: AssemblyMetadata("ScreenshotURL", "")]
[assembly: AssemblyMetadata("AltScreenshotURL", "")]
[assembly: AssemblyMetadata("LongDescription", @"Creates and edits N.I.N.A.'s custom horizon (.hrz) from inside N.I.N.A. - by manual entry or with your connected gear. Two views (Sky Dome + Strip) show the horizon exactly as N.I.N.A. reads it; edit points and Save to write the .hrz and reload it. No .hrz yet? Download template.hrz from GitHub (a flat line at 15° Alt) and link it in Options > General > Astrometry.

Before you start
- Rig polar aligned (a previous night's run or a permanent pier).
- If using the rotator, know its sky position - ideally 0° = Landscape at start. The angle buttons move MECHANICAL degrees from 0°; watch for cables, the Losmandy plate and other obstacles while it turns.
- Test on a COPY of your .hrz (Save overwrites in place). Link the copy in Options > General > Astrometry and restart N.I.N.A.
- Connect your gear; open the panel from the Imaging tab, like any plug-in.

The views
Sky Dome (polar: zenith centre, N up / E right / S down / W left, rings 15-75°) and Strip (azimuth N-E-S-W-N, altitude 0-90) show the same horizon: brown = blocked, dark = open sky. Gray dots = saved, red = unsaved. Cyan = mount, white cross = crosshair (your working point / slew target). On the Strip the wheel zooms and right-click resets.

Editing
- Select a dot (its Az/Alt load into Target), or click empty sky to move the crosshair.
- Place / Confirm adds a point at the crosshair, or moves the selected one there. Delete / Undo as needed.
- Save writes the .hrz and reloads it (red dots turn gray). Unsaved edits are lost until you Save.

Two ways to work
A. Offline (no gear): paste a frame's RA/Dec + time from a log into RA/Dec -> Alt/Az and Convert -> crosshair. Time - increases and Time + decreases the clearance margin along the target's path; add points where it clears.
B. Connected: at dawn, dusk or under cloud, adjust the camera for daylight and refocus. Slew to a point, Capture, and judge sky vs. obstruction; nudge with Alt or Time, re-slew, add the point when clear; move in Az and repeat to build the line.

Crosshair nudges: Time - / + traces the sky track (declination fixed). Az - / + moves in azimuth. Alt - / + moves up/down (editing only).

Equipment
- Focuser: shows position; enter a step and Move.
- Rotator: mechanical presets 0 / 315 / 45 / 90 + STOP (see the warning under Before you start).
- Flat panel: Open / Close.
- Filter: pick one to move the wheel.
- Guide-scope dust cover: On / Off (a Switch matched by name). A custom device of my own design - most users won't have one.
- Check frame: set Exp / Gain, then Capture or Abort.
- PHD2: Connect starts N.I.N.A.'s guider (exposure and gain are set in PHD2). With a separate guide scope, snap a PHD2 frame to confirm it is clear too.
- Mount: Slew, STOP, Track on / off - keep tracking OFF for horizon checks (fixed bearing).

The Sun: when up, it shows red 15° / yellow 30° rings and today's dashed path; a slew within 30° warns first. Consider closing the flat and dust cover before open-sky slews. N.I.N.A. checks the frame CENTRE against the horizon (it ignores sensor size and rotation).

Notes: a scratchpad saved with your profile.")]

[assembly: ComVisible(false)]

// The single place to change the plugin version (see the rules at the top of this file).
internal static class PluginVersion {
    public const string Value = "0.1.0.0";
}
