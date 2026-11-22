# iPadOS port preparation â€” not an installable mod

Status on 22 November 2026: shared rules and a native-call boundary compile and pass host tests. No iPad application, signed IPA, game hook adapter, complete role port or iPad gameplay test exists in this folder. No existing release or updater has been changed.

## Implemented groundwork

`Core/Forger.IPad.Rules.csproj` directly links the existing clean mod's pure rule files; it does not copy or rewrite those rules. The C-callable boundary in `Native/forger_rules.h` exposes bounded contexts, evidence changes, silence/meeting expiry, one ordinary-stab protection, lockpick budgets and Thief's one-use gate. Existing roster/lottery helpers also compile as linked source. This is not a complete implementation of these roles: actual role assignment/transfer, animations, kill/report actions, victory logic, input, authority and networking still depend on the game integration.

The rules library avoids BepInEx's Unity loader, runtime Harmony patching, reflection-driven registration and desktop/Android interop assemblies. It is designed for .NET NativeAOT. Native publication deliberately requires `ios-arm64`, a macOS build host and the Apple SDK. A successful Windows build must never be renamed to a dylib or IPA.

## Checks completed here

```powershell
dotnet run --project ipad/Tests/Tests.csproj -c Release
python3 ipad/check_build_inputs.py
```

The first check calls the exported entry points through unmanaged C calling-convention function pointers on this Windows host. It checks struct sizes/alignment, invalid arguments, stale handles, epoch isolation, forge limits, silence's ten-second expiry, the repeat-silence restriction, Fat Guy's first/second ordinary stab, door cooldown and Thief's one-use gate. It does not establish that an iPad binary links or works in Among Us.

The preflight reads a caller-provided compatible local iOS `.app` only when requested. It inspects the device framework and metadata, records fingerprints and refuses encrypted/incorrect inputs. It neither downloads nor decrypts a game, and never claims an unvalidated build matches our game bindings. No native addresses are copied from PC or Android.

## Remaining gates

1. Identify the target iPad model, iPadOS version and installed Among Us version.
2. Obtain a suitable legitimate local iOS game build for developing matching runtime bindings. No game binaries/assets or signing material should be published.
3. Build the full native game adapter, statically registered patch dispatch, object/component/delegate/coroutine bridges, native role registration, touch UI, chat silence and the exact existing PC/Android RPC formats. All roles and interactions remain required.
4. Compile on an Apple build host, package and sign with an appropriate Apple identity/provisioning method, then test on the physical iPad against PC/Android clients through Impostor.

A local Mac is convenient; a properly configured hosted Mac can also be a build host. Neither replaces device testing or valid signing. No IPA or easy installer is available yet.

## Research references

- [Microsoft NativeAOT for iOS-like platforms](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/ios-like-platforms/)
- [Apple code-signing model](https://support.apple.com/guide/security/app-code-signing-process-sec7c917bf14/web)
- [Experimental EHR native iOS port](https://github.com/adcomer0/ehr-ios), inspected locally at the revision recorded in `research.json`. Its binding/adapter pipeline is specific to EHR and its pinned game fingerprint; it is not a drop-in Forger loader. No third-party port code has been incorporated into our core.
- [BepInEx compatibility](https://github.com/BepInEx/BepInEx): our current Unity IL2CPP loader is not an iPadOS loader.

BetterCrewLink and the private Spotify connector are not part of this preparation. No private keys, host URLs or test controls are included.

## Hosted Mac compiler check

The isolated `iPad native rules research` workflow uses a standard ARM64 macOS runner and .NET 10. It first runs shared-rule checks, then compiles a native macOS library and calls it from real C++ code, then cross-compiles the iOS ARM64 library and verifies its Mach-O platform and exported symbols. Its research artifact is explicitly NOT an app or installer. It contains no game content, certificates or provisioning profiles and does not change any release/update feed. Copied shared inputs are pinned after LF line-ending normalization.
