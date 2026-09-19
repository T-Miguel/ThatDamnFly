// Invariants that affect fairness and data (AC03–AC06). They do not mirror the implementation.
using System.Linq;
using ThatDamnFly.Domain;
using Xunit;

public class RoundMachineTests
{
    static RoundMachine NewReady(bool successive = false) { var m = new RoundMachine(new RulesConfig { SuccessiveFlies = successive }); m.MarkReady(); return m; }
    static void Run(RoundMachine m, int ticks, Vec2 fly) { for (int i = 0; i < ticks; i++) m.Tick(fly, fly); }
    static readonly Vec2 Far = new Vec2(0.1f, 1.25f);     // target far from the fly
    static readonly Vec2 FlyAt = new Vec2(0.5f, 0.6f);

    [Fact]
    public void Timer_starts_on_first_confirmed_attack_or_after_10s_ready()
    {
        var m = NewReady(); var fly = new Vec2(0.9f, 0.1f);
        Run(m, 999, fly); Assert.Equal(RoundState.Ready, m.State); Assert.Equal(0, m.ActiveMs);
        Run(m, 1, fly); Assert.Equal(RoundState.Playing, m.State);
        var m2 = NewReady(); Assert.Equal(ConfirmError.None, m2.TryConfirm(KitchenTools.Cloth, Far, out _)); Assert.Equal(RoundState.Playing, m2.State);
    }

    [Fact]
    public void Contact_on_active_phase_wins_and_is_single_terminal()
    {
        var m = NewReady();
        Assert.Equal(ConfirmError.None, m.TryConfirm(KitchenTools.Cloth, FlyAt, out var a));
        Run(m, 30, FlyAt); // 140 ms prep + 80 ms active
        Assert.Equal(RoundState.Won, m.State); Assert.True(a.Lethal); Assert.Equal(a.Id, m.Outcome.LethalAttackId);
        int completed = m.Events.Count(e => e.Type == "round_completed");
        Run(m, 50, FlyAt); Assert.Equal(completed, m.Events.Count(e => e.Type == "round_completed")); Assert.Equal(RoundState.Won, m.State);
    }

    [Fact]
    public void Timeout_at_60000_and_late_contact_does_not_win()
    {
        var m = NewReady(); var fly = new Vec2(0.9f, 0.1f);
        Assert.Equal(ConfirmError.None, m.TryConfirm(KitchenTools.Cloth, Far, out _)); // starts the clock
        Run(m, 5990, fly); Assert.Equal(RoundState.Playing, m.State); Assert.Equal(59900, m.ActiveMs);
        // cloth confirmed at 59,900 ms: active only from 60,040 ms → after the limit → cancelled
        Assert.Equal(ConfirmError.None, m.TryConfirm(KitchenTools.Cloth, fly, out var late));
        Run(m, 30, fly);
        Assert.Equal(RoundState.TimedOut, m.State); Assert.False(late.Lethal); Assert.Equal(60000, m.ActiveMs);
    }

    [Fact]
    public void Contact_exactly_at_limit_wins_over_timeout()
    {
        var m = NewReady(); var fly = new Vec2(0.9f, 0.1f);
        m.TryConfirm(KitchenTools.Cloth, Far, out _);
        Run(m, 5985, fly); // 59,850 ms; a cloth confirmed now is active in [59,990, 60,070]
        Assert.Equal(ConfirmError.None, m.TryConfirm(KitchenTools.Cloth, fly, out var a));
        Run(m, 20, fly);
        Assert.Equal(RoundState.Won, m.State); Assert.True(a.LethalAtActiveMs <= 60000);
    }

    [Fact]
    public void Fury_thresholds_and_gains()
    {
        var m = NewReady();
        Assert.Equal(ConfirmError.FuryTooLow, m.TryConfirm(KitchenTools.Pan, FlyAt, out _));
        Assert.Equal(ConfirmError.FuryTooLow, m.TryConfirm(KitchenTools.Fridge, FlyAt, out _));
        var near = new Vec2(0.5f + 0.07f + 0.02f + 0.10f, 0.6f); // edge of the cloth 0.10 W from the edge of the body → +10
        Assert.Equal(ConfirmError.None, m.TryConfirm(KitchenTools.Cloth, near, out _)); Run(m, 30, FlyAt); Assert.Equal(10, m.Fury);
        Run(m, 200, FlyAt); // > 2 s until a new event
        var close = new Vec2(0.5f + 0.07f + 0.02f + 0.02f, 0.6f); // 0.02 W: "near miss" → +15
        Assert.Equal(ConfirmError.None, m.TryConfirm(KitchenTools.Cloth, close, out _)); Run(m, 30, FlyAt); Assert.Equal(25, m.Fury);
        Assert.True(m.IsEligible(KitchenTools.Pan)); Assert.False(m.IsEligible(KitchenTools.Fridge));
    }

