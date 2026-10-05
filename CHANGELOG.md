# Changelog

All notable changes to Interactive Horizon Builder. Versions follow
`Major.Minor.Patch.Build` (the number N.I.N.A. uses to detect updates):

- **Major** - breaking change or major redesign
- **Minor** - new features
- **Patch** - bug fixes
- **Build** - re-release of the same code (e.g. packaging/manifest fix)

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
