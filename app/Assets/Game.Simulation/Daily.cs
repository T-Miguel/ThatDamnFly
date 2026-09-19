// Fly of the day (ADR-006): the simulation is deterministic, so seed = f(date, scene) gives everyone the same fly on that day.
// Local date of the device (the player expects "today"); the key is YYYY-MM-DD.
using System;

namespace ThatDamnFly.Simulation
{
    public static class Daily
    {
        public static string Key(DateTime date) => date.ToString("yyyy-MM-dd");
        public static string TodayKey() => Key(DateTime.Now);
        public static string Pretty(string key) { if (key == null || key.Length < 10) return key ?? ""; return key.Substring(8, 2) + "/" + key.Substring(5, 2); }

        /// <summary>64-bit FNV-1a over "tdf-daily|date|scene" - stable across platforms and versions.</summary>
        public static ulong Seed(string dateKey, string sceneId)
        {
            ulong h = 14695981039346656037UL;
            foreach (char c in "tdf-daily|" + dateKey + "|" + sceneId) { h ^= (byte)c; h *= 1099511628211UL; }
            return h == 0 ? 1UL : h;
        }
    }
}
