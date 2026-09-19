// Versioned local persistence: preferences, records (normal/assisted), totals, applied result IDs, event counters.
// Apps: JSON file written with temp+rename. Web: localStorage via jslib (synchronous, small). No storage → play with a warning.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using ThatDamnFly.Domain;
using UnityEngine;

namespace ThatDamnFly.Simulation
{
    [Serializable] public class Prefs { public bool sound = true; public bool music = true; public bool vibration = true; public bool reducedEffects = false; public bool assist = false; public string language = ""; public bool tutorialSeen = false; public string scene = "kitchen"; public bool toolsRight = false; public bool highContrast = false; }
    [Serializable] public class Records { public int bestTimeMs = -1; public int bestAttacks = -1; public int wins = 0; public int bestFlies = 0; public int fliesTotal = 0; public int bestCombo = 0; }
    [Serializable] public class DailyRecord { public string date; public string scene; public int bestFlies; public int bestCombo; public int fastestMs = -1; public int plays; }
    [Serializable] public class SceneRecords { public string scene; public Records normal = new Records(); public Records assisted = new Records(); }
    /// <summary>ADR-014: fulfilled requests per scene (3-bit mask) and how many times the boss fly fell.</summary>
    [Serializable] public class SceneStars { public string scene; public int mask; public int boss; }
    [Serializable] public class Totals { public int rounds = 0; public int wins = 0; public int timeouts = 0; public int interrupted = 0; public int attacks = 0; }
    [Serializable] public class Counter { public string name; public int count; }
    [Serializable]
    public class SaveData
    {
        public int schemaVersion = 2;
        public string appVersion = "";
        public string rulesVersion = "3";
        public string modelId = "";
        public Prefs prefs = new Prefs();
        public Records normal = new Records();
        public Records assisted = new Records();
        /// <summary>Records of the scenes beyond the kitchen (the kitchen uses normal/assisted, for compatibility with schema 1).</summary>
        public List<SceneRecords> scenes = new List<SceneRecords>();
        /// <summary>Fly of the day: best result per day and scene (last 60 records).</summary>
        public List<DailyRecord> daily = new List<DailyRecord>();
        /// <summary>Stars (requests) per scene - the Day unlocks in a chain (ADR-014).</summary>
        public List<SceneStars> stars = new List<SceneStars>();
        public int daysCompleted = 0;
        /// <summary>ADR-016: collection - catches per fly kind (kind id).</summary>
        public List<Counter> kinds = new List<Counter>();
        public Totals totals = new Totals();
        public List<string> appliedResultIds = new List<string>();
        public List<Counter> counters = new List<Counter>();
        public long lastWriteUtcTicks;
    }

    public interface ISaveStore { bool Available { get; } string Read(); bool Write(string json); void Delete(); string Kind { get; } }

    public sealed class FileSaveStore : ISaveStore
    {
        readonly string _path; public string Kind => "file";
        public FileSaveStore() { _path = Path.Combine(Application.persistentDataPath, "save", "tdf-save.json"); }
        public bool Available { get { try { Directory.CreateDirectory(Path.GetDirectoryName(_path)); return true; } catch { return false; } } }
        public string Read() { try { return File.Exists(_path) ? File.ReadAllText(_path) : null; } catch (Exception e) { Debug.LogWarning("save read: " + e.Message); return null; } }
        public bool Write(string json)
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(_path)); string tmp = _path + ".tmp"; File.WriteAllText(tmp, json); if (File.Exists(_path)) File.Replace(tmp, _path, _path + ".bak"); else File.Move(tmp, _path); return true; }
            catch (Exception e) { Debug.LogWarning("save write: " + e.Message); return false; }
        }
        public void Delete() { try { if (File.Exists(_path)) File.Delete(_path); } catch { } }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    public sealed class WebSaveStore : ISaveStore
    {
        [DllImport("__Internal")] static extern int TDF_StorageAvailable();
        [DllImport("__Internal")] static extern string TDF_StorageRead(string key);
        [DllImport("__Internal")] static extern int TDF_StorageWrite(string key, string value);
        [DllImport("__Internal")] static extern void TDF_StorageDelete(string key);
        const string Key = "tdf-save-v1"; public string Kind => "localStorage";
        public bool Available { get { try { return TDF_StorageAvailable() == 1; } catch { return false; } } }
        public string Read() { try { var s = TDF_StorageRead(Key); return string.IsNullOrEmpty(s) ? null : s; } catch { return null; } }
        public bool Write(string json) { try { return TDF_StorageWrite(Key, json) == 1; } catch { return false; } }
        public void Delete() { try { TDF_StorageDelete(Key); } catch { } }
    }
