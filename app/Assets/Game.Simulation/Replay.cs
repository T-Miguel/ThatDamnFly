// Replay (ADR-007): the simulation is deterministic given the seed and the sequence (tick, tool, target) of confirmations.
// Recording this is enough to reproduce the round; the replay shows the window around the best catch.
using System.Collections.Generic;
using ThatDamnFly.Domain;

namespace ThatDamnFly.Simulation
{
    public sealed class ReplayInput { public int Tick; public string ToolId; public Vec2 Target; }

    public sealed class ReplayData
    {
        public ulong Seed; public string SceneId; public bool Assisted; public string DailyKey; public int LevelOffset;
        public readonly List<ReplayInput> Inputs = new List<ReplayInput>();
        public int BestCatchTick = -1, BestCatchCombo;
        public int WindowStartTick => System.Math.Max(0, BestCatchTick - 400);   // 4 s before
        public int WindowEndTick => BestCatchTick + 120;                          // 1.2 s after
        public bool HasCatch => BestCatchTick >= 0;
    }
}
