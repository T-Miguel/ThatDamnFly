// Game.Domain - pure C#, no UnityEngine. Round rules (rulesVersion 3: ADR-001 successive flies; ADR-003 arcade batch).
using System;
using System.Collections.Generic;

namespace ThatDamnFly.Domain
{
    /// <summary>Normalized arena: width W = 1, height H = 4/3. Origin at the bottom-left corner.</summary>
    public static class Arena
    {
        public const float W = 1f;
        public const float H = 4f / 3f;
        public const float FlyContactRadius = 0.02f;   // house fly; the other kinds have their own radius (the art does not decide collisions)
        public const float FlyVisualSize = 0.05f;
    }

    public struct Vec2
    {
        public float X, Y;
        public Vec2(float x, float y) { X = x; Y = y; }
        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Y * s);
        public float Length => (float)Math.Sqrt(X * X + Y * Y);
        public float Dot(Vec2 b) => X * b.X + Y * b.Y;
        public static Vec2 Lerp(Vec2 a, Vec2 b, float t) => a + (b - a) * t;
        public override string ToString() => $"({X:0.000},{Y:0.000})";
    }

    public enum RoundState { Preparing, Ready, Playing, Paused, Won, TimedOut, Result, Interrupted }
    public enum ToolShape { Circle, Rect }
    public enum ToolGesture { Tap, PressAimRelease }
    public enum AttackPhase { Preparing, Active, Done }
    public enum FlyKind { House, Bluebottle, FruitFly, Horsefly, Boss, Golden }
    /// <summary>Requests (ADR-014): per-scene objectives that award stars and unlock the next scene.</summary>
    public enum ObjectiveKind { CatchN, CatchKind, ComboN, CatchWithRole, CatchOnFood, FastCatch }
    /// <summary>Role of the tool in the scene: fast/small, medium (fury 25), giant (fury 60).</summary>
    public enum ToolRole { Fast, Medium, Giant }

    public sealed class ToolDefinition
    {
        public string Id;
        public ToolShape Shape;
        public float Radius;          // W (circle)
        public float RectW, RectH;    // W (rectangle)
        /// <summary>Cooldown counts from the end of the attack; in the presentation the object takes exactly that long to return to the hand (ADR-017: "as soon as it is back, it can be used"). Values ×0.6 of the originals on Sept 17.</summary>
        public int PrepMs, ActiveMs, CooldownMs;
        public int MinFury;
        public ToolGesture Gesture;
        public ToolRole Role;
        /// <summary>Equivalent radius for the sensory encoder (disc of equal area).</summary>
        public float SensoryRadius => Shape == ToolShape.Circle ? Radius : (float)Math.Sqrt(RectW * RectH / Math.PI);
    }

    /// <summary>Kitchen tools. rulesVersion 3 (ADR-003): pan and fridge faster and larger - with the original timings the fly always escaped.</summary>
    public static class KitchenTools
    {
        public static readonly ToolDefinition Cloth = new ToolDefinition { Id = "kitchen_cloth", Role = ToolRole.Fast, Shape = ToolShape.Circle, Radius = 0.07f, PrepMs = 140, ActiveMs = 80, CooldownMs = 330, MinFury = 0, Gesture = ToolGesture.Tap };
        public static readonly ToolDefinition Pan = new ToolDefinition { Id = "kitchen_pan", Role = ToolRole.Medium, Shape = ToolShape.Circle, Radius = 0.20f, PrepMs = 300, ActiveMs = 120, CooldownMs = 660, MinFury = 25, Gesture = ToolGesture.Tap };
        public static readonly ToolDefinition Fridge = new ToolDefinition { Id = "kitchen_fridge", Role = ToolRole.Giant, Shape = ToolShape.Rect, RectW = 0.60f, RectH = 0.26f, PrepMs = 450, ActiveMs = 200, CooldownMs = 1320, MinFury = 60, Gesture = ToolGesture.PressAimRelease };
        public static readonly ToolDefinition[] All = { Cloth, Pan, Fridge };
        public static ToolDefinition ById(string id) { foreach (var t in All) if (t.Id == id) return t; return null; }
    }

    /// <summary>Picnic tools (ADR-004): same roles and values as the kitchen - the scene changes the art and the sound, not the balance nor the science.</summary>
    public static class PicnicTools
    {
        public static readonly ToolDefinition Napkin = new ToolDefinition { Id = "picnic_napkin", Role = ToolRole.Fast, Shape = ToolShape.Circle, Radius = 0.07f, PrepMs = 140, ActiveMs = 80, CooldownMs = 330, MinFury = 0, Gesture = ToolGesture.Tap };
        public static readonly ToolDefinition Frisbee = new ToolDefinition { Id = "picnic_frisbee", Role = ToolRole.Medium, Shape = ToolShape.Circle, Radius = 0.20f, PrepMs = 300, ActiveMs = 120, CooldownMs = 660, MinFury = 25, Gesture = ToolGesture.Tap };
        public static readonly ToolDefinition Basket = new ToolDefinition { Id = "picnic_basket", Role = ToolRole.Giant, Shape = ToolShape.Rect, RectW = 0.60f, RectH = 0.26f, PrepMs = 450, ActiveMs = 200, CooldownMs = 1320, MinFury = 60, Gesture = ToolGesture.PressAimRelease };
        public static readonly ToolDefinition[] All = { Napkin, Frisbee, Basket };
    }


    /// <summary>Bathroom (ADR-011): toilet roll, plunger, toilet lid.</summary>
    public static class BathTools
    {
        public static readonly ToolDefinition Fast = new ToolDefinition { Id = "bath_paper", Role = ToolRole.Fast, Shape = ToolShape.Circle, Radius = 0.07f, PrepMs = 140, ActiveMs = 80, CooldownMs = 330, MinFury = 0, Gesture = ToolGesture.Tap };
        public static readonly ToolDefinition Medium = new ToolDefinition { Id = "bath_plunger", Role = ToolRole.Medium, Shape = ToolShape.Circle, Radius = 0.20f, PrepMs = 300, ActiveMs = 120, CooldownMs = 660, MinFury = 25, Gesture = ToolGesture.Tap };
        public static readonly ToolDefinition Giant = new ToolDefinition { Id = "bath_lid", Role = ToolRole.Giant, Shape = ToolShape.Rect, RectW = 0.60f, RectH = 0.26f, PrepMs = 450, ActiveMs = 200, CooldownMs = 1320, MinFury = 60, Gesture = ToolGesture.PressAimRelease };
        public static readonly ToolDefinition[] All = { Fast, Medium, Giant };
    }
    /// <summary>Living room (ADR-011): rolled newspaper, cushion, sofa.</summary>
    public static class LivingTools
    {
        public static readonly ToolDefinition Fast = new ToolDefinition { Id = "living_newspaper", Role = ToolRole.Fast, Shape = ToolShape.Circle, Radius = 0.07f, PrepMs = 140, ActiveMs = 80, CooldownMs = 330, MinFury = 0, Gesture = ToolGesture.Tap };
        public static readonly ToolDefinition Medium = new ToolDefinition { Id = "living_cushion", Role = ToolRole.Medium, Shape = ToolShape.Circle, Radius = 0.20f, PrepMs = 300, ActiveMs = 120, CooldownMs = 660, MinFury = 25, Gesture = ToolGesture.Tap };
        public static readonly ToolDefinition Giant = new ToolDefinition { Id = "living_sofa", Role = ToolRole.Giant, Shape = ToolShape.Rect, RectW = 0.60f, RectH = 0.26f, PrepMs = 450, ActiveMs = 200, CooldownMs = 1320, MinFury = 60, Gesture = ToolGesture.PressAimRelease };
        public static readonly ToolDefinition[] All = { Fast, Medium, Giant };
    }
    /// <summary>Beach (ADR-011): flip-flop, ball, parasol.</summary>
    public static class BeachTools
    {
        public static readonly ToolDefinition Fast = new ToolDefinition { Id = "beach_flipflop", Role = ToolRole.Fast, Shape = ToolShape.Circle, Radius = 0.07f, PrepMs = 140, ActiveMs = 80, CooldownMs = 330, MinFury = 0, Gesture = ToolGesture.Tap };
        public static readonly ToolDefinition Medium = new ToolDefinition { Id = "beach_ball", Role = ToolRole.Medium, Shape = ToolShape.Circle, Radius = 0.20f, PrepMs = 300, ActiveMs = 120, CooldownMs = 660, MinFury = 25, Gesture = ToolGesture.Tap };
        public static readonly ToolDefinition Giant = new ToolDefinition { Id = "beach_umbrella", Role = ToolRole.Giant, Shape = ToolShape.Rect, RectW = 0.60f, RectH = 0.26f, PrepMs = 450, ActiveMs = 200, CooldownMs = 1320, MinFury = 60, Gesture = ToolGesture.PressAimRelease };
        public static readonly ToolDefinition[] All = { Fast, Medium, Giant };
    }
    /// <summary>Café terrace (ADR-011): menu, tray, sun umbrella.</summary>
    public static class CafeTools
    {
        public static readonly ToolDefinition Fast = new ToolDefinition { Id = "cafe_menu", Role = ToolRole.Fast, Shape = ToolShape.Circle, Radius = 0.07f, PrepMs = 140, ActiveMs = 80, CooldownMs = 330, MinFury = 0, Gesture = ToolGesture.Tap };
        public static readonly ToolDefinition Medium = new ToolDefinition { Id = "cafe_tray", Role = ToolRole.Medium, Shape = ToolShape.Circle, Radius = 0.20f, PrepMs = 300, ActiveMs = 120, CooldownMs = 660, MinFury = 25, Gesture = ToolGesture.Tap };
        public static readonly ToolDefinition Giant = new ToolDefinition { Id = "cafe_parasol", Role = ToolRole.Giant, Shape = ToolShape.Rect, RectW = 0.60f, RectH = 0.26f, PrepMs = 450, ActiveMs = 200, CooldownMs = 1320, MinFury = 60, Gesture = ToolGesture.PressAimRelease };
        public static readonly ToolDefinition[] All = { Fast, Medium, Giant };
    }
    /// <summary>Backyard (ADR-011): fan, grill, barbecue lid.</summary>
    public static class YardTools
    {
        public static readonly ToolDefinition Fast = new ToolDefinition { Id = "yard_fan", Role = ToolRole.Fast, Shape = ToolShape.Circle, Radius = 0.07f, PrepMs = 140, ActiveMs = 80, CooldownMs = 330, MinFury = 0, Gesture = ToolGesture.Tap };
        public static readonly ToolDefinition Medium = new ToolDefinition { Id = "yard_grate", Role = ToolRole.Medium, Shape = ToolShape.Circle, Radius = 0.20f, PrepMs = 300, ActiveMs = 120, CooldownMs = 660, MinFury = 25, Gesture = ToolGesture.Tap };
        public static readonly ToolDefinition Giant = new ToolDefinition { Id = "yard_grilllid", Role = ToolRole.Giant, Shape = ToolShape.Rect, RectW = 0.60f, RectH = 0.26f, PrepMs = 450, ActiveMs = 200, CooldownMs = 1320, MinFury = 60, Gesture = ToolGesture.PressAimRelease };
        public static readonly ToolDefinition[] All = { Fast, Medium, Giant };
    }


    /// <summary>Second objects per role (ADR-013, rotating hand): slightly different values within the role; the fury thresholds do not change.</summary>
    /// <summary>ADR-019: the hand of the face bonus (you shoo the flies off the face) - fast, no minimum fury, short cooldown.</summary>
    public static class FaceTools
    {
        public static readonly ToolDefinition Slap = new ToolDefinition { Id = "face_slap", Role = ToolRole.Fast, Shape = ToolShape.Circle, Radius = 0.085f, PrepMs = 100, ActiveMs = 80, CooldownMs = 240, MinFury = 0, Gesture = ToolGesture.Tap };
        /// <summary>Bonus rules: 20 s without extension, each miss lands the hand on the face and adds fury; at 100 the character bursts.</summary>
        public static RulesConfig Rules() => new RulesConfig { RoundMs = 20000, MaxRoundMs = 20000, TwoFliesFromCatch = 2, ThreeFliesFromCatch = 5, BluebottleFromIndex = 2, FruitFlyFromIndex = 3, HorseflyFromIndex = 6, GoldenFromIndex = 3, FuryNearDistance = 10f, FuryMissCooldownMs = 250, FuryMissGain = 12, FuryNearMissGain = 0, FuryBonusGain = 0, EndAtFury = 100 };
    }

    public static class AltTools
    {
        public static readonly ToolDefinition[] Kitchen = { new ToolDefinition { Id = "kitchen_spatula", Role = ToolRole.Fast, Shape = ToolShape.Circle, Radius = 0.06f, PrepMs = 110, ActiveMs = 80, CooldownMs = 270, MinFury = 0, Gesture = ToolGesture.Tap }, new ToolDefinition { Id = "kitchen_potlid", Role = ToolRole.Medium, Shape = ToolShape.Circle, Radius = 0.18f, PrepMs = 260, ActiveMs = 120, CooldownMs = 570, MinFury = 25, Gesture = ToolGesture.Tap }, new ToolDefinition { Id = "kitchen_table", Role = ToolRole.Giant, Shape = ToolShape.Rect, RectW = 0.7f, RectH = 0.22f, PrepMs = 520, ActiveMs = 200, CooldownMs = 1440, MinFury = 60, Gesture = ToolGesture.PressAimRelease } };
        public static readonly ToolDefinition[] Picnic = { new ToolDefinition { Id = "picnic_strawhat", Role = ToolRole.Fast, Shape = ToolShape.Circle, Radius = 0.08f, PrepMs = 160, ActiveMs = 80, CooldownMs = 360, MinFury = 0, Gesture = ToolGesture.Tap }, new ToolDefinition { Id = "picnic_plate", Role = ToolRole.Medium, Shape = ToolShape.Circle, Radius = 0.19f, PrepMs = 280, ActiveMs = 120, CooldownMs = 600, MinFury = 25, Gesture = ToolGesture.Tap }, new ToolDefinition { Id = "picnic_cooler", Role = ToolRole.Giant, Shape = ToolShape.Rect, RectW = 0.5f, RectH = 0.3f, PrepMs = 480, ActiveMs = 200, CooldownMs = 1200, MinFury = 60, Gesture = ToolGesture.PressAimRelease } };
        public static readonly ToolDefinition[] Bath = { new ToolDefinition { Id = "bath_sponge", Role = ToolRole.Fast, Shape = ToolShape.Circle, Radius = 0.065f, PrepMs = 120, ActiveMs = 80, CooldownMs = 290, MinFury = 0, Gesture = ToolGesture.Tap }, new ToolDefinition { Id = "bath_brush", Role = ToolRole.Medium, Shape = ToolShape.Circle, Radius = 0.17f, PrepMs = 250, ActiveMs = 120, CooldownMs = 540, MinFury = 25, Gesture = ToolGesture.Tap }, new ToolDefinition { Id = "bath_bathmat", Role = ToolRole.Giant, Shape = ToolShape.Rect, RectW = 0.7f, RectH = 0.24f, PrepMs = 500, ActiveMs = 200, CooldownMs = 1380, MinFury = 60, Gesture = ToolGesture.PressAimRelease } };
        public static readonly ToolDefinition[] Living = { new ToolDefinition { Id = "living_remote", Role = ToolRole.Fast, Shape = ToolShape.Circle, Radius = 0.055f, PrepMs = 100, ActiveMs = 80, CooldownMs = 250, MinFury = 0, Gesture = ToolGesture.Tap }, new ToolDefinition { Id = "living_book", Role = ToolRole.Medium, Shape = ToolShape.Circle, Radius = 0.18f, PrepMs = 270, ActiveMs = 120, CooldownMs = 570, MinFury = 25, Gesture = ToolGesture.Tap }, new ToolDefinition { Id = "living_rug", Role = ToolRole.Giant, Shape = ToolShape.Rect, RectW = 0.72f, RectH = 0.26f, PrepMs = 520, ActiveMs = 200, CooldownMs = 1440, MinFury = 60, Gesture = ToolGesture.PressAimRelease } };
        public static readonly ToolDefinition[] Beach = { new ToolDefinition { Id = "beach_shovel", Role = ToolRole.Fast, Shape = ToolShape.Circle, Radius = 0.065f, PrepMs = 120, ActiveMs = 80, CooldownMs = 300, MinFury = 0, Gesture = ToolGesture.Tap }, new ToolDefinition { Id = "beach_bucket", Role = ToolRole.Medium, Shape = ToolShape.Circle, Radius = 0.17f, PrepMs = 260, ActiveMs = 120, CooldownMs = 540, MinFury = 25, Gesture = ToolGesture.Tap }, new ToolDefinition { Id = "beach_towel", Role = ToolRole.Giant, Shape = ToolShape.Rect, RectW = 0.66f, RectH = 0.28f, PrepMs = 480, ActiveMs = 200, CooldownMs = 1200, MinFury = 60, Gesture = ToolGesture.PressAimRelease } };
        public static readonly ToolDefinition[] Cafe = { new ToolDefinition { Id = "cafe_coaster", Role = ToolRole.Fast, Shape = ToolShape.Circle, Radius = 0.06f, PrepMs = 110, ActiveMs = 80, CooldownMs = 270, MinFury = 0, Gesture = ToolGesture.Tap }, new ToolDefinition { Id = "cafe_plate", Role = ToolRole.Medium, Shape = ToolShape.Circle, Radius = 0.19f, PrepMs = 280, ActiveMs = 120, CooldownMs = 600, MinFury = 25, Gesture = ToolGesture.Tap }, new ToolDefinition { Id = "cafe_chair", Role = ToolRole.Giant, Shape = ToolShape.Rect, RectW = 0.5f, RectH = 0.3f, PrepMs = 480, ActiveMs = 200, CooldownMs = 1200, MinFury = 60, Gesture = ToolGesture.PressAimRelease } };
        public static readonly ToolDefinition[] Yard = { new ToolDefinition { Id = "yard_spatula", Role = ToolRole.Fast, Shape = ToolShape.Circle, Radius = 0.06f, PrepMs = 110, ActiveMs = 80, CooldownMs = 270, MinFury = 0, Gesture = ToolGesture.Tap }, new ToolDefinition { Id = "yard_pot", Role = ToolRole.Medium, Shape = ToolShape.Circle, Radius = 0.19f, PrepMs = 300, ActiveMs = 120, CooldownMs = 660, MinFury = 25, Gesture = ToolGesture.Tap }, new ToolDefinition { Id = "yard_table", Role = ToolRole.Giant, Shape = ToolShape.Rect, RectW = 0.7f, RectH = 0.22f, PrepMs = 520, ActiveMs = 200, CooldownMs = 1440, MinFury = 60, Gesture = ToolGesture.PressAimRelease } };
    }

    /// <summary>Scene: identifier, tools (always three: fast, medium, giant). The arena, the fly and the circuit are the same in all of them.</summary>
    public sealed class SceneDef
    {
        public string Id; public ToolDefinition[] Tools;
        /// <summary>Three requests per scene (ADR-014).</summary>
        public Objective[] Objectives = new Objective[0];
        /// <summary>Last scene of the day: the boss fly enters at catch BossAtCatch.</summary>
        public bool Boss;
        /// <summary>Second object per role (ADR-013): enters the hand when the first is spent on a catch, and vice versa.</summary>
        public ToolDefinition[] Alternates = new ToolDefinition[0];
        /// <summary>Outdoor (bright music, open-air ambience) or indoor.</summary>
        public bool Outdoor;
        /// <summary>Food spots (ADR-007): attract the wandering and invite landing. They only affect the body; never the circuit.</summary>
        public FoodSpot[] Food = new FoodSpot[0];
        public ToolDefinition ByRole(ToolRole r) { foreach (var t in Tools) if (t.Role == r) return t; return Tools[0]; }
        public ToolDefinition ById(string id) { foreach (var t in Tools) if (t.Id == id) return t; foreach (var t in Alternates) if (t.Id == id) return t; return null; }
        /// <summary>The other object of the same role (primary ↔ alternate).</summary>
        public ToolDefinition Other(ToolDefinition t) { for (int i = 0; i < Tools.Length; i++) { if (Tools[i] == t && i < Alternates.Length) return Alternates[i]; if (i < Alternates.Length && Alternates[i] == t) return Tools[i]; } return t; }
    }

    public struct FoodSpot { public Vec2 Pos; public float Radius; public FoodSpot(float x, float y, float r) { Pos = new Vec2(x, y); Radius = r; } }

    public sealed class Objective
    {
        public string Id; public ObjectiveKind Kind; public int N = 1; public FlyKind FlyKind; public ToolRole Role; public int Ms;
        public static Objective Catch(int n) => new Objective { Id = "catch" + n, Kind = ObjectiveKind.CatchN, N = n };
        public static Objective OfKind(FlyKind k, int n = 1) => new Objective { Id = "kind_" + k.ToString().ToLowerInvariant() + (n > 1 ? n.ToString() : ""), Kind = ObjectiveKind.CatchKind, FlyKind = k, N = n };
        public static Objective ComboOf(int n) => new Objective { Id = "combo" + n, Kind = ObjectiveKind.ComboN, N = n };
        public static Objective WithRole(ToolRole r, int n = 1) => new Objective { Id = "role_" + r.ToString().ToLowerInvariant() + (n > 1 ? n.ToString() : ""), Kind = ObjectiveKind.CatchWithRole, Role = r, N = n };
        public static Objective OnFood(int n = 1) => new Objective { Id = "food" + (n > 1 ? n.ToString() : ""), Kind = ObjectiveKind.CatchOnFood, N = n };
        public static Objective Fast(int ms) => new Objective { Id = "fast" + ms, Kind = ObjectiveKind.FastCatch, Ms = ms, N = 1 };
    }

    public static class Scenes
    {
        public static readonly SceneDef Kitchen = new SceneDef { Id = "kitchen", Tools = KitchenTools.All, Alternates = AltTools.Kitchen, Objectives = new[] { Objective.Catch(4), Objective.ComboOf(3), Objective.OnFood() }, Food = new[] { new FoodSpot(0.54f, 0.48f, 0.07f), new FoodSpot(0.175f, 0.47f, 0.06f) } };   // fruit bowl, cutting board
        public static readonly SceneDef Picnic = new SceneDef { Id = "picnic", Outdoor = true, Tools = PicnicTools.All, Alternates = AltTools.Picnic, Objectives = new[] { Objective.Catch(7), Objective.OnFood(3), Objective.WithRole(ToolRole.Medium, 2) }, Food = new[] { new FoodSpot(0.583f, 0.60f, 0.09f), new FoodSpot(0.742f, 0.425f, 0.08f), new FoodSpot(0.44f, 0.667f, 0.05f), new FoodSpot(0.35f, 0.52f, 0.05f) } };   // sandwiches, watermelon, lemonade, apples
        public static readonly SceneDef Bath = new SceneDef { Id = "bath", Boss = true, Tools = BathTools.All, Alternates = AltTools.Bath, Objectives = new[] { Objective.Catch(10), Objective.ComboOf(5), Objective.OfKind(FlyKind.Boss) }, Food = new[] { new FoodSpot(0.72f, 0.68f, 0.05f), new FoodSpot(0.27f, 0.87f, 0.05f) } };                                   // toothpaste, soap
        public static readonly SceneDef Living = new SceneDef { Id = "living", Tools = LivingTools.All, Alternates = AltTools.Living, Objectives = new[] { Objective.Catch(9), Objective.WithRole(ToolRole.Giant, 2), Objective.Fast(2000) }, Food = new[] { new FoodSpot(0.54f, 0.34f, 0.07f) } };                                                              // popcorn
        public static readonly SceneDef Beach = new SceneDef { Id = "beach", Outdoor = true, Tools = BeachTools.All, Alternates = AltTools.Beach, Objectives = new[] { Objective.Catch(6), Objective.ComboOf(4), Objective.OfKind(FlyKind.FruitFly) }, Food = new[] { new FoodSpot(0.54f, 0.37f, 0.06f), new FoodSpot(0.20f, 0.25f, 0.06f) } };            // ice cream, Berlin doughnut
        public static readonly SceneDef Cafe = new SceneDef { Id = "cafe", Outdoor = true, Tools = CafeTools.All, Alternates = AltTools.Cafe, Objectives = new[] { Objective.Catch(5), Objective.WithRole(ToolRole.Giant), Objective.OfKind(FlyKind.Bluebottle) }, Food = new[] { new FoodSpot(0.40f, 0.34f, 0.06f), new FoodSpot(0.28f, 0.35f, 0.05f) } };               // custard tart, espresso
        public static readonly SceneDef Yard = new SceneDef { Id = "yard", Outdoor = true, Tools = YardTools.All, Alternates = AltTools.Yard, Objectives = new[] { Objective.Catch(8), Objective.OfKind(FlyKind.Horsefly), Objective.ComboOf(5) }, Food = new[] { new FoodSpot(0.23f, 0.45f, 0.08f), new FoodSpot(0.40f, 0.46f, 0.05f), new FoodSpot(0.80f, 0.31f, 0.10f) } };   // sausages, corn, grill
        /// <summary>Order of the day (ADR-014): breakfast → café terrace → beach → picnic → barbecue → evening → night.</summary>
        public static readonly SceneDef[] All = { Kitchen, Cafe, Beach, Picnic, Yard, Living, Bath };
        public static int IndexOf(SceneDef s) => Array.IndexOf(All, s);
        /// <summary>ADR-019: the face bonus - outside the Day. A single object (the hand); the flies are attracted to the nose, ears, mouth and forehead (food spots of body v1.6).</summary>
        public static readonly SceneDef Face = new SceneDef { Id = "face", Tools = new[] { FaceTools.Slap }, Food = new[] { new FoodSpot(0.50f, 0.53f, 0.07f), new FoodSpot(0.15f, 0.55f, 0.06f), new FoodSpot(0.85f, 0.55f, 0.06f), new FoodSpot(0.50f, 0.35f, 0.07f), new FoodSpot(0.50f, 0.90f, 0.08f) } };
        public static SceneDef ById(string id) { if (id == "face") return Face; foreach (var s in All) if (s.Id == id) return s; return Kitchen; }
    }

    /// <summary>Fly kind: only changes the body (contact radius, agitation) and the time value. The neural circuit is the same.</summary>
    public sealed class FlyKindDef
    {
        public FlyKind Kind; public string Id;
        public float ContactRadius;
        public float VisualScale, SpeedFactor, DartFactor, LandFactor, BuzzPitch;
        public int TimeBonusMs;
        /// <summary>Short landings (0.2–0.4 s) followed by a dart: "fakes a landing".</summary>
        public bool LandShort;
    }

    public static class FlyKinds
    {
        public static readonly FlyKindDef House = new FlyKindDef { Kind = FlyKind.House, Id = "house", ContactRadius = 0.02f, VisualScale = 1f, SpeedFactor = 1f, DartFactor = 1f, LandFactor = 1f, BuzzPitch = 1f, TimeBonusMs = 2000 };
        public static readonly FlyKindDef Bluebottle = new FlyKindDef { Kind = FlyKind.Bluebottle, Id = "bluebottle", ContactRadius = 0.028f, VisualScale = 1.35f, SpeedFactor = 0.8f, DartFactor = 0.7f, LandFactor = 1.4f, BuzzPitch = 0.72f, TimeBonusMs = 3000 };
        public static readonly FlyKindDef FruitFly = new FlyKindDef { Kind = FlyKind.FruitFly, Id = "fruitfly", ContactRadius = 0.015f, VisualScale = 0.72f, SpeedFactor = 1.25f, DartFactor = 1.6f, LandFactor = 0.6f, BuzzPitch = 1.6f, TimeBonusMs = 4000 };
        /// <summary>Horsefly (ADR-007): big and fast, fakes landings (very short) and darts; worth 5 s.</summary>
        public static readonly FlyKindDef Horsefly = new FlyKindDef { Kind = FlyKind.Horsefly, Id = "horsefly", ContactRadius = 0.03f, VisualScale = 1.45f, SpeedFactor = 1.35f, DartFactor = 2.2f, LandFactor = 1.6f, BuzzPitch = 0.55f, TimeBonusMs = 5000, LandShort = true };
        /// <summary>Boss fly (ADR-014): huge, fast, fakes landings; only in the last scene of the day, at the 10th catch; worth 10 s and closes the day.</summary>
        public static readonly FlyKindDef Boss = new FlyKindDef { Kind = FlyKind.Boss, Id = "boss", ContactRadius = 0.034f, VisualScale = 1.9f, SpeedFactor = 1.5f, DartFactor = 2.5f, LandFactor = 1.2f, BuzzPitch = 0.45f, TimeBonusMs = 10000, LandShort = true };
        /// <summary>ADR-016: golden fly - rare (≈6 % from the 5th index), agile, worth +8 s; counts in the collection.</summary>
        public static readonly FlyKindDef Golden = new FlyKindDef { Kind = FlyKind.Golden, Id = "golden", ContactRadius = 0.02f, VisualScale = 1.05f, SpeedFactor = 1.15f, DartFactor = 1.3f, LandFactor = 0.8f, BuzzPitch = 1.1f, TimeBonusMs = 8000 };
        public static FlyKindDef Of(FlyKind k) => k == FlyKind.Bluebottle ? Bluebottle : (k == FlyKind.FruitFly ? FruitFly : (k == FlyKind.Horsefly ? Horsefly : (k == FlyKind.Boss ? Boss : (k == FlyKind.Golden ? Golden : House))));
        public const int Count = 6;
    }

    public sealed class RulesConfig
    {
        public const string RulesVersion = "3";
        /// <summary>rulesVersion 2+: catching a fly brings in another until time runs out. false = original contract (one fly, ends on the catch).</summary>
        public bool SuccessiveFlies = true;
        public int RespawnDelayMs = 600;
        public int RoundMs = 60000;
        /// <summary>ADR-003: every catch adds time (the fly kind's value + combo bonus), up to this round maximum.</summary>
        public int MaxRoundMs = 90000;
        public int ComboTimeBonusMs = 1000;
        public int ComboTimeBonusCap = 3;
        /// <summary>From this catch on, two flies are active at the same time.</summary>
        public int TwoFliesFromCatch = 6;
        /// <summary>From this catch on, three active flies (ADR-007).</summary>
        public int ThreeFliesFromCatch = 10;
        public int BluebottleFromIndex = 3;
        public int FruitFlyFromIndex = 5;
        public int HorseflyFromIndex = 8;
        public float BluebottleChance = 0.35f, FruitFlyChance = 0.30f, HorseflyChance = 0.25f;
        public int GoldenFromIndex = 4; public float GoldenChance = 0.06f;   // ADR-016
        /// <summary>Index of the boss fly (−1 = none); catching it closes the round 2 s later (end of the day).</summary>
        public int BossAtIndex = -1;
        /// <summary>Index of the boss fly in scenes with a boss (ADR-014): the 11th fly, after the 3rd wave opens.</summary>
        public const int BossIndexDefault = 10;
        public int BossEndMs = 2000;
        /// <summary>ADR-010: offset of the body's agitation level (−1 beginner … +3 expert); 0 on the fly of the day. Body only; never the circuit.</summary>
        public int LevelOffset = 0;
        public int ReadyMaxMs = 10000;
        public int GlobalIntervalMs = 350;
        public float FuryNearDistance = 0.12f;
        public float NearMissMargin = 0.03f;
        public int FuryMissCooldownMs = 1200;
        public int FuryBonusPeriodMs = 5000;
        public int FuryMissGain = 10, FuryNearMissGain = 5, FuryBonusGain = 5;
        /// <summary>ADR-019 (face bonus): the round ends when fury reaches this (−1 = never). The character "bursts".</summary>
        public int EndAtFury = -1;
        public int[] FuryThresholds = { 0, 25, 60 };
        public float AssistScale = 1.25f;
        public int TickMs = 10;
    }

    public enum ConfirmError { None, WrongState, OutsideArena, ToolUnknown, FuryTooLow, OnCooldown, GlobalInterval, AnotherAttackActive }

    public sealed class Attack
    {
        public int Id;
        public ToolDefinition Tool;
        public Vec2 Target;
        public int ConfirmedAtSimMs;   // simulated time (Ready+Playing, ms) at confirmation
        public int PrepEndSimMs, ActiveEndSimMs;
        public AttackPhase Phase = AttackPhase.Preparing;
        public float ScaledRadius, ScaledRectW, ScaledRectH;
        public float MinEdgeDistance = float.MaxValue;   // smallest distance (edge of the active area ↔ edge of the body) during the active phase
        public bool Lethal;
        public int LethalAtActiveMs = -1;
        public bool FuryApplied;
        /// <summary>Visible position of the object (for the sensory encoder and the presentation): starts at the bottom of the arena and reaches the target at the end of the preparation.</summary>
        public Vec2 SpawnPoint;
        public Vec2 VisiblePosition(int simMs)
        {
            if (Phase == AttackPhase.Preparing)
            {
                float t = (float)(simMs - ConfirmedAtSimMs) / Math.Max(1, PrepEndSimMs - ConfirmedAtSimMs);
                if (t < 0) t = 0; if (t > 1) t = 1;
                return Vec2.Lerp(SpawnPoint, Target, t);
            }
            return Target;
        }
        /// <summary>Fraction of the visible/sensory radius during the descent (0.3 → 1 in the preparation; 1 in the active phase). The threat grows in place: looming at the target.</summary>
        public float ThreatRadiusFraction(int simMs)
        {
            if (Phase != AttackPhase.Preparing) return 1f;
            float t = (float)(simMs - ConfirmedAtSimMs) / Math.Max(1, PrepEndSimMs - ConfirmedAtSimMs); if (t < 0) t = 0; if (t > 1) t = 1;
            return 0.3f + 0.7f * t;
        }
        public Vec2 VisibleVelocity()
        {
            if (Phase != AttackPhase.Preparing) return new Vec2(0, 0);
            float secs = Math.Max(1, PrepEndSimMs - ConfirmedAtSimMs) / 1000f;
            return (Target - SpawnPoint) * (1f / secs);
        }
    }

    /// <summary>One fly of the round (one per "slot"; up to two slots). The position is written by the simulation before each tick.</summary>
    public sealed class FlyState
    {
        public int Slot;
        public int Index;                 // n-th fly of the round (0 = first) - agitation level of the body
        public FlyKindDef Kind = FlyKinds.House;
        public bool Active;
        public int RespawnAtSimMs = -1;
        public int EnteredSimMs;
        public ulong Seed;
        public Vec2 Prev, Pos;
    }

    public sealed class RoundEvent
    {
        public string Type; public int ActiveMs; public string Detail;
        public override string ToString() => $"{ActiveMs}ms {Type} {Detail}";
    }

    public sealed class RoundOutcome
    {
        public RoundState Terminal;
        public int ActiveMs;
        public int Attacks;
        public bool Assisted;
        public string LastToolId;
        public int Fury;
        public int LethalAttackId = -1;
        public int FliesCaught;
        public int FastestCatchMs = -1;
        public int BestCombo;
        public int TimeBonusMs;
        public int RoundEndMs;
        public bool BossCaught;
        /// <summary>ADR-016: catches per kind (index = FlyKind), for the collection.</summary>
        public int[] CaughtByKind = new int[FlyKinds.Count];
        public bool[] ObjectivesDone = new bool[0];
        public int[] ObjectiveProgress = new int[0];
    }

    /// <summary>Round state machine + attacks + fury + flies. Advances in 10 ms ticks; clocks only advance in Ready/Playing.</summary>
    public sealed class RoundMachine
    {
        public readonly RulesConfig Rules;
        public readonly ulong Seed;
        public RoundState State { get; private set; } = RoundState.Preparing;
        public int ActiveMs { get; private set; }        // active time in Playing (0 until it starts)
        public int ReadyMs { get; private set; }         // time in Ready
        public int SimMs { get; private set; }           // total simulated time (Ready + Playing), used by the attacks
        public int Fury { get; private set; }
        public bool Assisted;
        public int AttackCount { get; private set; }
        public string LastToolId { get; private set; }
        public int FliesCaught { get; private set; }
        public int FastestCatchMs { get; private set; } = -1;
        /// <summary>Consecutive catches without a missed attack.</summary>
        public int Combo { get; private set; }
        public int BestCombo { get; private set; }
        /// <summary>End of the round in active time (grows with the catches up to Rules.MaxRoundMs).</summary>
        public int RoundEndMs { get; private set; }
        public int TimeBonusMs { get; private set; }
        public int RemainingMs => Math.Max(0, RoundEndMs - ActiveMs);
        public readonly List<FlyState> Flies = new List<FlyState>();
        /// <summary>true if at least one fly is active.</summary>
        public bool FlyActive { get { foreach (var f in Flies) if (f.Active) return true; return false; } }
        public int FlyEnteredSimMs => Flies[0].EnteredSimMs;
        int _spawned = 1;
        /// <summary>Fly caught: (fly, id of the lethal attack, active time of the catch).</summary>
        public event Action<FlyState, int, int> FlyCaught;
        /// <summary>A fly entered (new or next).</summary>
        public event Action<FlyState> FlySpawned;
        /// <summary>Time added on a catch: (ms granted, combo at that moment).</summary>
        public event Action<int, int> TimeExtended;
        /// <summary>Attack ended without a catch.</summary>
        public event Action<Attack> AttackMissed;
        public RoundOutcome Outcome { get; private set; }
        public readonly List<RoundEvent> Events = new List<RoundEvent>();
        public readonly List<Attack> Attacks = new List<Attack>();
        readonly Dictionary<string, int> _cooldownUntilSimMs = new Dictionary<string, int>();
        int _nextAttackId = 1, _lastConfirmSimMs = -1000000, _lastFuryMissActiveMs = -1000000, _nextBonusBoundaryMs;
        readonly List<int> _validAttackActiveMs = new List<int>();
        int _pausedFrom = -1; // -1 = not paused
        int _lastCatchActiveMs = 0;
        readonly List<(FlyState fly, Attack attack, int atMs)> _hits = new List<(FlyState, Attack, int)>();

        public readonly SceneDef Scene;
        /// <summary>Progress and completion of the scene's requests in this round.</summary>
        public readonly int[] ObjectiveProgress; public readonly bool[] ObjectiveDone;
        public event Action<int> ObjectiveCompleted;
        public event Action BossCaught;
        public bool BossWasCaught { get; private set; }
        public readonly int[] CaughtByKind = new int[FlyKinds.Count];

        public RoundMachine(RulesConfig rules, ulong seed = 0, SceneDef scene = null)
        {
            Scene = scene; int n = scene?.Objectives.Length ?? 0; ObjectiveProgress = new int[n]; ObjectiveDone = new bool[n];
            Rules = rules; Seed = seed; _nextBonusBoundaryMs = rules.FuryBonusPeriodMs; RoundEndMs = rules.RoundMs;
            Flies.Add(new FlyState { Slot = 0, Index = 0, Kind = rules.BossAtIndex == 0 ? FlyKinds.Boss : FlyKinds.House, Active = true, Seed = seed, EnteredSimMs = 0 });   // the first is always a house fly (unless boss at index 0, development only)
        }

        public void MarkReady() { if (State != RoundState.Preparing) return; State = RoundState.Ready; Emit("round_ready", ""); }

        public Attack ActiveAttack { get { foreach (var a in Attacks) if (a.Phase != AttackPhase.Done) return a; return null; } }
        public int CooldownRemainingMs(string toolId) => _cooldownUntilSimMs.TryGetValue(toolId, out var until) ? Math.Max(0, until - SimMs) : 0;
        public bool IsEligible(ToolDefinition t) => Fury >= t.MinFury;
        public int FuryLevel { get { int lvl = 0; for (int i = 0; i < Rules.FuryThresholds.Length; i++) if (Fury >= Rules.FuryThresholds[i]) lvl = i; return lvl; } }
        public int SlotsFor(int caught) => !Rules.SuccessiveFlies ? 1 : (caught >= Rules.ThreeFliesFromCatch ? 3 : (caught >= Rules.TwoFliesFromCatch ? 2 : 1));

        /// <summary>Only creates an attackId after validating state, arena, tool, fury, cooldown, global interval and single action.</summary>
        public ConfirmError TryConfirm(ToolDefinition tool, Vec2 target, out Attack attack)
        {
            attack = null;
            if (State != RoundState.Ready && State != RoundState.Playing) return ConfirmError.WrongState;
            if (target.X < 0 || target.X > Arena.W || target.Y < 0 || target.Y > Arena.H) return ConfirmError.OutsideArena;
            if (tool == null) return ConfirmError.ToolUnknown;
            if (Fury < tool.MinFury) return ConfirmError.FuryTooLow;
            if (CooldownRemainingMs(tool.Id) > 0) return ConfirmError.OnCooldown;
            if (SimMs - _lastConfirmSimMs < Rules.GlobalIntervalMs) return ConfirmError.GlobalInterval;
            if (ActiveAttack != null) return ConfirmError.AnotherAttackActive;
            float scale = Assisted ? Rules.AssistScale : 1f;
            attack = new Attack
            {
                Id = _nextAttackId++, Tool = tool, Target = target, ConfirmedAtSimMs = SimMs,
                PrepEndSimMs = SimMs + tool.PrepMs, ActiveEndSimMs = SimMs + tool.PrepMs + tool.ActiveMs,
                ScaledRadius = tool.Radius * scale, ScaledRectW = tool.RectW * scale, ScaledRectH = tool.RectH * scale,
                SpawnPoint = target, // rulesVersion 2: the object comes down over the target (grows in place) instead of coming from outside the plane
            };
            Attacks.Add(attack); _lastConfirmSimMs = SimMs; AttackCount++; LastToolId = tool.Id;
            if (State == RoundState.Ready) StartPlaying("first_attack");
            _validAttackActiveMs.Add(ActiveMs);
            Emit("attack_confirmed", $"{tool.Id} id={attack.Id} target={target}");
            return ConfirmError.None;
        }

        void StartPlaying(string why) { State = RoundState.Playing; Emit("round_started", why); }

        public void Pause() { if (State == RoundState.Ready || State == RoundState.Playing) { _pausedFrom = (int)State; State = RoundState.Paused; Emit("paused", ""); } }
        public void Resume() { if (State == RoundState.Paused) { State = (RoundState)_pausedFrom; _pausedFrom = -1; Emit("resumed", ""); } }
        public void Interrupt(string reason) { if (IsTerminal) return; State = RoundState.Interrupted; Outcome = MakeOutcome(); Emit("round_interrupted", reason); }
        public bool IsTerminal => State == RoundState.Won || State == RoundState.TimedOut || State == RoundState.Interrupted || State == RoundState.Result;
        public void EnterResult() { if (State == RoundState.Won || State == RoundState.TimedOut) State = RoundState.Result; }

        /// <summary>One 10 ms tick with a single fly (slot 0): center of the body at the start and at the end of the tick.</summary>
        public bool Tick(Vec2 flyPrev, Vec2 flyPos) { Flies[0].Prev = flyPrev; Flies[0].Pos = flyPos; return Tick(); }

        /// <summary>One 10 ms tick. Reads Prev/Pos of every active fly in Flies. Returns true if the round ended in this tick.</summary>
        public bool Tick()
        {
            if (State != RoundState.Ready && State != RoundState.Playing) return false;
            int dt = Rules.TickMs;
            SimMs += dt;
            if (State == RoundState.Ready)
            {
                ReadyMs += dt;
                if (ReadyMs >= Rules.ReadyMaxMs) StartPlaying("ready_timeout");
            }
            int tickStartActive = ActiveMs;
            if (State == RoundState.Playing) ActiveMs += dt;

            // advance attacks and resolve swept contacts per fly; sort by effective contact time
            _hits.Clear();
            foreach (var a in Attacks)
            {
                if (a.Phase == AttackPhase.Done) continue;
                if (a.Phase == AttackPhase.Preparing && SimMs >= a.PrepEndSimMs) a.Phase = AttackPhase.Active;
                if (a.Phase == AttackPhase.Active)
                {
                    foreach (var fly in Flies)
                    {
                        if (!fly.Active) continue;
                        float tHit; float edgeDist = SweptDistance(a, fly.Prev, fly.Pos, fly.Kind.ContactRadius, out tHit);
                        if (edgeDist < a.MinEdgeDistance) a.MinEdgeDistance = edgeDist;
                        if (tHit >= 0f && State == RoundState.Playing)
                        {
                            int contactActiveMs = tickStartActive + (int)Math.Round(tHit * dt);
                            int existing = -1; for (int i = 0; i < _hits.Count; i++) if (_hits[i].fly == fly) existing = i;
                            if (existing < 0) _hits.Add((fly, a, contactActiveMs)); else if (contactActiveMs < _hits[existing].atMs) _hits[existing] = (fly, a, contactActiveMs);
                        }
                    }
                }
                if (a.Phase == AttackPhase.Active && SimMs >= a.ActiveEndSimMs) { a.Phase = AttackPhase.Done; _cooldownUntilSimMs[a.Tool.Id] = SimMs + a.Tool.CooldownMs; }
            }
            if (Rules.SuccessiveFlies)
            {
                foreach (var h in _hits) if (h.atMs <= RoundEndMs) Catch(h.fly, h.attack, h.atMs);
            }
            else if (_hits.Count > 0 && _hits[0].atMs <= RoundEndMs)
            {
                var (fly, lethal, at) = _hits[0];
                lethal.Lethal = true; lethal.LethalAtActiveMs = at; ActiveMs = Math.Min(ActiveMs, Math.Max(at, tickStartActive));
                fly.Active = false; FliesCaught = 1; FastestCatchMs = at; Combo = 1; BestCombo = 1;
                State = RoundState.Won; Outcome = MakeOutcome(); Outcome.LethalAttackId = lethal.Id;
                foreach (var a in Attacks) if (a.Phase != AttackPhase.Done) a.Phase = AttackPhase.Done;
                Emit("round_completed", $"won attack={lethal.Id} t={at}");
                return true;
            }
            bool timeUp = State == RoundState.Playing && (ActiveMs >= RoundEndMs || (Rules.EndAtFury > 0 && Fury >= Rules.EndAtFury));
            // slots and entries (successive mode)
            if (Rules.SuccessiveFlies && !timeUp)
            {
                int slots = SlotsFor(FliesCaught);
                while (Flies.Count < slots) { Flies.Add(new FlyState { Slot = Flies.Count, Active = false, RespawnAtSimMs = SimMs + Rules.RespawnDelayMs }); Emit("slot_opened", $"slots={slots}"); }
                foreach (var fly in Flies)
                    if (!fly.Active && fly.RespawnAtSimMs >= 0 && SimMs >= fly.RespawnAtSimMs) Spawn(fly);
            }
            // misses: fury (only attacks ended without a lethal contact) and combo break
            foreach (var a in Attacks)
            {
                if (a.Phase != AttackPhase.Done || a.FuryApplied || a.Lethal) continue;
                a.FuryApplied = true;
                if (State != RoundState.Playing) continue;
                if (Combo > 0) Emit("combo_break", $"attack={a.Id} combo={Combo}");
                Combo = 0; AttackMissed?.Invoke(a);
                if (a.MinEdgeDistance <= Rules.FuryNearDistance && ActiveMs - _lastFuryMissActiveMs >= Rules.FuryMissCooldownMs)
                {
                    int gain = Rules.FuryMissGain + (a.MinEdgeDistance <= Rules.NearMissMargin ? Rules.FuryNearMissGain : 0);
                    AddFury(gain, $"miss attack={a.Id} d={a.MinEdgeDistance:0.000}"); _lastFuryMissActiveMs = ActiveMs;
                }
            }
            // activity bonus at every 5 s boundary of Playing
            if (State == RoundState.Playing && ActiveMs >= _nextBonusBoundaryMs)
            {
                int windowStart = _nextBonusBoundaryMs - Rules.FuryBonusPeriodMs; bool had = false;
                foreach (var t in _validAttackActiveMs) if (t >= windowStart && t < _nextBonusBoundaryMs) { had = true; break; }
                if (had) AddFury(Rules.FuryBonusGain, $"bonus@{_nextBonusBoundaryMs}");
                _nextBonusBoundaryMs += Rules.FuryBonusPeriodMs;
            }
            if (timeUp)
            {
                if (ActiveMs < RoundEndMs) RoundEndMs = ActiveMs; else ActiveMs = RoundEndMs;   // end by fury (bonus): closes now, without jumping the clock
                State = RoundState.TimedOut; Outcome = MakeOutcome();
                foreach (var a in Attacks) if (a.Phase != AttackPhase.Done) a.Phase = AttackPhase.Done; // cancel attacks that would only make contact later
                Emit("round_completed", "timed_out"); return true;
            }
            return false;
        }

        void Catch(FlyState fly, Attack lethal, int atMs)
        {
            if (!fly.Active) return;
            lethal.Lethal = true; lethal.LethalAtActiveMs = atMs; lethal.Phase = AttackPhase.Done; _cooldownUntilSimMs[lethal.Tool.Id] = SimMs + lethal.Tool.CooldownMs;
            int catchMs = atMs - (FliesCaught == 0 ? 0 : _lastCatchActiveMs);
            FliesCaught++; if (FastestCatchMs < 0 || catchMs < FastestCatchMs) FastestCatchMs = catchMs; _lastCatchActiveMs = atMs; CaughtByKind[(int)fly.Kind.Kind]++;
            Combo++; if (Combo > BestCombo) BestCombo = Combo;
            int bonus = fly.Kind.TimeBonusMs + Rules.ComboTimeBonusMs * Math.Min(Combo - 1, Rules.ComboTimeBonusCap);
            int before = RoundEndMs; RoundEndMs = Math.Min(Rules.MaxRoundMs, RoundEndMs + bonus); int granted = RoundEndMs - before; TimeBonusMs += granted;
            fly.Active = false; fly.RespawnAtSimMs = SimMs + Rules.RespawnDelayMs;
            Emit("fly_caught", $"#{FliesCaught} slot={fly.Slot} kind={fly.Kind.Id} attack={lethal.Id} t={atMs} combo={Combo} +{granted}ms");
            FlyCaught?.Invoke(fly, lethal.Id, atMs);
            if (granted > 0) TimeExtended?.Invoke(granted, Combo);
            UpdateObjectives(fly, lethal, catchMs);
            if (fly.Kind.Kind == FlyKind.Boss && !BossWasCaught) { BossWasCaught = true; RoundEndMs = Math.Min(RoundEndMs, ActiveMs + Rules.BossEndMs); Emit("boss_caught", ""); BossCaught?.Invoke(); }
        }

        void UpdateObjectives(FlyState fly, Attack lethal, int catchMs)
        {
            if (Scene == null) return;
            for (int i = 0; i < Scene.Objectives.Length; i++)
            {
                if (ObjectiveDone[i]) continue;
                var o = Scene.Objectives[i];
                switch (o.Kind)
                {
                    case ObjectiveKind.CatchN: ObjectiveProgress[i] = FliesCaught; break;
                    case ObjectiveKind.CatchKind: if (fly.Kind.Kind == o.FlyKind) ObjectiveProgress[i]++; break;
                    case ObjectiveKind.ComboN: ObjectiveProgress[i] = Math.Max(ObjectiveProgress[i], Combo); break;
                    case ObjectiveKind.CatchWithRole: if (lethal.Tool.Role == o.Role) ObjectiveProgress[i]++; break;
                    case ObjectiveKind.CatchOnFood: foreach (var f in Scene.Food) if ((f.Pos - fly.Pos).Length <= f.Radius + 0.05f) { ObjectiveProgress[i]++; break; } break;
                    case ObjectiveKind.FastCatch: if (catchMs <= o.Ms) ObjectiveProgress[i] = 1; break;
                }
                if (ObjectiveProgress[i] >= o.N) { ObjectiveDone[i] = true; Emit("objective_done", o.Id); ObjectiveCompleted?.Invoke(i); }
            }
        }

        void Spawn(FlyState fly)
        {
            fly.Index = _spawned++; fly.Active = true; fly.RespawnAtSimMs = -1; fly.EnteredSimMs = SimMs;
            fly.Seed = Seed ^ ((ulong)(fly.Index + 1) * 0x9E3779B97F4A7C15UL) ^ ((ulong)fly.Slot * 0xD1B54A32D192ED03UL);
            fly.Kind = ChooseKind(fly.Seed, fly.Index);
            Emit("fly_spawned", $"#{fly.Index} slot={fly.Slot} kind={fly.Kind.Id}"); FlySpawned?.Invoke(fly);
        }

        /// <summary>Deterministic kind by seed and index: bluebottle from BluebottleFromIndex, fruit fly from FruitFlyFromIndex.</summary>
        public FlyKindDef ChooseKind(ulong seed, int index)
        {
            if (index == Rules.BossAtIndex) return FlyKinds.Boss;
            if (index >= Rules.GoldenFromIndex && Hash01(seed ^ 0x601DF17UL) < Rules.GoldenChance) return FlyKinds.Golden;   // ADR-016: its own draw (does not change the others)
            double u = Hash01(seed ^ 0x5EEDF1EEUL);
            if (index >= Rules.HorseflyFromIndex && u >= 1.0 - Rules.HorseflyChance) return FlyKinds.Horsefly;   // its own band at the top: does not change the choices of the previous indices
            if (index >= Rules.FruitFlyFromIndex && u < Rules.FruitFlyChance) return FlyKinds.FruitFly;
            if (index >= Rules.BluebottleFromIndex && u < Rules.FruitFlyChance + Rules.BluebottleChance) return FlyKinds.Bluebottle;
            return FlyKinds.House;
        }

        static double Hash01(ulong x)
        {
            x += 0x9E3779B97F4A7C15UL; x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL; x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL; x ^= x >> 31;
            return (x >> 11) * (1.0 / 9007199254740992.0);
        }

        void AddFury(int gain, string why) { int before = Fury; Fury = Math.Min(100, Fury + gain); if (Fury != before) Emit("fury", $"+{Fury - before} {why} -> {Fury}"); }
        RoundOutcome MakeOutcome() => new RoundOutcome { Terminal = State, ActiveMs = ActiveMs, Attacks = AttackCount, Assisted = Assisted, LastToolId = LastToolId, Fury = Fury, FliesCaught = FliesCaught, FastestCatchMs = FastestCatchMs, BestCombo = BestCombo, TimeBonusMs = TimeBonusMs, RoundEndMs = RoundEndMs, BossCaught = BossWasCaught, CaughtByKind = (int[])CaughtByKind.Clone(), ObjectivesDone = ObjectiveDone == null ? new bool[0] : (bool[])ObjectiveDone.Clone(), ObjectiveProgress = ObjectiveProgress == null ? new int[0] : (int[])ObjectiveProgress.Clone() };
        void Emit(string type, string detail) => Events.Add(new RoundEvent { Type = type, ActiveMs = ActiveMs, Detail = detail });

        /// <summary>Minimum distance between the edge of the fly's body (swept segment) and the edge of the active area. tHit ∈ [0,1] if there was contact (distance ≤ 0), otherwise −1.</summary>
        static float SweptDistance(Attack a, Vec2 p0, Vec2 p1, float bodyRadius, out float tHit)
        {
            tHit = -1f; float best = float.MaxValue; const int steps = 8;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps; Vec2 p = Vec2.Lerp(p0, p1, t);
                float d = (a.Tool.Shape == ToolShape.Circle ? (p - a.Target).Length - a.ScaledRadius : RectDistance(p, a.Target, a.ScaledRectW, a.ScaledRectH)) - bodyRadius;
                if (d < best) best = d;
                if (d <= 0f && tHit < 0f) tHit = t;
            }
            return Math.Max(0f, best);
        }
        static float RectDistance(Vec2 p, Vec2 c, float w, float h)
        {
            float dx = Math.Max(Math.Abs(p.X - c.X) - w * 0.5f, 0f), dy = Math.Max(Math.Abs(p.Y - c.Y) - h * 0.5f, 0f);
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
