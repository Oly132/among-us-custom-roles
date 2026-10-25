# Update channels

| Label | How it is published | In-game behavior |
| --- | --- | --- |
| 🔴 **MANDATORY — everyone update** | Stable release; Latest on the main feed, stable android-v companion feed; mandatory=true | Checks at launch, downloads only changed files. Since 0.15.10 download starts automatically on the main menu. Older clients may need one Download click. |
| 🟡 **OPTIONAL PREVIEW — manual only** | GitHub prerelease; never Latest; no live update feed; mandatory=false if a sample manifest is provided | Ignored by the in-game updater. Included in normal play only after merging into the next mandatory release. |

Current mandatory release: **0.15.10**. There are no optional previews in this release.

Do not publish an optional preview as a normal stable Latest release. Older PC updaters do not understand the mandatory flag, so GitHub prerelease/Latest separation is required for backward compatibility. Older Android updaters also ignore prereleases. Keep the last mandatory feed reachable until its replacement is fully uploaded and published.

A mandatory update is a supported release distribution channel, not a bypass of offline devices, disabled software, missing updater runtimes, unsupported game builds, or Android's installer consent.
