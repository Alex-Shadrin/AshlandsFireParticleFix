# Ashlands Fire & Particle Fix

[Download on Thunderstore](https://thunderstore.io/c/valheim/p/AlexShadrin/Ashlands_Fire_Particle_Fix/) · [Source on GitHub](https://github.com/Alex-Shadrin/AshlandsFireParticleFix) · [Report an issue](https://github.com/Alex-Shadrin/AshlandsFireParticleFix/issues)

Server-side protection against **accumulated Ashlands fires and the fire/smoke effects they spawn**.

Excessive `Fire` and `HouseFire` network objects can make an Ashlands area increasingly expensive to simulate and render. This mod retires overlapping, excessive and long-lived natural fires using Valheim's normal network destruction mechanism.

Natural wildfires are deliberately limited and extinguished. The limits are configurable; consider that gameplay tradeoff before enabling the mod on an existing world.

## Installation

Install on the **dedicated server or the hosting player**. Other players do not need the mod. It also runs on a single-player host. A remote client running it on its own cannot clean the server's world.

Use r2modman/Thunderstore Mod Manager, or copy `BepInEx/plugins/AshlandsFireParticleFix/AshlandsFireGuard.dll` into the host's BepInEx plugins directory. BepInExPack Valheim is the only package dependency; no Jotunn or ServerSync is required.

If upgrading from the manually installed **Ashlands Fire Guard 1.0.x**, remove that old plugin DLL while the host is stopped before installing this package. The public build retains the old plugin GUID for upgrade compatibility.

## Default behavior

| Setting | Default | Meaning |
| --- | ---: | --- |
| Cleanup interval | 60 seconds | Timer-based pass; no per-frame world scan |
| Fires per sector | 8 | Retained `Fire`/`HouseFire` objects in each 64×64 m sector |
| Fires across Ashlands | 128 | Retained objects across the whole Ashlands biome, shared by all players |
| Lifetime threshold | 60 seconds | Age since server tracking began; resets when the world loads |
| Overlap cell | 2×2×2 m | At most one retained wildfire per cell |
| Cleanup on world load | Enabled | One quota/overlap pass after loading saved fires |
| Allowed world names | Empty | Any world name |

**Limits are enforced during passes. Counts can exceed them between passes.** With the default interval, age-based removal normally happens at about 60–120 seconds. Saved fires begin with age zero; the optional startup pass still applies quota and overlap limits immediately.

The host tracks only exact native `Fire` and `HouseFire` prefab IDs in the Ashlands biome. Tombstones and their inventories, dropped items, chests, buildings, creatures, and ordinary fireplaces such as `fire_pit` are outside that filter.

## Configuration

The first launch creates `BepInEx/config/local.sadge.ashlandsfireguard.cfg` on the host. Stop the host, edit the settings, and restart it.

```ini
[General]
Enabled = true
LogCleanupSummary = true
# Empty allows every world; otherwise use comma-separated, case-sensitive names.
AllowedWorldNames =

[Cleanup]
CheckIntervalSeconds = 60
MaxLifetimeSeconds = 60
CleanupOnWorldLoad = true

[Limits]
MaxFiresPerSector = 8
MaxFiresInAshlands = 128
SuppressOverlappingFires = true
CellSizeMeters = 2
```

Set `MaxLifetimeSeconds = 0` to disable age-based removal while retaining quota/overlap limits. Settings are local to the host; clients do not configure them.

## Scope and limitations

This **changes natural wildfire persistence and spread** in Ashlands. Use it when that tradeoff is appropriate for your server. Back up an existing world before enabling cleanup: fires removed by the mod stay removed when the world is saved. Disabling the plugin does not reconstruct them.

The mod reduces fire/smoke effects by removing their accumulated wildfire objects. It does not directly patch Unity particle rendering, change graphics quality, remove arbitrary VFX prefabs, or guarantee a particular FPS. Other causes of poor performance may require separate diagnosis.

The host keeps tracking saved Ashlands fires even when players are elsewhere. It scans all loaded ZDO records once at world load, then visits only its tracked wildfire list at each interval. Ordinary network updates return immediately after a prefab check. An empty timer pass returns without allocating temporary collections.

The current release targets **Valheim 1.0.17 / world save format 41** and BepInExPack Valheim 5.4.2351. It uses native ZDO methods and may require adaptation after game/API changes. Mods that replace these wildfire prefabs or alter biome lookup/ownership can affect compatibility. Existing offline world-file repair scripts are not included or executed.

## Testing and source

Source code, build instructions and a synthetic native-game test probe are available in the [GitHub repository](https://github.com/Alex-Shadrin/AshlandsFireParticleFix). See [TESTING.md](https://github.com/Alex-Shadrin/AshlandsFireParticleFix/blob/main/TESTING.md) for build and validation instructions. Installable ZIPs are available from [Thunderstore](https://thunderstore.io/c/valheim/p/AlexShadrin/Ashlands_Fire_Particle_Fix/); the separate source archive is available from [GitHub Releases](https://github.com/Alex-Shadrin/AshlandsFireParticleFix/releases). The installable package contains no test plugin or development files. The cleanup path is tested using Valheim's native prefab IDs, network serialization and server destruction calls, including tombstone/container inventory preservation and objects owned by another peer. Tests exercise configurable 2-second passes and a 6-second age threshold for speed; the private predecessor also underwent a native 60-second timer test. Such tests validate cleanup behavior; they do not constitute an FPS benchmark for every modpack or hardware configuration.

This mod was developed with AI assistance. Its Thunderstore listing should use the **AI Generated** category. Code is provided under the MIT license.

## Кратко по-русски

Мод для сервера/хоста ограничивает накопление пожаров и связанных эффектов огня и дыма в Пепельных землях. По умолчанию проверка идёт раз в минуту: до 8 пожаров на сектор и 128 на весь биом, с порогом возраста 60 секунд. Лимиты применяются при проверке, поэтому между проверками огня может быть больше. Могилки, лут, постройки и обычные костры в фильтр очистки не входят. Перед установкой сохраните резервную копию мира.
