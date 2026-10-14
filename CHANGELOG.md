# Changelog

## 1.1.1

- Publish source code, build instructions and the synthetic test probe on GitHub.
- Link the Thunderstore website field and README to the repository and issue tracker.
- No cleanup behavior or default configuration changes from 1.1.0.

## 1.1.0

- First public release, based on the privately tested Ashlands Fire Guard.
- Works with any world name; optional host-side world whitelist.
- Configurable interval, per-sector/biome quotas, lifetime and overlap suppression.
- Default cleanup interval remains 60 seconds, without a per-frame update hook.
- Optional quota/overlap cleanup when loading saved wildfire records.
- Exact native wildfire prefab filtering and authoritative network removal.
- MIT license; source/build/test archive distributed separately from the installable package.
