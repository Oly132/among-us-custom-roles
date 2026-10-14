# Among Us Custom Roles

Every player needs the mod. [Read the role guide](ROLES.md).

## Downloads

Get **version 0.15.9** from [the latest release](https://github.com/Oly132/among-us-custom-roles/releases/latest).

- **Windows Setup:** recommended normal installer; no testing UI. Close Among Us and Silencer CrewLink, install over the existing game folder, then launch through Steam.
- **Steam Deck Experimental:** clean package. Extract in Desktop Mode and run `install.sh`; use Steam launch options `WINEDLLOVERRIDES="winhttp=n,b" %command%`. Physical Deck verification remains pending.
- **Android Beta:** clean ARM64 Custom Roles Launcher; requires the supported installed Among Us 2026.9.29. Install over the previous launcher and launch the modded game through it. Set the host address using Custom server address. Google Play sign-in is unsupported through the launcher.
- **Windows Testing Setup:** developer build only. Install into a separate copied game folder. Solo testing UI uses F9; normal automatic updates are disabled.

Only the Windows developer build and its source archive include testing tools. Normal Windows, Deck, Android and clean source exclude them. Private QA logs and signing keys are not published. There is no working iOS build. Ask your host privately for the current HTTPS server address.

## Updates

Windows and Steam Deck use `update-manifest.json` from the latest published release. Accept the in-game update prompt, then relaunch. Updates go directly to the newest supported version and download changed mod files only.

Android checks [its matching update feed](https://github.com/Oly132/among-us-custom-roles/releases/tag/android-v0.15.9) for `android-update-manifest.json`. Mod updates require restarting; launcher changes require installing the new APK. Repository edits alone do not trigger updates.

Everyone should update to the same version and create a fresh lobby. Main-menu Server address/Paste lets you change your host's address without reinstalling.

## Source and validation

Root source is the clean Windows mod. Exact clean, developer and Android corresponding-source archives are attached to each release. Build with .NET and your legally owned supported Among Us plus initialized BepInEx IL2CPP references:

```powershell
dotnet build Forger.csproj -c Release -p:GameDir="C:/path/to/Among Us"
```

Windows/Android/developer builds and packaging checks pass. Real server-connected Windows clients verified role assignment and ability buttons, Fat Guy first/second normal attacks, several abilities, Silencer countdown, and normal/neutral victory paths. This is not exhaustive every-map/every-role certification. Physical Steam Deck and the latest Android APK have not been live tested. Intermittent replay-lobby disconnection remains under investigation; create a fresh lobby if returning fails. See release notes for details.

## Download progress in 0.15.9

The update feeds now deliver the progress patch directly to users upgrading from older versions. The full Windows/Steam Deck installers stay unchanged; after a new desktop installation apply the small Windows Progress Patch or Steam Deck Progress Patch from the same release. The Android APK is refreshed (versionCode15, displayed version0.15.9). See [progress patch instructions](https://github.com/Oly132/among-us-custom-roles/releases/download/v0.15.9/PROGRESS-PATCH.md).

Progress and verified completion appear for updates after this patch is installed; an older updater cannot show the new UI during the update that installs it. Exact source is the baseline release archive plus the Progress Source Patch archive. No testing tools are added to normal builds.
