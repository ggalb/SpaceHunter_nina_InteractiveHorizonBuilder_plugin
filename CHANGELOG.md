# Changelog

All notable changes to Interactive Horizon Builder. Versions follow
`Major.Minor.Patch.Build` (the number N.I.N.A. uses to detect updates):

- **Major** - breaking change or major redesign
- **Minor** - new features
- **Patch** - bug fixes
- **Build** - re-release of the same code (e.g. packaging/manifest fix)

## 0.2.1.0 - 2026-10-19

- New **Stellarium...** button: exports the horizon as a Stellarium landscape
  ZIP (ini + horizon list + transparent panorama), so Stellarium shows the same
  horizon line as N.I.N.A. The ZIP file name becomes the landscape name; the
  location comes from the N.I.N.A. profile (used when Stellarium's "Location
  from landscape" is ticked).
- The line colour is chosen in Stellarium itself (Landscape tab).
- New **Carte du Ciel...** button: saves the horizon as a Carte du Ciel
  horizon file (.txt). Both export buttons sit at the top of the panel.
- Open... / Save As... also list .txt horizon files.

## 0.2.0.1 - 2026-10-16

- Plugin page now starts with a safety warning: the plugin moves real
  equipment - test each control with the rig in view, never slew within 15°
  of the Sun. No code changes.

## 0.2.0.0 - 2026-10-13

- Horizon views now use exactly the same calculation as N.I.N.A. (and Target
  Scheduler), including how N.I.N.A. closes the line at north when the file
  has no 0° or 360° point.
- 0° and 360° are kept as separate points, as in N.I.N.A.
- Vertical steps: N.I.N.A. keeps only one altitude per azimuth, so a point
  placed at the same azimuth as another now becomes a step - the higher point
  stays, the lower one moves 0.01° to the open side. A third point on an
  existing step is refused.
- Opening a .hrz with duplicate azimuths, lines N.I.N.A. ignores or azimuths
  outside 0-360 fixes them on load and reports it (fixed points show red
  until saved).
- Save refuses a horizon with fewer than 2 points (N.I.N.A. rejects it).
- Az / Alt boxes show 2 decimals and keep what you type (applied when you
  leave the box); the nudge step accepts 0.01.
- Optional "Chart colours from N.I.N.A. theme" switch next to the title:
  charts use N.I.N.A.'s colour schema, incl. the night (alternative) colours.
  Off by default (the plugin's own colours).
- **Open...** loads any .hrz and **Save As...** saves to a new file; both make
  that file N.I.N.A.'s horizon. Save As also creates a horizon from scratch.
- The first save of a file per session keeps the previous version as
  `.hrz.bak`.
- The file in use is shown under the status line. Save with no file linked
  opens Save As.
- Save, Save As and Open now hand the horizon to N.I.N.A. the same way its
  Options page does, so Options, Sky Atlas, Framing and sequencer targets
  show the new horizon at once (before: after a restart).

## 0.1.0.0 - 2026-10-05

First release.

- Sky Dome and Strip views of N.I.N.A.'s custom horizon (.hrz), with Sun
  position, Sun path and 15°/30° Sun-proximity rings.
- Edit horizon points: select, place/confirm, delete, undo, save (writes the
  .hrz and reloads it into N.I.N.A.).
- Strip view: mouse-wheel zoom, left-drag pan, right-click to reset.
- Mount control: slew to the crosshair (with near-Sun guard), stop, tracking
  on/off; Az/Alt and time (RA-track) nudges.
- RA/Dec -> Alt/Az converter for coordinates taken from an imaging log.
- Equipment controls: focuser, rotator (mechanical angles + stop), flat panel
  cover, dust-cover switch, filter wheel, PHD2 connect.
- Check frame: capture a main-camera snapshot to judge clear/obstructed sky.
- Persistent Notes box saved with the N.I.N.A. profile.
- `template.hrz` starter horizon (flat line at 15° altitude).
