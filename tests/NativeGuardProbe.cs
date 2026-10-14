using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using BepInEx;
using HarmonyLib;
using UnityEngine;

// Synthetic test world only; not included in the public package.
[BepInPlugin("validation.ashlandsfirefix.probe", "Synthetic Wildfire Probe", "1.1.0")]
[BepInDependency(AshlandsFireGuard.PluginGuid)]
public sealed class NativeGuardProbe : BaseUnityPlugin
{
    private bool started;
    private static readonly int Grave = Hash("Player_tombstone");
    private static readonly int Chest = Hash("piece_chest_wood");
    private static readonly int Campfire = Hash("fire_pit");
    private static readonly int Items = Hash("items");
    private string ExpectedFile { get { return Path.Combine(Paths.ConfigPath, "synthetic-inventory.sha256"); } }
    private Dictionary<ZDOID, ZDO> Objects { get { return (Dictionary<ZDOID, ZDO>)AccessTools.Field(typeof(ZDOMan), "m_objectsByID").GetValue(ZDOMan.instance); } }

    private static int Hash(string text)
    {
        unchecked
        {
            int a = 5381, b = 5381;
            for (int i = 0; i < text.Length; i += 2)
            {
                a = (a * 33) ^ text[i];
                if (i + 1 < text.Length) b = (b * 33) ^ text[i + 1];
            }
            return a + b * 1566083941;
        }
    }
    private void Update()
    {
        if (started || ZNet.instance == null || !ZNet.instance.IsServer() || WorldGenerator.instance == null || ZDOMan.instance == null || ZNetScene.instance == null) return;
        if (ZNet.instance.GetWorldName() != "FireParticleFixSyntheticTest") return;
        if (ZNetScene.instance.GetPrefab("Wood") == null) return;
        started = true;
        StartCoroutine(Run());
    }
    private void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("PUBLIC-FIRE-PROBE FAIL: " + message);
        Logger.LogInfo("PUBLIC-FIRE-PROBE PASS: " + message);
    }
    private static string Digest(byte[] bytes)
    {
        using (SHA256 sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(bytes));
    }
    private ZDO Create(int prefab, Vector3 position)
    {
        ZDO record = ZDOMan.instance.CreateNewZDO(position, prefab);
        record.Persistent = true;
        record.SetPrefab(prefab);
        return record;
    }
    private List<Vector3> Positions()
    {
        var positions = new List<Vector3>();
        for (int z = -151; z <= -120 && positions.Count < 18; z++)
            for (int x = -55; x <= 55 && positions.Count < 18; x++)
            {
                Vector3 p = new Vector3(x * 64, 35, z * 64);
                bool good = true;
                for (int i = 0; i < 8; i++) good &= AshlandsFireGuard.IsTarget(AshlandsFireGuard.FireHash, p + new Vector3(i * 2.2f, 0, 0));
                if (good) positions.Add(p);
            }
        Require(positions.Count == 18, "found 18 native Ashlands sectors in a synthetic world");
        return positions;
    }
    private byte[] InventoryPayload()
    {
        GameObject woodPrefab = ZNetScene.instance.GetPrefab("Wood");
        ItemDrop.ItemData wood = woodPrefab.GetComponent<ItemDrop>().m_itemData.Clone();
        wood.m_dropPrefab = woodPrefab;
        wood.m_stack = 10;
        var inventory = new Inventory("Synthetic fixture", null, 4, 4);
        Require(inventory.AddItem(wood), "native inventory contains ten Wood items");
        var packet = new ZPackage();
        inventory.Save(packet);
        return packet.GetArray();
    }
    private int FireCount()
    {
        int n = 0;
        foreach (ZDO record in Objects.Values)
            if (AshlandsFireGuard.IsTarget(record.GetPrefab(), record.GetPosition())) n++;
        return n;
    }
    private void CheckProtected()
    {
        int graves = 0, chests = 0, campfires = 0, outside = 0;
        string expected = File.ReadAllText(ExpectedFile).Trim();
        foreach (ZDO record in Objects.Values)
        {
            int prefab = record.GetPrefab();
            if (prefab == Grave || prefab == Chest)
            {
                Require(Digest(record.GetByteArray(Items, new byte[0])) == expected, "native grave/container inventory unchanged");
                if (prefab == Grave) graves++; else chests++;
            }
            if (prefab == Campfire) campfires++;
            if (prefab == AshlandsFireGuard.FireHash && !AshlandsFireGuard.IsTarget(prefab, record.GetPosition())) outside++;
        }
        Require(graves == 2 && chests == 1 && campfires == 1 && outside == 1, "two graves, chest, campfire and outside-biome fire preserved");
    }
    private void CheckQuotas(int expectedCount)
    {
        var sectors = new Dictionary<string, int>();
        var cells = new HashSet<string>();
        int n = 0;
        foreach (ZDO record in Objects.Values)
        {
            if (!AshlandsFireGuard.IsTarget(record.GetPrefab(), record.GetPosition())) continue;
            Vector3 p = record.GetPosition();
            string sector = Math.Floor((p.x + 32) / 64) + ":" + Math.Floor((p.z + 32) / 64);
            string cell = Math.Floor(p.x / 2) + ":" + Math.Floor(p.y / 2) + ":" + Math.Floor(p.z / 2);
            int count; sectors.TryGetValue(sector, out count); sectors[sector] = count + 1;
            Require(cells.Add(cell), "no overlapping wildfire cells");
            n++;
        }
        foreach (int count in sectors.Values) Require(count <= 8, "native sector quota <= 8");
        Require(n == expectedCount, "native biome quota count " + n);
    }
    private IEnumerator Run()
    {
        yield return new WaitForSeconds(0.5f);
        bool preseed = Environment.GetEnvironmentVariable("PUBLIC_FIRE_PROBE_MODE") == "preseed";
        try
        {
            List<Vector3> positions = Positions();
            if (preseed)
            {
                byte[] inventory = InventoryPayload();
                File.WriteAllText(ExpectedFile, Digest(inventory));
                foreach (int prefab in new int[] { Grave, Grave, Chest, Campfire })
                {
                    ZDO record = Create(prefab, positions[0] + new Vector3(0, 0, 20 + prefab % 5));
                    if (prefab == Grave || prefab == Chest) record.Set(Items, inventory);
                }
                Create(AshlandsFireGuard.FireHash, new Vector3(0, 35, 8000));
                for (int sector = 0; sector < positions.Count; sector++)
                    for (int i = 0; i < 8; i++)
                    {
                        ZDO record = Create(i % 2 == 0 ? AshlandsFireGuard.FireHash : AshlandsFireGuard.HouseFireHash,
                            positions[sector] + new Vector3(i * 2.2f, 0, 0));
                        record.SetOwner(123456789L);
                    }
                for (int i = 0; i < 4; i++) Create(AshlandsFireGuard.FireHash, positions[0]);
            }
            else
            {
                Require(AshlandsFireGuard.SweepCount >= 1, "world-load cleanup ran for a world without the private recovery name");
                CheckQuotas(128);
                CheckProtected();
            }
        }
        catch (Exception error) { Logger.LogError(error); Application.Quit(); yield break; }
        if (preseed)
        {
            yield return new WaitForSeconds(4);
            try
            {
                Require(FireCount() == 148 && AshlandsFireGuard.SweepCount == 0, "Enabled=false prevents cleanup");
                CheckProtected();
                AccessTools.Method(typeof(ZNet), "SaveWorld", new Type[] { typeof(bool) }).Invoke(ZNet.instance, new object[] { true });
                Logger.LogInfo("PUBLIC-FIRE-PROBE PRESEED PASSED");
            }
            catch (Exception error) { Logger.LogError(error); }
            yield return new WaitForSeconds(2);
            Application.Quit();
            yield break;
        }
        yield return new WaitForSeconds(8);
        try
        {
            Require(FireCount() == 0, "configured age cleanup expires all Ashlands wildfires");
            CheckProtected();
            Vector3 p = Positions()[0];
            ZDO prototype = Create(AshlandsFireGuard.FireHash, p);
            prototype.SetOwner(123456789L);
            var packet = new ZPackage();
            prototype.Serialize(packet);
            ZDO received = Create(0, p);
            received.Deserialize(new ZPackage(packet.GetArray()));
            received.SetOwner(123456789L);
            var tracking = (IDictionary)AccessTools.Field(typeof(AshlandsFireGuard), "tracked").GetValue(null);
            Require(tracking.Contains(received.m_uid), "native Deserialize receives new remotely owned wildfire");
        }
        catch (Exception error) { Logger.LogError(error); Application.Quit(); yield break; }
        yield return new WaitForSeconds(3);
        try { CheckQuotas(1); CheckProtected(); }
        catch (Exception error) { Logger.LogError(error); Application.Quit(); yield break; }
        yield return new WaitForSeconds(8);
        try
        {
            Require(FireCount() == 0, "remotely owned received wildfire destroyed through native server path");
            CheckProtected();
            Logger.LogInfo("PUBLIC-FIRE-PROBE ALL CHECKS PASSED");
        }
        catch (Exception error) { Logger.LogError(error); }
        yield return new WaitForSeconds(2);
        Application.Quit();
    }
}