    [Fact]
    public void Miss_far_away_gives_no_fury_and_one_miss_event_per_2s()
    {
        var m = NewReady();
        Assert.Equal(ConfirmError.None, m.TryConfirm(KitchenTools.Cloth, Far, out _)); Run(m, 30, FlyAt); Assert.Equal(0, m.Fury);
        var near = new Vec2(0.5f + 0.07f + 0.02f + 0.10f, 0.6f);
        Run(m, 60, FlyAt); Assert.Equal(ConfirmError.None, m.TryConfirm(KitchenTools.Cloth, near, out _)); Run(m, 30, FlyAt); Assert.Equal(10, m.Fury);
        Run(m, 60, FlyAt); Assert.Equal(ConfirmError.None, m.TryConfirm(KitchenTools.Cloth, near, out _)); Run(m, 30, FlyAt); Assert.Equal(10, m.Fury); // < 2 s since the last event
    }

    [Fact]
    public void Activity_bonus_only_with_valid_attack_in_previous_5s()
    {
        var m = NewReady(); var fly = new Vec2(0.9f, 0.1f);
        m.TryConfirm(KitchenTools.Cloth, Far, out _); Run(m, 500, fly); Assert.Equal(5, m.Fury);  // 5 s boundary with an attack in the previous 5 s
        Run(m, 500, fly); Assert.Equal(5, m.Fury);                                                  // no attack in [5,10) → no bonus
    }

    [Fact]
    public void Global_interval_single_action_and_cooldown()
    {
        var m = NewReady(); var fly = new Vec2(0.9f, 0.1f);
        Assert.Equal(ConfirmError.None, m.TryConfirm(KitchenTools.Cloth, Far, out _));
        Assert.Equal(ConfirmError.GlobalInterval, m.TryConfirm(KitchenTools.Cloth, Far, out _));
        Run(m, 40, fly); // 400 ms: the active phase ended at 220 ms; cooldown until 770 ms
        Assert.Equal(ConfirmError.OnCooldown, m.TryConfirm(KitchenTools.Cloth, Far, out _));
        Run(m, 40, fly); Assert.Equal(ConfirmError.None, m.TryConfirm(KitchenTools.Cloth, Far, out _));
    }

    [Fact]
    public void Invalid_confirm_does_not_change_state()
    {
        var m = NewReady(); var before = (m.Fury, m.AttackCount, m.State);
        Assert.Equal(ConfirmError.OutsideArena, m.TryConfirm(KitchenTools.Cloth, new Vec2(1.5f, 0.5f), out _));
        Assert.Equal(before, (m.Fury, m.AttackCount, m.State));
    }

    [Fact]
    public void Pause_freezes_clocks_and_attacks()
    {
        var m = NewReady();
        m.TryConfirm(KitchenTools.Cloth, Far, out var a); Run(m, 5, FlyAt); m.Pause();
        int active = m.ActiveMs, sim = m.SimMs; Run(m, 500, FlyAt);
        Assert.Equal(active, m.ActiveMs); Assert.Equal(sim, m.SimMs); Assert.Equal(AttackPhase.Preparing, a.Phase);
        m.Resume(); Run(m, 30, FlyAt); Assert.Equal(AttackPhase.Done, a.Phase);
    }

    [Fact]
    public void Assist_scales_area_by_25_percent()
    {
        var m = NewReady(); m.Assisted = true;
        var target = new Vec2(0.5f + 0.07f * 1.25f + 0.02f - 0.005f, 0.6f); // only touches with assistance
        m.TryConfirm(KitchenTools.Cloth, target, out _); Run(m, 30, FlyAt); Assert.Equal(RoundState.Won, m.State);
        var n = NewReady(); n.TryConfirm(KitchenTools.Cloth, target, out _); Run(n, 30, FlyAt); Assert.NotEqual(RoundState.Won, n.State);
    }

