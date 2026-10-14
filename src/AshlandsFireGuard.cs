using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

[assembly: AssemblyVersion("1.1.1.0")]
[assembly: AssemblyFileVersion("1.1.1.0")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("NativeGuardProbe")]

// Keep the GUID used by the private recovery build so BepInEx recognizes upgrades.
[BepInPlugin(PluginGuid, "Ashlands Fire & Particle Fix", "1.1.1")]
public sealed class AshlandsFireGuard : BaseUnityPlugin
{
    public const string PluginGuid = "local.sadge.ashlandsfireguard";
    internal const int HouseFireHash = 1730843282;
    internal const int FireHash = -837447586;
    internal static int SweepCount;
    private static AshlandsFireGuard current;
    private static ManualLogSource log;
    private Harmony patches;
    private ConfigEntry<bool> guardEnabled;
    private ConfigEntry<int> interval;
    private ConfigEntry<int> perSector;
    private ConfigEntry<int> total;
    private ConfigEntry<int> lifetime;
    private ConfigEntry<bool> deduplicate;
    private ConfigEntry<int> cellSize;
    private ConfigEntry<bool> startupCleanup;
    private ConfigEntry<bool> logSummary;
    private HashSet<string> allowedWorlds;
    private static long sequence;
    private static readonly Dictionary<ZDOID, Entry> tracked = new Dictionary<ZDOID, Entry>();

    private sealed class Entry
    {
        public ZDO Record;
        public ZDOID Id;
        public float Created;
        public long Sequence;
        public bool PendingDelete;
    }

    private void Awake()
    {
        current = this;
        log = Logger;
        guardEnabled = Config.Bind("General", "Enabled", true, "Enable the server-side wildfire guard. Restart the host after changing settings.");
        interval = Config.Bind("Cleanup", "CheckIntervalSeconds", 60,
            new ConfigDescription("Seconds between cleanup passes. Counts can exceed limits between passes.", new AcceptableValueRange<int>(1, 3600)));
        perSector = Config.Bind("Limits", "MaxFiresPerSector", 8,
            new ConfigDescription("Maximum retained Fire/HouseFire objects per 64x64m sector at each pass.", new AcceptableValueRange<int>(1, 10000)));
        total = Config.Bind("Limits", "MaxFiresInAshlands", 128,
            new ConfigDescription("Maximum retained Fire/HouseFire objects across the entire Ashlands biome at each pass.", new AcceptableValueRange<int>(1, 100000)));
        lifetime = Config.Bind("Cleanup", "MaxLifetimeSeconds", 60,
            new ConfigDescription("Remove tracked fires at or beyond this age. Zero disables age-based removal. Actual removal may wait one cleanup interval; age resets on world load.", new AcceptableValueRange<int>(0, 86400)));
        deduplicate = Config.Bind("Limits", "SuppressOverlappingFires", true, "Retain at most one wildfire in each spatial cell.");
        cellSize = Config.Bind("Limits", "CellSizeMeters", 2,
            new ConfigDescription("Width, height and depth of the deduplication cells.", new AcceptableValueRange<int>(1, 16)));
        startupCleanup = Config.Bind("Cleanup", "CleanupOnWorldLoad", true,
            "Run a quota/overlap pass once after loading the saved world. Saved fires start with age zero; the lifetime rule still waits for the configured age.");
        logSummary = Config.Bind("General", "LogCleanupSummary", true, "Log one aggregate line when a pass removes fires.");
        string names = Config.Bind("General", "AllowedWorldNames", "",
            "Optional comma-separated world names, case-sensitive. Empty means all worlds. Restart the host after changing this setting.").Value;
        allowedWorlds = new HashSet<string>(StringComparer.Ordinal);
        foreach (string part in names.Split(','))
            if (!String.IsNullOrWhiteSpace(part)) allowedWorlds.Add(part.Trim());
        patches = new Harmony(PluginGuid);
        patches.PatchAll(typeof(AshlandsFireGuard).Assembly);
        Schedule();
        Logger.LogInfo(guardEnabled.Value
            ? "Wildfire guard ready: Ashlands only; cleanup interval " + interval.Value + "s; limits " + perSector.Value + "/sector and " + total.Value + "/biome; age threshold " + lifetime.Value + "s."
            : "Wildfire guard disabled by configuration.");
    }

    private void Schedule()
    {
        CancelInvoke("PeriodicCheck");
        if (guardEnabled.Value) InvokeRepeating("PeriodicCheck", interval.Value, interval.Value);
    }

    private void OnDestroy()
    {
        CancelInvoke("PeriodicCheck");
        if (patches != null) patches.UnpatchSelf();
        tracked.Clear();
        if (System.Object.ReferenceEquals(current, this)) current = null;
    }

    private static bool ScopeAllowsServer()
    {
        if (current == null || !current.guardEnabled.Value) return false;
        ZNet net = ZNet.instance;
        if (net == null || !net.IsServer()) return false;
        return current.allowedWorlds.Count == 0 ||
            (net.GetWorld() != null && current.allowedWorlds.Contains(net.GetWorldName()));
    }

