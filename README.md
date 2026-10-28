# UWPHook 3.0 — one connected library

A new Windows interface and a single import action for Xbox / Windows games in
Steam. Based on [BrianLima/UWPHook](https://github.com/BrianLima/UWPHook), with
original MIT credits preserved.

## Use it

1. Keep UWPHook.exe in a permanent folder and open it.
2. Select an installed game. Its saved Steam edition, localized description and
   languages load in the same window. For an ambiguous title, use **Conferir ou
   trocar a edição** once to choose the release or enter its Steam AppID.
3. Click **Adicionar à Steam** (or **Atualizar na Steam** for an existing game).
   UWPHook closes Steam normally, prepares the included library presentation,
   imports the shortcut, official artwork and committed metadata mapping, and
   reopens Steam. No manual linking step in Steam is needed.

The first setup may show a Windows administrator prompt for the Steam folder.
Official artwork does not require a SteamGridDB API key. Existing custom artwork,
shortcut identity, tags, visibility and play history are preserved. Import writes
have a restore manifest under `%APPDATA%\Briano\UWPHook\backups` and roll back on
failure. An existing independently managed NativeGameLink installation is not
overwritten.

The new charcoal interface includes installed-title search, a game preview,
Portuguese catalog information, edition selection and integrated preferences.
Previously saved edition choices are reused. MWIII (2023) uses Steam AppID
**3595270**; it is kept distinct from Modern Warfare 3 (2011).

## What is integrated

The same UWPHook executable contains the Xbox launcher and the optional Steam
presentation components Millennium 3.6.2 / NativeGameLink 5.0.0. Their setup is
handled by the import action; no separate download or plugin configuration is
required. Steam still needs its client extension at runtime for detailed
non-Steam library pages. The included MIT dependencies remain credited; see
[component provenance](third_party/LibraryDetails/README.md).

Version 3.0 fixes the missing final integration: committed mappings must be keyed
as `shortcut:<unsigned shortcut ID>`. Earlier UWPHook versions wrote bare numbers,
which the frontend interpreted as title aliases. Import now writes the canonical
shortcut name and the correct mapping together, before Steam starts. The
frontend's own already-linked predicate suppresses its linking review.

The game continues using its installed Xbox activation flow. This does not
convert Xbox achievements, hours or licenses into Steam account progress.

## Build and verify

Requires Windows, PowerShell 7, .NET 8 SDK, Node.js 24 and pnpm.

```powershell
./scripts/Prepare-LibraryDetails.ps1
pnpm install --frozen-lockfile --ignore-scripts
dotnet run --project UWPHook.Tests/UWPHook.Tests.csproj -c Release -- --contract-output .bundles/import-contract.json
pnpm test:companion
dotnet publish UWPHook/UWPHook.csproj -c Release -r win-x64 --self-contained false -o publish
```

The published executable requires .NET 8 Desktop Runtime x64. Component archives
are pinned and hash-verified at build time. Tests cover first imports into a fresh
Steam profile, existing shortcut preservation, retries without duplicates,
transaction rollback, corrupted mappings, component ownership and activation.
The C# import output is executed against the packaged JavaScript already-linked
predicate and Lua backend, including the regression for the old numeric keys.
Tests use isolated fixtures and do not alter a live Steam library.

The live workflow still requires testing against the installed Steam client;
compatibility depends on Steam, Millennium and the included frontend version.

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