    // ---- rulesVersion 2: successive flies
    [Fact]
    public void Successive_catch_does_not_end_round_and_next_fly_enters_after_delay()
    {
        var m = NewReady(true); int spawned = 0, caught = 0; m.FlySpawned += _ => spawned++; m.FlyCaught += (_, _, _) => caught++;
        Assert.Equal(ConfirmError.None, m.TryConfirm(KitchenTools.Cloth, FlyAt, out var a));
        Run(m, 30, FlyAt);
        Assert.Equal(RoundState.Playing, m.State); Assert.Equal(1, m.FliesCaught); Assert.Equal(1, caught); Assert.False(m.FlyActive); Assert.True(a.Lethal);
        Run(m, 40, FlyAt); Assert.False(m.FlyActive);   // 400 ms < 700 ms
        Run(m, 40, FlyAt); Assert.True(m.FlyActive); Assert.Equal(1, spawned);
    }

    [Fact]
    public void Successive_no_contact_while_no_fly_and_fury_persists()
    {
        var m = new RoundMachine(new RulesConfig { SuccessiveFlies = true, RespawnDelayMs = 1500 }); m.MarkReady();
        m.TryConfirm(KitchenTools.Cloth, FlyAt, out _); Run(m, 30, FlyAt); Assert.Equal(1, m.FliesCaught);   // catch at ~150 ms; next fly at 1650 ms
        Run(m, 50, FlyAt); Assert.False(m.FlyActive);
        Assert.Equal(ConfirmError.None, m.TryConfirm(KitchenTools.Cloth, FlyAt, out var b)); Run(m, 30, FlyAt);  // active in [940, 1020] with no fly
        Assert.Equal(1, m.FliesCaught); Assert.False(b.Lethal); Assert.Equal(0, m.Fury);   // no fly: neither catch nor fury
        Run(m, 80, FlyAt); Assert.True(m.FlyActive);
        Run(m, 100, FlyAt);
        var near = new Vec2(0.5f + 0.07f + 0.02f + 0.10f, 0.6f); Assert.Equal(ConfirmError.None, m.TryConfirm(KitchenTools.Cloth, near, out _)); Run(m, 30, FlyAt);
        Assert.Equal(10, m.Fury);   // fury accumulates over the round with the new fly
    }

    [Fact]
    public void Successive_round_ends_at_end_time_with_count_and_fastest_catch()
    {
        var m = new RoundMachine(new RulesConfig { SuccessiveFlies = true, ComboTimeBonusMs = 0, FruitFlyFromIndex = 99, BluebottleFromIndex = 99 }); m.MarkReady();
        m.TryConfirm(KitchenTools.Cloth, FlyAt, out _); Run(m, 30, FlyAt);           // catch at ~150 ms → +2 s
        Run(m, 100, FlyAt); m.TryConfirm(KitchenTools.Cloth, FlyAt, out _); Run(m, 30, FlyAt); // 2nd catch → +2 s
        Assert.Equal(2, m.FliesCaught); Assert.True(m.FastestCatchMs > 0); Assert.Equal(64000, m.RoundEndMs);
        Run(m, 6500, FlyAt);
        Assert.Equal(RoundState.TimedOut, m.State); Assert.Equal(2, m.Outcome.FliesCaught); Assert.Equal(64000, m.ActiveMs); Assert.Equal(4000, m.Outcome.TimeBonusMs);
    }

    // ---- rulesVersion 3 (ADR-003): combos, extra time, fly kinds, two slots
    static int CatchOne(RoundMachine m)   // attacks the stationary fly and lets it finish; returns the flies caught
    {
        Assert.Equal(ConfirmError.None, m.TryConfirm(KitchenTools.Cloth, FlyAt, out _)); Run(m, 30, FlyAt); Run(m, 80, FlyAt); return m.FliesCaught;
    }

    [Fact]
    public void Combo_grows_with_consecutive_catches_and_breaks_on_miss()
    {
        var m = new RoundMachine(new RulesConfig { SuccessiveFlies = true, FruitFlyFromIndex = 99, BluebottleFromIndex = 99 }); m.MarkReady();
        int bonusEvents = 0, lastCombo = 0; m.TimeExtended += (ms, c) => { bonusEvents++; lastCombo = c; };
        CatchOne(m); Assert.Equal(1, m.Combo); Assert.Equal(62000, m.RoundEndMs);           // 2 s
        CatchOne(m); Assert.Equal(2, m.Combo); Assert.Equal(65000, m.RoundEndMs);           // 2 + 1 s
        CatchOne(m); Assert.Equal(3, m.Combo); Assert.Equal(69000, m.RoundEndMs);           // 2 + 2 s
        Assert.Equal(3, bonusEvents); Assert.Equal(3, lastCombo); Assert.Equal(3, m.BestCombo);
        int missed = 0; m.AttackMissed += _ => missed++;
        Assert.Equal(ConfirmError.None, m.TryConfirm(KitchenTools.Cloth, Far, out _)); Run(m, 30, FlyAt);
        Assert.Equal(0, m.Combo); Assert.Equal(1, missed); Assert.Equal(3, m.BestCombo);
        Run(m, 60, FlyAt); CatchOne(m); Assert.Equal(1, m.Combo); Assert.Equal(71000, m.RoundEndMs);           // restarts at 2 s
    }

