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
[assembly: AssemblyMetadata("LongDescription", @"Interactive Horizon Builder ports the SpaceHunter Horizon Editor into N.I.N.A. as a dockable panel. It draws the custom horizon in a Sky Dome (polar) and Strip (azimuth x altitude) view, lets you slew to a horizon point using N.I.N.A.'s own mount, take a daytime/dawn check frame with the main camera (adjustable gain and exposure), decide by eye whether the sky is clear, nudge in time (RA) or azimuth, and add/update points - then save the horizon back into N.I.N.A. Main flat panel (open/close) and the guide-scope dust cover (a Switch) can be toggled to protect optics during daytime slews; PHD2 is connected through N.I.N.A.'s guider. Replaces the previous external PowerShell ASCOM bridge entirely.")]

[assembly: ComVisible(false)]
