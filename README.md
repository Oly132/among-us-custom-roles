# Among Us Custom Roles

## 🔴 MANDATORY — 0.15.10 hotfix

[Download latest mandatory release](https://github.com/Oly132/among-us-custom-roles/releases/latest) · [Role guide](ROLES.md) · [Update channel rules](UPDATE-CHANNELS.md)

OFF now selects official Europe, preserves private addresses, skips role/cosmetic patches, and requires restarting before joining. The menu/updater remain loaded; OFF is not an uninstall. Saving a private address while OFF no longer selects that server.

| Platform | Choose this download |
| --- | --- |
| Windows PC (recommended) | **Windows-Setup.exe** |
| Steam Deck / Linux Proton | **SteamDeck.zip** |
| Android ARM64 | **Android-Beta.apk** |
| Developers only | Windows-Testing-Setup.exe (test UI; manual updates) |

Every custom-role player should update to **0.15.10** and restart before creating a fresh lobby. Older updater-enabled versions can jump directly here, including **0.15.0**. Their old Download confirmation may still require one click. From 0.15.10 onward, mandatory downloads automatically start on the main menu with progress and verified completion. Only changed files download. Missing updater/runtime or unsupported game builds require the appropriate installer.

## 🟡 OPTIONAL PREVIEWS — manual only

Optional releases are GitHub **Pre-releases**, marked **OPTIONAL — MANUAL ONLY**. They are never Latest and never go on the live mandatory feed, so the game will not download them. Preview changes join the next mandatory release when ready. **No optional preview is being published with this hotfix.** Editing source code or README alone never triggers updates.

## Install and compatibility

Install over the existing mod; no uninstall is needed. Windows includes optional Silencer BetterCrewLink support. Deck uses Steam launch options `WINEDLLOVERRIDES="winhttp=n,b" %command%`. Android requires the installed ARM64 Among Us 2026.9.29; install the signed launcher APK over the previous launcher and open your mod profile. Google Play sign-in in the launcher remains unsupported. No working iOS installer is available.

Normal Windows, Deck and Android packages have no testing UI. Only the Windows developer installer/source contain testing controls. Private QA files, signing keys and personal host addresses are excluded. Get the server address privately from your host.

[Detailed installation guide](https://github.com/Oly132/among-us-custom-roles/releases/download/v0.15.10/INSTALL.md).

## Source and validation

Root source is the clean Windows mod. Exact clean, developer and Android corresponding-source archives are attached to the release. Build using .NET and your legally owned supported Among Us with initialized BepInEx IL2CPP references:

```powershell
dotnet build Forger.csproj -c Release -p:GameDir="C:/path/to/Among Us"
```

The hotfix passes Windows/Android/developer build checks and region/update regression checks. It has not been tested live on a physical Steam Deck or Android phone. Prior server-connected Windows tests covered role assignment/buttons, Fat Guy first/second attacks, several abilities, Silencer countdown and victory paths; this is not exhaustive every-map/every-role certification. Intermittent replay-lobby disconnection remains under investigation; create a fresh lobby if returning fails.

## Lobby radio preview

[Receiver documentation](RADIO.md). This branch contains only the receiver; the private broadcasting connector is not distributed.