    [Fact]
    public void Time_bonus_is_capped_at_max_round()
    {
        var m = new RoundMachine(new RulesConfig { SuccessiveFlies = true, MaxRoundMs = 63000, FruitFlyFromIndex = 99, BluebottleFromIndex = 99 }); m.MarkReady();
        CatchOne(m); Assert.Equal(62000, m.RoundEndMs);
        CatchOne(m); Assert.Equal(63000, m.RoundEndMs); Assert.Equal(3000, m.TimeBonusMs);
        CatchOne(m); Assert.Equal(63000, m.RoundEndMs);
    }

    [Fact]
    public void Second_slot_opens_after_sixth_catch_and_both_flies_can_be_caught()
    {
        var m = new RoundMachine(new RulesConfig { SuccessiveFlies = true, TwoFliesFromCatch = 2, FruitFlyFromIndex = 99, BluebottleFromIndex = 99 }); m.MarkReady();
        CatchOne(m); Assert.Single(m.Flies);
        CatchOne(m); Assert.Equal(2, m.Flies.Count); Assert.True(m.Flies[0].Active); Assert.True(m.Flies[1].Active);
        // two flies, one on the target and another far away: the cloth only catches the one on the target
        m.Flies[1].Prev = m.Flies[1].Pos = Far;
        Assert.Equal(ConfirmError.None, m.TryConfirm(KitchenTools.Cloth, FlyAt, out _));
        for (int i = 0; i < 30; i++) { m.Flies[0].Prev = m.Flies[0].Pos = FlyAt; m.Flies[1].Prev = m.Flies[1].Pos = Far; m.Tick(); }
        Assert.Equal(3, m.FliesCaught); Assert.False(m.Flies[0].Active); Assert.True(m.Flies[1].Active);
        // the fridge (60 % fury) catches both if both are in the area
        for (int i = 0; i < 80; i++) { m.Flies[0].Prev = m.Flies[0].Pos = FlyAt; m.Flies[1].Prev = m.Flies[1].Pos = FlyAt; m.Tick(); }
        Assert.True(m.Flies[0].Active); Assert.NotEqual(m.Flies[0].Seed, m.Flies[1].Seed);
    }

    [Fact]
    public void Three_slots_from_tenth_catch_and_horsefly_from_index_8()
    {
        var m = new RoundMachine(new RulesConfig { SuccessiveFlies = true }); 
        Assert.Equal(1, m.SlotsFor(5)); Assert.Equal(2, m.SlotsFor(6)); Assert.Equal(2, m.SlotsFor(9)); Assert.Equal(3, m.SlotsFor(10));
        bool any = false; for (ulong s = 0; s < 200; s++) any |= m.ChooseKind(s, 8) == FlyKinds.Horsefly; Assert.True(any);
        for (ulong s = 0; s < 200; s++) Assert.NotSame(FlyKinds.Horsefly, m.ChooseKind(s, 7));
        // the horsefly band does not change the previous choices: for index 7 the distribution is the old one
        Assert.Same(m.ChooseKind(123, 7), new RoundMachine(new RulesConfig { HorseflyChance = 0f }).ChooseKind(123, 7));
    }

