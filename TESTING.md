# Build and native-game validation

## Build

On Windows with the .NET Framework C# compiler available, supply the installed game's managed assemblies and BepInEx core directory:

```powershell
.\build.ps1 -ManagedPath '<Valheim dedicated server>/valheim_server_Data/Managed' -BepInExCorePath '<BepInEx>/core'
```

Output: `build/AshlandsFireGuard.dll`. Game assemblies, BepInEx and Harmony are reference inputs and are not redistributed in this package.

## Synthetic probe

`tests/NativeGuardProbe.cs` is test source only. No compiled test plugin is shipped. Never add a compiled probe to a normal player/server profile.

The probe runs only in a deliberately named world, `FireParticleFixSyntheticTest`, and creates synthetic fixtures with ten Wood items. It never needs an existing player's saves, logs or inventory.

For native validation, compile it as `NativeGuardProbe.dll`, referencing the release DLL and the same game/BepInEx/Harmony assemblies. Use an isolated BepInEx profile, separate save directory, private server port and `-public 0`.

1. With `PUBLIC_FIRE_PROBE_MODE=preseed`, set `Enabled=false`, `CheckIntervalSeconds=2` and `MaxLifetimeSeconds=6`. Start the synthetic world with seed `fire-fix-validation`. The probe builds 148 overlapping/remotely owned Ashlands wildfire records, two graves, a chest, a campfire and one fire outside Ashlands. It confirms that disabling the guard prevents cleanup, saves the fixture world and quits.
2. Set `PUBLIC_FIRE_PROBE_MODE=verify`, `Enabled=true` and `CleanupOnWorldLoad=true`. Start the same synthetic world. The probe checks world-load cleanup to 128 fires, sector quotas, overlap suppression, inventory preservation, configured age expiry, native network deserialization and native deletion of received remotely owned fires. It quits after validation.

The expected completion lines are `PUBLIC-FIRE-PROBE PRESEED PASSED` and `PUBLIC-FIRE-PROBE ALL CHECKS PASSED`. Any `PUBLIC-FIRE-PROBE FAIL` is a failed run. Fixture files and logs are private test artifacts and should not be added to the distribution archive.

## Release 1.1.0 results

Validated against Valheim 1.0.17 (world format 41), BepInEx 5.4.23.5 / BepInExPack Valheim 5.4.2351 and its bundled Harmony. Both native phases passed. All three synthetic inventories survived reload and every cleanup phase byte-for-byte. The private predecessor also passed native checks with a 60-second timer; observed consecutive intervals were approximately 60.03 seconds.

These are behavioral and integrity checks, not a hardware-independent FPS benchmark. Linux, older game versions and every third-party modpack have not been individually validated.

## Release 1.1.1

This release adds the public GitHub repository and cross-links. The runtime source differs from 1.1.0 only in plugin/assembly version metadata. Cleanup logic and defaults are unchanged. The release DLL is rebuilt and the installable/source archives are validated again.

## Package

After building the DLL, run `python package.py` from the repository. It validates the manifest, icon and archive allowlists, then writes the installable ZIP and a separate source ZIP. Generated archives, build outputs and local validation reports are ignored by Git.
