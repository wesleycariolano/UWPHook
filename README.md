# sGs-app 3.1

A Windows library for connecting Xbox and PC games to Steam in one action.
Based on [BrianLima/UWPHook](https://github.com/BrianLima/UWPHook), with the MIT
license and original project credits preserved.

## Use the app

1. Keep `sGs-app.exe` in a permanent folder and open it. The title bar, sidebar
   and About section read the version compiled from the project file.
2. Select a game and review its Steam edition, description and languages.
3. Click **Vincular à Steam**. The app downloads official artwork, closes Steam
   normally, prepares its included presentation components, commits the shortcut
   and metadata together, and reopens Steam.

The automatic list contains registered games: GDK titles with game configuration
or game-launch metadata, declared legacy games and recognized classic Microsoft
games. It excludes Windows utilities, Xbox support services and framework packages.

If a title is missing, choose **+ Pasta do jogo**. A registered Xbox game keeps its
normal Windows/Xbox activation identity. For a regular PC game, choose the main
executable when several are found. Folder choices persist and are restored on the
next launch. Installer and crash-report executables are excluded. A registered
Xbox game is never converted into a direct launch of its protected executable.

A synchronized game displays **Vinculado** with an **Atualizar vínculo** action.
This label can be turned off in **Preferências**. The status is checked against
both the Steam shortcut and the committed metadata mapping, rather than only the
presence of a shortcut.

## Branding, version and compatibility

The application and executable are named **sGs-app**, with a new vector logo and
a Windows icon in seven sizes. `<Version>` in `UWPHook/UWPHook.csproj` is the single
version source; UI labels derive from the compiled assembly. `scripts/Build-BrandAssets.ps1`
reproduces the PNG and icon assets from the vector mark geometry.

Existing UWPHook launch identities and local selections are retained. State stays
in `%APPDATA%\Briano\UWPHook` for compatibility. Reimporting a game preserves its
shortcut ID, play history, tags, visibility and custom artwork. A valid empty
Steam shortcut file is accepted; other truncated files are rejected. Imports have
restore manifests and roll back partial writes.

## Included components

The same executable bundles Millennium 3.6.2 and NativeGameLink 5.0.0 with their MIT
licenses. The app handles setup; no separate end-user download is required.
[Component provenance](third_party/LibraryDetails/README.md) documents the pinned
archives and distribution changes. The companion accepts the legacy UWPHook and
new sGs-app launcher names without modifying game licensing files.

Steam mappings use `shortcut:<unsigned ID>` so the frontend recognizes imports as
already linked. Xbox titles continue using Xbox activation; folder-based PC games
use their chosen executable. Xbox license, achievement and hour conversion into
Steam account progress is not implemented.

## Build and check

Requires Windows, PowerShell 7, .NET 8 SDK, Node.js 24 and pnpm.

```powershell
./scripts/Build-BrandAssets.ps1
./scripts/Prepare-LibraryDetails.ps1
pnpm install --frozen-lockfile --ignore-scripts
dotnet run --project UWPHook.Tests/UWPHook.Tests.csproj -c Release -- --contract-output .bundles/import-contract.json
pnpm test:companion
dotnet publish UWPHook/UWPHook.csproj -c Release -r win-x64 --self-contained false -o publish
```

The published executable requires .NET 8 Desktop Runtime x64. Regression checks
cover game filtering, folder identities and persistence, executable selection,
Xbox folder resolution, brand compatibility, first imports into empty profiles,
duplicate prevention, rollback and committed frontend mapping recognition.
Tests use separate fixtures and do not alter a live Steam library. Optional
`--check-discovery` and `--check-folder <path>` harness modes inspect real Windows
registrations without changing them.

## Original project documentation

# UWPHook

[![](http://imgur.com/gWwR02D.png)](https://briano.dev/UWPHook/)

Small project to link UWP games and XboxGamePass to Steam

If you want to add Windows Store or Xbox Game Pass Games to Steam, you need to do a bit of a workaround because Steam can't see UWP apps, and there's a chance Steam won't show it on your "Currently playing" status. This app aims to simplify a little bit the process where it is possible by automating the scripting and launching of Windows Store apps and Xbox Game Pass games.

# To add UWP or XGP games to Steam #

[Download the latest version of UWPHook](https://github.com/BrianLima/UWPHook/releases) and store it somewhere on your PC.

Click on the 🔄 to load installed UWP Apps, we will find every UWP app and Xbox Game Pass game installed on your PC.

![](https://i.imgur.com/pjGfGHe.png)

Select every app you want to add to Steam, you can change the name by double clicking the "name" collumn, press "Export selected apps to Steam" and 🎉, every app you selected will be added to Steam.

![](https://i.imgur.com/on46CMQ.png)

Close UWPHook, restart Steam if prompted, click play on your UWP game, and Steam will show your current game on your status as long as you are playing it!

----------

# SteamGridDB #

Since v2.8, UWPHook can automatically import grid, icons and hero images from [SteamGridDB](https://www.steamgriddb.com)

On your first usage, the app will ask you if you want it to download images, redirecting you to the settings page.

![](https://i.imgur.com/K0Cm4IL.png)

By adding a API Key obtained in the SteamGridDB preferences, UWPHook will try to find matching images for any exported games, giving you the following result:

![](https://i.imgur.com/mlvVdwb.png)

You can refine the images by using filters for animated images, blurred, no logo or memes for example, but it will always pick the first it finds for the filters automatically.

Special thanks to @FusRoDah061 for implementing the base feature!

# Troubleshooting #

- **Steam's Overlay isn't working!**
  - Unfortunately, it's a Steam limitation, Valve has to update it in order to work properly with UWP, DXTory is a recommended overlay for UWP games.
- **Using Steam Link**
  - Check the option "Streaming" mode inside the settings screen
- **Steam Deck?**
  - This app is not compatible with the Steam Deck in any way.

 If you are facing an error of any kind, please check the contents of the file 
- **I have shortcuts from other application that broke when i used UWPHook**
  - You can find a backup of your `shortcuts.vdf` file in `%appdata%\Roaming\Briano\UWPHook\backups`, each file in this directory is a backup of the original `shortcuts.vdf` before manipulation by UWPHook, the files are renamed `{userid}_{timestamp}_shortcut.vdf`, you can restore these files to their original location for usage.
- **My question isn't listed here!**
  - Drop by our subreddit and ask a question over there, maybe someone will help you, i surely will as soon as i can:
 **[https://www.reddit.com/r/uwphook](https://www.reddit.com/r/uwphook)**
  - Please also paste the contents of the file `%appdata%Roaming\Briano\UWPHook\application.log` so i can try to understand better the problem.
----------

# Building

- Clone project or forked project.
- Install Visual Studio 2022 with .NET Framework 4.8.
- Install [SharpSteam by BrianLima](https://github.com/BrianLima/SharpSteam/releases/) and [VDFParser](https://github.com/brianlima/VDFParser) and build.
- If the project asks for the references for VDFParser and Sharpsteam, point it to the most recent build.
- Press run!

## Installer 

The installer is built with [NSIS](https://nsis.sourceforge.io/Download), just run the script `UWPHook.nsi` and things **should** work. Modify any hardcoded paths to suit your setup.
The installation consists of zipping the application and creating some of the paths for the user, since the application is mostly static/dynamic and does not depend a lot on where it is installed, the installer is made for convenience.

----------

# About

This software is open-source under the MIT License.
It will mostly likely break withouth any heads up, since any API, file format, script and many other things used by it may be changed by Valve or Microsoft withouth prior notice.

I am not responsible if anything breaks.

If you like what i did with it and want to suport me, you can cheer me up at my [Twitter](http://www.twitter.com/brianostorm "Twitter") or [pay me a coffee via Paypal, it will help me to continue to build amazing open source tools for you!"](https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=9YPV3FHEFRAUQ)

Thanks for your support, and game on!
