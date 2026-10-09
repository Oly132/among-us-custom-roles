# Among Us Custom Roles

Custom roles for Among Us v19s, build 7489: Forger, Puppeteer, Faker, Silencer, Cannibal, The Fat Guy, Dog Owner, Terrorist, Binocular and Evil Engineer. Every player needs the mod. Android support is deferred; Steam Deck support is experimental.

This repository contains the clean player source. Testing controls and debug role runners are excluded. Among Us and third-party dependency binaries are not included in source control. No player logs, credentials or personal configuration are included.

## Releases and updates

Download the Windows full installer from [GitHub Releases](https://github.com/Oly132/among-us-custom-roles/releases/latest). The Steam Deck package is experimental. Version 0.15.0 includes the automatic updater and the latest multiplayer/UI fixes.

The mod checks the latest GitHub Release on each launch, including when custom roles are disabled. If a newer release has an `update-manifest.json` asset, the main menu asks whether to download it. Choosing Yes closes the game. Close Silencer CrewLink too. Launch through Steam after the update finishes. Choosing Not now postpones the update until the next launch.

Files are compared by SHA-256, so only changed or missing files download. Valid cached downloads are reused. Updates are staged and checked before installation. Failed installation restores previously replaced files. Personal role settings are preserved. BetterCrewLink updates apply only to the separate Silencer copy when installed, leaving ordinary BetterCrewLink alone. The current updater requires the bundled Windows Node runtime installed by the PC installer. Steam Deck updater support needs its runtime to be included and an on-device test before it is ready.

Uploading code or changing README does not notify players. Publishing a newer release with a valid manifest does. An installation made with an older installer needs the updater-enabled mod once before automatic updates are available.

## Building

Use .NET SDK 6 or newer and a legally owned supported game with BepInEx IL2CPP already installed and initialized. From this directory run:

```powershell
dotnet build Forger.csproj -c Release -p:GameDir="C:/path/to/Among Us"
```

The output DLL is `bin/Release/net6.0/Forger.dll`. Keep the game closed before replacing `BepInEx/plugins/Forger.dll`.

## Publishing a release

Build this clean source, update the plugin version, and prepare a payload folder containing only managed mod/dependency files. Run `prepare-release.py` with the payload, version, tag and output folder. Optionally supply the rebuilt Silencer CrewLink `resources/app.asar` with `--voice-asar`. Supply `--server-url` to merge a new HTTPS Impostor region address while preserving official regions and the player's selected region. This creates a manifest and one asset per unique changed-content hash. Upload all generated assets to the matching GitHub Release, make it the latest stable release, then publish it. The updater never installs prerelease/draft content through the latest stable URL.

Do not publish game binaries, generated game interop assemblies, personal configuration, or developer testing builds. Release assets must come from the clean build and belong to this repository. The manifest pins the supported GameAssembly hash and restricts file destinations and download URLs.