    [Fact]
    public void Objectives_progress_and_complete_and_boss_closes_the_day()
    {
        var rules = new RulesConfig { SuccessiveFlies = true, FruitFlyFromIndex = 99, BluebottleFromIndex = 99, HorseflyFromIndex = 99, BossAtIndex = 2 };
        var m = new RoundMachine(rules, 7, Scenes.Kitchen); m.MarkReady();
        int done = 0; m.ObjectiveCompleted += _ => done++; bool boss = false; m.BossCaught += () => boss = true;
        Assert.Equal(3, m.ObjectiveProgress.Length);
        CatchOne(m); Assert.Equal(1, m.ObjectiveProgress[0]); Assert.False(m.ObjectiveDone[0]);   // catch 5: 1/5
        CatchOne(m); Assert.Equal(FlyKinds.Boss, m.Flies[0].Kind);                             // index 2 = boss
        CatchOne(m); Assert.True(boss); Assert.True(m.BossWasCaught); Assert.True(m.RoundEndMs <= m.ActiveMs + rules.BossEndMs);
        Assert.Equal(3, m.ObjectiveProgress[1]); Assert.True(m.ObjectiveDone[1]); Assert.Equal(1, done);   // combo 3 fulfilled
        Run(m, 300, FlyAt); Assert.Equal(RoundState.TimedOut, m.State); Assert.True(m.Outcome.BossCaught); Assert.True(m.Outcome.ObjectivesDone[1]);
    }

    [Fact]
    public void Face_bonus_ends_when_fury_reaches_the_limit()
    {
        var m = new RoundMachine(FaceTools.Rules(), 3, Scenes.Face); m.MarkReady(); var far = new Vec2(0.95f, 0.05f);
        for (int k = 0; k < 12 && m.State != RoundState.TimedOut; k++) { m.TryConfirm(FaceTools.Slap, far, out _); Run(m, 60, FlyAt); }   // misses far from the fly (the hand lands on the face): each one adds fury
        Assert.Equal(RoundState.TimedOut, m.State); Assert.True(m.Fury >= 100); Assert.True(m.ActiveMs < 20000);
        var n = new RoundMachine(new RulesConfig(), 3, Scenes.Kitchen); Assert.Equal(-1, n.Rules.EndAtFury);
    }

    [Fact]
    public void Every_scene_has_three_roles_and_alternates_pair_by_role()
    {
        foreach (var sc in Scenes.All)
        {
            Assert.Equal(3, sc.Tools.Length); Assert.Equal(3, sc.Alternates.Length); Assert.Equal(3, sc.Objectives.Length);
            for (int r = 0; r < 3; r++)
            {
                Assert.Equal((ToolRole)r, sc.Tools[r].Role); Assert.Equal((ToolRole)r, sc.Alternates[r].Role);
                Assert.Equal(sc.Tools[r].MinFury, sc.Alternates[r].MinFury);
                Assert.Same(sc.Alternates[r], sc.Other(sc.Tools[r])); Assert.Same(sc.Tools[r], sc.Other(sc.Alternates[r]));
                Assert.Same(sc.Alternates[r], sc.ById(sc.Alternates[r].Id));
            }
        }
    }

    [Fact]
    public void Fly_kind_is_deterministic_and_gated_by_index()
    {
        var a = new RoundMachine(new RulesConfig(), 42); var b = new RoundMachine(new RulesConfig(), 42);
        for (int i = 0; i < 20; i++) Assert.Same(a.ChooseKind(1000 + (ulong)i, i), b.ChooseKind(1000 + (ulong)i, i));
        for (int i = 0; i < 3; i++) for (ulong s = 0; s < 50; s++) Assert.Same(FlyKinds.House, a.ChooseKind(s, i));
        bool anyBlue = false, anyFruit = false;
        for (ulong s = 0; s < 200; s++) { var k = a.ChooseKind(s, 10); anyBlue |= k == FlyKinds.Bluebottle; anyFruit |= k == FlyKinds.FruitFly; }
        Assert.True(anyBlue && anyFruit);
    }

    [Fact]
    public void Fruit_fly_has_smaller_contact_radius()
    {
        var m = new RoundMachine(new RulesConfig { SuccessiveFlies = true }); m.MarkReady();
        m.Flies[0].Kind = FlyKinds.FruitFly;
        var target = new Vec2(0.5f + 0.07f + 0.018f, 0.6f);   // edge of the cloth 0.018 W from the center: catches the house fly (0.02), not the fruit fly (0.015)
        Assert.Equal(ConfirmError.None, m.TryConfirm(KitchenTools.Cloth, target, out _)); Run(m, 30, FlyAt); Assert.Equal(0, m.FliesCaught);
        var n = new RoundMachine(new RulesConfig { SuccessiveFlies = true }); n.MarkReady();
        Assert.Equal(ConfirmError.None, n.TryConfirm(KitchenTools.Cloth, target, out _)); Run(n, 30, FlyAt); Assert.Equal(1, n.FliesCaught);
    }
}