#endif

    public sealed class SaveService
    {
        public SaveData Data { get; private set; } = new SaveData();
        public bool StorageAvailable { get; private set; }
        public string StoreKind => _store.Kind;
        readonly ISaveStore _store;

        public SaveService()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            _store = new WebSaveStore();
#else
            _store = new FileSaveStore();
#endif
        }

        public void Load(string appVersion, string modelId)
        {
            StorageAvailable = _store.Available;
            Data = new SaveData();
            if (StorageAvailable)
            {
                var json = _store.Read();
                if (!string.IsNullOrEmpty(json))
                {
                    try { var d = JsonUtility.FromJson<SaveData>(json); if (d != null) Data = Migrate(d); }
                    catch (Exception e) { Debug.LogWarning("save parse failed; keeping defaults: " + e.Message); }
                }
            }
            Data.appVersion = appVersion; Data.modelId = modelId;
            if (string.IsNullOrEmpty(Data.prefs.language)) Data.prefs.language = Loc.SystemDefault();
        }

        static SaveData Migrate(SaveData d)
        {
            // Schema 1 is the initial one. Future migrations: N → N+1, preserving preferences and records; never turn unknown into zero.
            if (d.schemaVersion < 1) d.schemaVersion = 1;
            d.prefs ??= new Prefs(); d.normal ??= new Records(); d.assisted ??= new Records(); d.totals ??= new Totals(); d.appliedResultIds ??= new List<string>(); d.counters ??= new List<Counter>(); d.scenes ??= new List<SceneRecords>(); d.daily ??= new List<DailyRecord>(); d.stars ??= new List<SceneStars>(); d.kinds ??= new List<Counter>();
            if (string.IsNullOrEmpty(d.prefs.scene)) d.prefs.scene = "kitchen";
            if (d.schemaVersion < 2)
            {   // 1 → 2 (ADR-014): whoever already had records receives the stars those records already prove (flies in a round, combo) - no scene already played is lost
                foreach (var sc in Scenes.All)
                {
                    int mask = 0;
                    foreach (var r in new[] { RecordsIn(d, sc.Id, false), RecordsIn(d, sc.Id, true) })
                        for (int i = 0; i < sc.Objectives.Length; i++)
                        {
                            var o = sc.Objectives[i];
                            if ((o.Kind == ObjectiveKind.CatchN && r.bestFlies >= o.N) || (o.Kind == ObjectiveKind.ComboN && r.bestCombo >= o.N)) mask |= 1 << i;
                        }
                    if (mask != 0) d.stars.Add(new SceneStars { scene = sc.Id, mask = mask });
                }
                d.schemaVersion = 2;
            }
            return d;
        }

        public bool Save()
        {
            if (!StorageAvailable) return false;
            Data.lastWriteUtcTicks = DateTime.UtcNow.Ticks;
            return _store.Write(JsonUtility.ToJson(Data));
        }

        public int CountOf(string evt) { foreach (var c in Data.counters) if (c.name == evt) return c.count; return 0; }
        public void Count(string evt)
        {
            foreach (var c in Data.counters) if (c.name == evt) { c.count++; return; }
            Data.counters.Add(new Counter { name = evt, count = 1 });
        }

        /// <summary>Applies a result ONCE (idempotent per resultId). Returns (flies record, fastest catch record, combo record).</summary>
        public Records RecordsFor(string sceneId, bool assisted) => RecordsIn(Data, sceneId, assisted);
        static Records RecordsIn(SaveData d, string sceneId, bool assisted)
        {
            if (string.IsNullOrEmpty(sceneId) || sceneId == "kitchen") return assisted ? d.assisted : d.normal;
            foreach (var s in d.scenes) if (s.scene == sceneId) return assisted ? s.assisted : s.normal;
            var n = new SceneRecords { scene = sceneId }; d.scenes.Add(n); return assisted ? n.assisted : n.normal;
        }

        // ---- ADR-014: requests (stars), chain unlock, complete day ----
        public const int StarsPerScene = 3;
        SceneStars StarsEntry(string sceneId, bool create)
        {
            foreach (var s in Data.stars) if (s.scene == sceneId) return s;
            if (!create) return null; var n = new SceneStars { scene = sceneId }; Data.stars.Add(n); return n;
        }
        public int StarMask(string sceneId) => StarsEntry(sceneId, false)?.mask ?? 0;
        public static int CountBits(int mask) => (mask & 1) + ((mask >> 1) & 1) + ((mask >> 2) & 1);
        public int StarsOf(string sceneId) => CountBits(StarMask(sceneId));
        public int DayStars() { int n = 0; foreach (var sc in Scenes.All) n += StarsOf(sc.Id); return n; }
        public int DayStarsMax => Scenes.All.Length * StarsPerScene;
        /// <summary>The first scene is always open; each of the following opens with ≥1 star in the previous one.</summary>
        public bool IsUnlocked(SceneDef sc) { int i = Scenes.IndexOf(sc); return i <= 0 || StarsOf(Scenes.All[i - 1].Id) >= 1; }
        /// <summary>Records the requests fulfilled in a round; returns the mask of the NEW stars.</summary>
        public int ApplyStars(string sceneId, bool[] done, bool bossCaught)
        {
            int mask = 0; for (int i = 0; i < done.Length && i < StarsPerScene; i++) if (done[i]) mask |= 1 << i;
            if (mask == 0 && !bossCaught) return 0;
            var e = StarsEntry(sceneId, true); int fresh = mask & ~e.mask; e.mask |= mask;
            if (bossCaught) { e.boss++; Data.daysCompleted++; }
            return fresh;
        }
        // ---- ADR-016: collection and titles ----
        public void ApplyKinds(int[] counts)
        {
            for (int k = 0; k < counts.Length; k++)
            {
                if (counts[k] == 0) continue; string id = FlyKinds.Of((FlyKind)k).Id; bool found = false;
                foreach (var c in Data.kinds) if (c.name == id) { c.count += counts[k]; found = true; break; }
                if (!found) Data.kinds.Add(new Counter { name = id, count = counts[k] });
            }
        }
        public int KindCount(string id) { foreach (var c in Data.kinds) if (c.name == id) return c.count; return 0; }
        public int FliesTotalAll() { int n = 0; foreach (var sc in Scenes.All) n += RecordsFor(sc.Id, false).fliesTotal + RecordsFor(sc.Id, true).fliesTotal; return n + RecordsFor("face", false).fliesTotal; }   // + face bonus (ADR-019)
        /// <summary>Player title (1…6) by total flies; "owner of the house" once the day is closed.</summary>
        public int TitleTier()
        {
            if (Data.daysCompleted > 0) return 6; int n = FliesTotalAll();
            return n >= 300 ? 5 : (n >= 120 ? 4 : (n >= 40 ? 3 : (n >= 10 ? 2 : 1)));
        }

        /// <summary>Development builds only: opens everything (1 star per scene) or grants all three.</summary>
        public void DevGrantStars(int mask) { foreach (var sc in Scenes.All) StarsEntry(sc.Id, true).mask = mask; }

        public (bool fliesRecord, bool timeRecord, bool comboRecord) ApplyResult(string resultId, int fliesCaught, int fastestCatchMs, bool interrupted, int attacks, bool assisted, int bestCombo = 0, string sceneId = "kitchen")
        {
            if (Data.appliedResultIds.Contains(resultId)) return (false, false, false);
            Data.appliedResultIds.Add(resultId); if (Data.appliedResultIds.Count > 100) Data.appliedResultIds.RemoveAt(0);
            Data.totals.rounds++; Data.totals.attacks += attacks;
            if (interrupted) { Data.totals.interrupted++; return (false, false, false); }
            if (fliesCaught == 0) Data.totals.timeouts++; else Data.totals.wins++;
            var r = RecordsFor(sceneId, assisted); r.fliesTotal += fliesCaught; if (fliesCaught > 0) r.wins++;
            bool fr = false, tr = false, cr = false;
            if (fliesCaught > r.bestFlies) { r.bestFlies = fliesCaught; fr = true; }
            if (fastestCatchMs > 0 && (r.bestTimeMs < 0 || fastestCatchMs < r.bestTimeMs)) { r.bestTimeMs = fastestCatchMs; tr = true; }
            if (bestCombo >= 2 && bestCombo > r.bestCombo) { r.bestCombo = bestCombo; cr = true; }
            return (fr, tr, cr);
        }

        public DailyRecord DailyFor(string dateKey, string sceneId, bool create)
        {
            foreach (var d in Data.daily) if (d.date == dateKey && d.scene == sceneId) return d;
            if (!create) return null;
            var n = new DailyRecord { date = dateKey, scene = sceneId }; Data.daily.Add(n);
            while (Data.daily.Count > 60) Data.daily.RemoveAt(0);
            return n;
        }

        /// <summary>Records a fly-of-the-day round; returns true if it was the best result of the day in that scene.</summary>
        public bool ApplyDaily(string dateKey, string sceneId, int flies, int bestCombo, int fastestMs, bool interrupted)
        {
            var d = DailyFor(dateKey, sceneId, true); d.plays++;
            if (interrupted) return false;
            bool rec = flies > d.bestFlies || (flies == d.bestFlies && flies > 0 && bestCombo > d.bestCombo);
            if (flies > d.bestFlies) d.bestFlies = flies;
            if (bestCombo > d.bestCombo) d.bestCombo = bestCombo;
            if (fastestMs > 0 && (d.fastestMs < 0 || fastestMs < d.fastestMs)) d.fastestMs = fastestMs;
            return rec;
        }

        public void ResetProgress() { var p = Data.prefs; Data = new SaveData { prefs = p, appVersion = Data.appVersion, modelId = Data.modelId }; _store.Delete(); Save(); }

        public string DiagnosticsJson(string extra)
        {
            return "{\"app\":\"" + Data.appVersion + "\",\"platform\":\"" + Application.platform + "\",\"model\":\"" + Data.modelId + "\",\"rules\":\"" + Data.rulesVersion + "\",\"store\":\"" + StoreKind + "\",\"storage\":" + (StorageAvailable ? "true" : "false") +
                   ",\"totals\":" + JsonUtility.ToJson(Data.totals) + ",\"counters\":" + JsonUtility.ToJson(new CounterList { items = Data.counters }) + ",\"screen\":\"" + Screen.width + "x" + Screen.height + "\",\"lang\":\"" + Loc.Lang + "\"" + (string.IsNullOrEmpty(extra) ? "" : "," + extra) + "}";
        }
        [Serializable] class CounterList { public List<Counter> items; }
    }
}
