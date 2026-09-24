using System.Reflection;
using System.Runtime.InteropServices;

// [MANDATORY] Unique plugin identifier. Generated once for this plugin - do not change after
// the first release, or N.I.N.A. will treat every future build as a different plugin.
[assembly: Guid("b3d4e2a1-7c6f-4a2b-9e10-2f8c5a1d3b44")]

[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]

[assembly: AssemblyTitle("Interactive Horizon Builder")]
[assembly: AssemblyDescription("Interactive Sky Dome + Strip horizon editor as a N.I.N.A. dockable panel.")]
[assembly: AssemblyCompany("Space Hunter - Georg G Albrecht")]
[assembly: AssemblyProduct("Interactive Horizon Builder")]
[assembly: AssemblyCopyright("Copyright © 2026 Space Hunter - Georg G Albrecht")]

// Minimum N.I.N.A. version this plugin was built and tested against.
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.2.0.9001")]

[assembly: AssemblyMetadata("License", "MPL-2.0")]
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
[assembly: AssemblyMetadata("Repository", "")]
[assembly: AssemblyMetadata("Homepage", "")]
[assembly: AssemblyMetadata("Tags", "Horizon,Custom Horizon,Sky Dome,Azimuth,Obstruction")]
[assembly: AssemblyMetadata("ChangelogURL", "")]
[assembly: AssemblyMetadata("FeaturedImageURL", "")]
[assembly: AssemblyMetadata("ScreenshotURL", "")]
[assembly: AssemblyMetadata("AltScreenshotURL", "")]
[assembly: AssemblyMetadata("LongDescription", @"Interactive Horizon Builder builds and edits N.I.N.A.'s custom horizon (.hrz) from inside N.I.N.A. It shows your horizon in two views, lets you check the real sky by slewing the mount and taking a frame, and saves the edited horizon straight back into N.I.N.A. It uses N.I.N.A.'s already-connected equipment - there is no separate program or bridge to run.

Before you start
- In Options > General, set your Horizon file. The plugin loads that file when it starts, so if you change it, restart N.I.N.A.
- Back up your .hrz first. Save overwrites the file in place.
- Connect the gear you want to use (mount, main camera, focuser, rotator, flat, switch, filter wheel, guider) as usual.
- Open the panel from the Imaging tab, the same place as the Azimuth Chart.

The two views
- Sky Dome (top left): a polar view. Zenith at the centre, the horizon at the rim, N up, E right, S down, W left. Altitude rings are 15/30/45/60/75 degrees. The brown area is blocked (below the horizon line); the dark area is open sky.
- Strip (below): azimuth across (N-E-S-W-N), altitude up (0-90) - the single-valued horizon exactly as N.I.N.A. reads it. Mouse wheel zooms around the pointer; right-click resets the zoom.
Both views show the same data. Gray dots are saved horizon points; red dots are unsaved edits. The cyan diamond/line is where the mount is pointing now. The white cross is the crosshair - your working point and slew target. The top line shows the mount connection, its Az/Alt, and the horizon altitude at the mount's azimuth.

Two ways to work
A. Offline, from an imaging log (no mount needed). Use this when a target recently clipped a new obstruction (a tree that grew, a new building). Take the last good frame's RA/Dec and date/time from N.I.N.A.'s session metadata, type them into RA/Dec -> Alt/Az, and click Convert -> crosshair. The crosshair jumps to where that frame was taken. Use Time - / Time + to step along that target's path across the sky, and add points where it clears the obstruction.

B. Connected, by slewing (dawn, dusk, or a cloudy day). Brighten the camera for daylight (raise gain, short exposure) and refocus for day use. Pick a point, Slew to it, press Capture, and look at the frame: if you see sky, the point is clear; if it is blocked, nudge with Az or Time, re-slew, and check again. Once clear, add the point.

Editing the horizon
- Select: click on a dot (within a few pixels); its Az/Alt load into the Target fields. Zoom in on the strip to pick between close dots.
- Place: click empty sky - the white crosshair moves there; nothing is added yet.
- Place / Confirm: adds a new point at the crosshair, or moves the selected point to the crosshair. New/edited points turn red (unsaved).
- Delete: removes the selected point. Undo: reverses the last add/move/delete.
- Save: writes the .hrz and reloads it into N.I.N.A. Red dots turn gray. Until you Save, edits are not on disk.

Corrections: Time vs Az/Alt
Time - / Time + moves the crosshair along the sky track a target at that spot would follow (declination held fixed, hour angle stepped) - use it to trace a target's path. Az - / Az + moves sideways in azimuth. Alt - / Alt + moves straight up/down (an editing convenience, not a sky track).

Equipment controls
- Focuser: shows the current position; type a target step and Move (for your day-focus offset).
- Rotator: preset mechanical angles 0 / 315 / 45 / 90 (0 is landscape).
- Main flat panel: Open / Close the cover.
- Guide-scope dust cover: On / Off (a switch, found by name on the connected Switch device).
- Filter: pick a filter from the dropdown to move the wheel.
- Check frame: set Exposure and Gain, Capture (shown in the preview), Abort to cancel.
- PHD2: Connect starts N.I.N.A.'s guider; guide exposure and gain are set in PHD2 itself.
- Mount: Slew (to the crosshair), STOP, Track on / Track off. For horizon checks you usually want tracking OFF so the view stays on a fixed bearing. Alt/Az mounts stop tracking on a slew by themselves; RA/Dec mounts keep tracking until you turn it off.

Daytime and the Sun
When the Sun is up it is drawn with a red 15-degree and yellow 30-degree safety ring, and today's Sun path is a dashed line. A Slew within 30 degrees of the Sun asks for confirmation first - it does not stop the slew. Before slewing under open sky, consider closing the flat panel and the dust cover to protect the optics. N.I.N.A. checks the frame centre against the horizon (it ignores sensor size and rotation), so pointing the scope at a horizon point shows exactly what N.I.N.A. will use.

Notes
The Notes box is a scratchpad saved with your profile - night focus position, day focus position, gain/offset, filter, rotation, or the target you are working on.

Recommendations
- Copy your .hrz before your first edits.
- Write down your night focus before refocusing for daytime.
- Save before you finish - unsaved (red) points are lost otherwise.")]

[assembly: ComVisible(false)]
