<img src="assets/Logo.png" alt="Interactive Horizon Builder logo" width="160" align="right" />

# Interactive Horizon Builder

A [N.I.N.A.](https://nighttime-imaging.eu/) plugin that creates and edits
N.I.N.A.'s custom horizon (`.hrz`) from inside N.I.N.A. - by manual entry or
with your connected gear. Two views (Sky Dome + Strip) show the horizon exactly
as N.I.N.A. reads it; edit points and Save to write the `.hrz` and reload it.

By Space Hunter - Georg G Albrecht.

⚠️ This plugin moves real equipment. Read the rotator and Sun warnings and the
[Disclaimer](#disclaimer) before use.

## Requirements

- N.I.N.A. **3.2.0.9001** or later (Windows). Tested on N.I.N.A. 3.2.1;
  **not yet tested on the N.I.N.A. 3.3 nightly builds.**
- Gear is optional: the plugin works offline, and every device (mount,
  camera, focuser, **rotator**, filter wheel, flat panel, **PHD2**) is optional -
  use only what you have connected.
- An existing `.hrz` horizon file. No `.hrz` yet? Download
  [`template.hrz`](template.hrz) from this repo (a flat line at 15° altitude)
  and link it in **Options > General > Astrometry**.

## Installation

1. Download `InteractiveHorizonBuilder.dll` from the
   [latest release](../../releases/latest).
2. Close N.I.N.A.
3. Copy the DLL into
   `%LOCALAPPDATA%\NINA\Plugins\3.0.0\Interactive Horizon Builder\`
   (create the folder if it doesn't exist).
4. Start N.I.N.A. and open the panel from the **Imaging** tab, like any
   plugin panel.

## Before you start

- Rig polar aligned (a previous night's run or a permanent pier).
- If using the rotator, know its sky position - ideally 0° = Landscape at
  start. The angle buttons move **mechanical** degrees from 0°; watch for
  cables, the dovetail plate and other obstacles while it turns.

> [!WARNING]
> ⚠️ **Rotator - test it first while standing next to the rig.** Try each angle
> button with your hand on the STOP button and watch for cable snags or
> collisions before using it unattended. Rotators with limited travel (less
> than 360°) can drive into a hard stop. The plugin works fine without a
> rotator connected.
- **Test on a copy of your `.hrz`** (Save overwrites in place). Link the copy
  in Options > General > Astrometry and restart N.I.N.A.
- Connect your gear.

## The views

**Sky Dome** (polar: zenith centre, N up / E right / S down / W left, rings
15-75°) and **Strip** (azimuth N-E-S-W-N, altitude 0-90) show the same
horizon: brown = blocked, dark = open sky. Gray dots = saved, red = unsaved.
Cyan = mount, white cross = crosshair (your working point / slew target).

On the Strip: mouse wheel zooms, left-drag pans, right-click resets the view.

## Editing

- Select a dot (its Az/Alt load into Target), or click empty sky to move the
  crosshair.
- **Place / Confirm** adds a point at the crosshair, or moves the selected one
  there. **Delete** / **Undo** as needed.
- **Save** writes the `.hrz` and reloads it (red dots turn gray). Unsaved edits
  are lost until you Save.

## Two ways to work

**A. Offline (no gear):** paste a frame's RA/Dec + time from a log into
*RA/Dec -> Alt/Az* and **Convert -> crosshair**. Time - increases and Time +
decreases the clearance margin along the target's path; add points where it
clears.

**B. Connected:** at dawn, dusk or under cloud, adjust the camera for daylight
and refocus. Slew to a point, Capture, and judge sky vs. obstruction; nudge
with Alt or Time, re-slew, add the point when clear; move in Az and repeat to
build the line.

Crosshair nudges: **Time - / +** traces the sky track (declination fixed).
**Az - / +** moves in azimuth. **Alt - / +** moves up/down (editing only).

## Equipment

- **Focuser:** shows position; enter a step and Move.
- **Rotator:** mechanical presets 0 / 315 / 45 / 90 + STOP. ⚠️ Test next to
  the rig first (see the warning under *Before you start*). Optional.
- **Flat panel:** Open / Close.
- **Filter:** pick one to move the wheel.
- **Guide-scope dust cover:** On / Off (an ASCOM Switch matched by name). A
  custom device of my own design - most users won't have one.
- **PHD2:** Connect starts N.I.N.A.'s guider (exposure and gain are set in
  PHD2; watch the taken image in PHD2). With a separate guide scope, snap a
  PHD2 frame to confirm it is clear too. Optional - the plugin works without
  PHD2.
- **Mount:** Slew, STOP, Track on / off - keep tracking **off** for horizon
  checks (fixed bearing).
- **Check frame:** set Exp / Gain, then Capture or Abort.

## The Sun

When the Sun is up, the views show red 15° / yellow 30° rings and today's
dashed path; a slew within 30° of the Sun warns first. Consider closing the
flat panel and dust cover before open-sky slews.

> [!CAUTION]
> 🚫 **Never slew to within 15° of the Sun** (inside the red ring). Sunlight
> through the optics can destroy the camera sensor and other equipment, and
> can cause permanent eye damage. The plugin only *warns* before such a slew -
> it does not block it. Make sure nobody looks through the telescope or
> finder during daytime work.

Note: N.I.N.A. checks the frame **centre** against the horizon (it ignores
sensor size and rotation).

## Notes

A scratchpad in the panel to add your nighttime and daytime settings, such as
focus position, gain etc. Saved with your N.I.N.A. profile.

## Building from source

Requires the .NET 8 SDK and N.I.N.A. installed in its default location (the
project references N.I.N.A.'s own DLLs from the install folder).

```
dotnet build -c Release
```

A Debug build also copies the DLL into your local N.I.N.A. plugins folder
(close N.I.N.A. first).

## Changelog

See [CHANGELOG.md](CHANGELOG.md).

## Disclaimer

> [!IMPORTANT]
> This plugin moves real equipment (mount, rotator, focuser, covers).
> **Use it at your own risk.** No responsibility or liability is assumed for
> any damage to equipment, injury, or other loss resulting from its use. It is
> provided "as is", without warranty of any kind (see sections 6 and 7 of the
> license).

## License

Copyright © 2026 Space Hunter - Georg G Albrecht. Licensed under the
[Mozilla Public License 2.0](LICENSE).
