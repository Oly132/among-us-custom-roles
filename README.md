# Among Us Custom Roles

Every player needs the mod. Eleven custom roles: Forger, Puppeteer, Faker, Silencer, Cannibal, The Fat Guy, Dog Owner, Terrorist, Binocular, Evil Engineer and Thief. [Role guide](ROLES.md).

## Downloads

Get matching installers from [the latest release](https://github.com/Oly132/among-us-custom-roles/releases/latest). Version 0.15.3 includes changes to role allocation, assignment sync, native intro timing, Cannibal/Thief buttons, name privacy and camera cleanup. These fixes still require live multiplayer confirmation.

- **Windows Setup:** recommended normal installer, no testing UI. Install over the existing game folder with Among Us and Silencer CrewLink closed. Launch through Steam.
- **Windows Testing Setup:** separate developer build with testing UI. Use a copied game folder; normal automatic updates are disabled.
- **Steam Deck Experimental:** extract in Desktop Mode and run install.sh. Keep Steam launch options `WINEDLLOVERRIDES="winhttp=n,b" %command%`. Hardware/controller/voice validation remains pending.
- **Android Beta:** supported Among Us 2026.9.29 ARM64 only. Install over Custom Roles Launcher, set the host's private HTTPS address using the launcher button, then launch Among Us through the launcher. Android startup and joining a PC lobby have been confirmed; full role interactions and outgoing voice silence remain unverified. Google Play account login is unsupported through the launcher.

Only Windows Testing contains testing controls. There is no iOS build. Obtain the current server address privately from the host; no private endpoint is published here.

## Updating

Windows checks published releases for update-manifest.json and offers an update. It downloads changed files by SHA-256 and preserves settings. Steam Deck updater execution remains experimental.

Android checks the separate [android-v0.15.3 update channel](https://github.com/Oly132/among-us-custom-roles/releases/tag/android-v0.15.3) for android-update-manifest.json. Mod DLL updates require a restart; launcher changes require installing the new APK. Repository edits alone do not trigger updates.

## Source and validation

Use the exact Windows clean and Android corresponding-source archives attached to each release. The root main-branch source and GitHub automatic source archives may lag the packaged build. Build with .NET SDK and a legally owned supported game plus initialized BepInEx IL2CPP references:

```powershell
dotnet build Forger.csproj -c Release -p:GameDir="C:/path/to/Among Us"
```

Builds, installer packaging, APK signatures and allocation tests have passed. Release notes distinguish those checks from live gameplay. Stale disconnected-player cards and complete death/ghost synchronization are not claimed fixed. No game APKs, signing keys, private player logs or personal server configuration are supplied.