    internal static bool IsTarget(int prefab, Vector3 position)
    {
        // Exact native prefab IDs exclude tombstones, containers, fireplaces,
        // structures, creatures and arbitrary particle-system prefabs.
        if (prefab != HouseFireHash && prefab != FireHash) return false;
        if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z) ||
            float.IsInfinity(position.x) || float.IsInfinity(position.y) || float.IsInfinity(position.z)) return false;
        return WorldGenerator.instance != null &&
            WorldGenerator.instance.GetBiome(position) == Heightmap.Biome.AshLands;
    }

    internal static void Track(ZDO record)
    {
        if (record == null) return;
        int prefab = record.GetPrefab();
        // Ordinary network objects do not query configuration, world or biome.
        if (prefab != HouseFireHash && prefab != FireHash) return;
        if (!ScopeAllowsServer() || !IsTarget(prefab, record.GetPosition())) return;
        Entry old;
        if (tracked.TryGetValue(record.m_uid, out old) && System.Object.ReferenceEquals(old.Record, record)) return;
        tracked[record.m_uid] = new Entry
        {
            Record = record, Id = record.m_uid, Created = Time.realtimeSinceStartup,
            Sequence = ++sequence
        };
    }

    internal static void Check(ZDOMan manager)
    {
        if (tracked.Count == 0 || !ScopeAllowsServer()) return;
        float now = Time.realtimeSinceStartup;
        SweepCount++;
        var live = new List<Entry>();
        var stale = new List<ZDOID>();
        foreach (var pair in tracked)
        {
            Entry entry = pair.Value;
            if (!System.Object.ReferenceEquals(manager.GetZDO(entry.Id), entry.Record) ||
                !IsTarget(entry.Record.GetPrefab(), entry.Record.GetPosition()))
                stale.Add(pair.Key);
            else if (!entry.PendingDelete)
                live.Add(entry);
        }
        foreach (ZDOID id in stale) tracked.Remove(id);
        // Stable arrival order favors newer ignitions, including same-frame spawns.
        live.Sort(delegate(Entry a, Entry b) { return b.Sequence.CompareTo(a.Sequence); });
        var sectors = new Dictionary<string, int>();
        var cells = new HashSet<string>();
        int kept = 0, removed = 0;
        foreach (Entry entry in live)
        {
            Vector3 p = entry.Record.GetPosition();
            string sector = Math.Floor((p.x + 32) / 64) + ":" + Math.Floor((p.z + 32) / 64);
            string cell = Math.Floor(p.x / current.cellSize.Value) + ":" +
                Math.Floor(p.y / current.cellSize.Value) + ":" + Math.Floor(p.z / current.cellSize.Value);
            int sectorCount;
            sectors.TryGetValue(sector, out sectorCount);
            bool aged = current.lifetime.Value > 0 && now - entry.Created >= current.lifetime.Value;
            bool overlap = current.deduplicate.Value && cells.Contains(cell);
            bool remove = aged || kept >= current.total.Value || sectorCount >= current.perSector.Value || overlap;
            if (remove)
            {
                // Native destruction requires ownership. Claim only the exact
                // validated wildfire record, then enqueue normal network removal.
                entry.Record.SetOwner(ZDOMan.GetSessionID());
                manager.DestroyZDO(entry.Record);
                entry.PendingDelete = true;
                removed++;
            }
            else
            {
                kept++;
                sectors[sector] = sectorCount + 1;
                cells.Add(cell);
            }
        }
        if (removed > 0 && current.logSummary.Value)
            log.LogInfo("Ashlands wildfire cleanup: removed " + removed + "; retained " + kept + ".");
    }

    private void PeriodicCheck()
    {
        if (tracked.Count == 0) return;
        ZDOMan manager = ZDOMan.instance;
        if (manager != null) Check(manager);
    }

    [HarmonyPatch(typeof(ZDO), "SetPrefab")]
    private static class PrefabPatch
    {
        private static void Postfix(ZDO __instance) { Track(__instance); }
    }

    [HarmonyPatch(typeof(ZDO), "Deserialize")]
    private static class ReceivePatch
    {
        private static void Postfix(ZDO __instance) { Track(__instance); }
    }

    [HarmonyPatch(typeof(ZDOMan), "LoadChunks")]
    private static class LoadPatch
    {
        private static void Postfix(ZDOMan __instance, Dictionary<ZDOID, ZDO> ___m_objectsByID)
        {
            tracked.Clear();
            SweepCount = 0;
            sequence = 0;
            if (!ScopeAllowsServer()) return;
            // One scan when loading, never a recurring full-world scan.
            foreach (ZDO record in ___m_objectsByID.Values)
                if (record.GetPrefab() == HouseFireHash || record.GetPrefab() == FireHash) Track(record);
            log.LogInfo("Saved Ashlands wildfires tracked: " + tracked.Count);
            if (current.startupCleanup.Value) Check(__instance);
            current.Schedule();
        }
    }
}
