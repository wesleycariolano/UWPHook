# Library details components

UWPHook integrates Millennium 3.6.2 and NativeGameLink 5.0.0 as MIT-licensed
runtime dependencies. Original authors and licenses are preserved here.

Sources:
* https://github.com/SteamClientHomebrew/Millennium/tree/v3.6.2
* https://github.com/senkumil/NativeGameLink-for-Steam/tree/V5.0.0

`scripts/Prepare-LibraryDetails.ps1` downloads pinned release archives and checks
SHA-256 before packaging. The resulting archives are embedded into UWPHook.exe;
there are no separate downloads or installer windows for the end user.

Distribution changes to NativeGameLink:
* The two embedded distribution API credentials are removed. UWPHook continues
  to use the user's configured key for artwork.
* Account-achievement writes, card farming, administrator-prompt suppression and
  changes to game steam_appid.txt files are disabled in the companion backend.
* The original plugin identifier stays intact for API/path compatibility.

The optional activation action installs the host and companion in the real Steam
folder with Steam closed. It records ownership, preserves other plugins and
settings, and rolls back failed file writes. Existing unowned installations are
not overwritten. Deactivation removes only this companion from enabled plugins.

UI rendering still depends on compatibility between Steam, Millennium and the
pinned NativeGameLink release. File, package and API tests cannot certify the
current live Steam interface. No license, Xbox achievement or Xbox playtime
conversion is implemented.
