// Generates the art of the extra scenes (ADR-011): 1200×1600 backgrounds with the "behind" painted, tools (fast 512, medium 512, giant 768×332),
// "at home" sprites of the giants, 128 icons and four 256 decals per scene. Own work; same style rules (ink outline, flat colours).
// Usage: node tools/art/scenes.mjs && node tools/art/export.mjs
import fs from "node:fs";
import path from "node:path";
const root = path.resolve(path.dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, "$1")), "..", "..");
const src = path.join(root, "art", "svg");
const INK = "#202622", PAPER = "#F7F0DE", ACTION = "#F4CD3C", FURY = "#AF3D2F", INFO = "#2755A5", SAGE = "#CBD3B9", WOOD = "#B98F55", WOOD2 = "#8F6A38", GRASS = "#8DB27A", GRASS2 = "#5F8A55", SKY = "#BFD7E6";
const svg = (w, h, body) => `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">${body}</svg>`;
const bg = (body) => svg(1200, 1600, body);
const st = (w = 6) => `stroke="${INK}" stroke-width="${w}" stroke-linejoin="round" stroke-linecap="round"`;
const rect = (x, y, w, h, fill, r = 0, sw = 6) => `<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${r}" fill="${fill}" ${st(sw)}/>`;
const circ = (cx, cy, r, fill, sw = 6) => `<circle cx="${cx}" cy="${cy}" r="${r}" fill="${fill}" ${st(sw)}/>`;
const ell = (cx, cy, rx, ry, fill, sw = 6) => `<ellipse cx="${cx}" cy="${cy}" rx="${rx}" ry="${ry}" fill="${fill}" ${st(sw)}/>`;
const path_ = (d, fill, sw = 6, extra = "") => `<path d="${d}" fill="${fill}" ${st(sw)} ${extra}/>`;
const line = (d, color = INK, w = 6) => `<path d="${d}" fill="none" stroke="${color}" stroke-width="${w}" stroke-linecap="round"/>`;
const web = (x, y, flip) => { const s = flip ? -1 : 1; return `<g stroke="#555E55" stroke-width="3" fill="none" stroke-opacity="0.8"><path d="M${x} ${y} l${100 * s} 0 M${x} ${y} l0 100 M${x} ${y} l${100 * s} 100 M${x} ${y} l${60 * s} 30 M${x} ${y} l${40 * s} 70"/><path d="M${x + 15 * s} ${y + 20} q${20 * s} -10 ${40 * s} 20 q${-10 * s} 25 ${-35 * s} 30 q${-15 * s} -25 ${-5 * s} -50"/></g>`; };
const dust = (x, y) => `<circle cx="${x}" cy="${y}" r="14" fill="#8F8A76" fill-opacity="0.5"/><circle cx="${x + 40}" cy="${y + 30}" r="9" fill="#8F8A76" fill-opacity="0.5"/>`;
const write = (name, s) => fs.writeFileSync(path.join(src, name + ".svg"), s);

// ---------- generic tools per family ----------
const fastRound = (inner) => svg(512, 512, `<g ${st(12)}>${inner}</g>`);
const giant = (inner) => svg(768, 332, `<g ${st(12)}>${inner}</g>`);
const icon = (inner) => svg(128, 128, inner);

// =====================================================================================
// BATHROOM - toilet on the right (lid = giant), plunger next to it (medium), roll on its holder (fast)
// homes (arena, y from the bottom): roll (0.16, 0.66) w0.10 · plunger (0.60, 0.30) w0.16 · lid (0.80, 0.30) w0.22
write("bath_bg", bg(`
  <defs><pattern id="bt" width="150" height="150" patternUnits="userSpaceOnUse"><rect width="150" height="150" fill="#DCE6EA"/><path d="M0 0 H150 M0 0 V150" stroke="#C4D2D8" stroke-width="6"/></pattern></defs>
  <rect width="1200" height="1150" fill="url(#bt)"/>
  <rect x="0" y="1000" width="1200" height="60" fill="${INFO}" fill-opacity="0.5"/><path d="M0 1000 H1200 M0 1060 H1200" stroke="${INK}" stroke-width="6"/>
  <rect x="0" y="1150" width="1200" height="450" fill="#EDE6D2"/><path d="M0 1150 H1200" stroke="${INK}" stroke-width="6"/>
  <path d="M0 1300 H1200 M0 1450 H1200 M300 1150 V1600 M600 1150 V1600 M900 1150 V1600" stroke="#D8D0BB" stroke-width="5"/>
  ${rect(120, 160, 360, 420, "#BFD7E6", 14, 8)}<path d="M150 200 L300 200 L150 380 Z" fill="#FFFFFF" fill-opacity="0.35"/>
  ${rect(100, 600, 400, 40, "#F7F0DE", 8)}${circ(140, 620, 9, INK, 0)}${circ(300, 620, 9, INK, 0)}${rect(220, 540, 60, 60, "#5F8A55", 8)}${rect(300, 560, 40, 40, ACTION, 6)}
  <!-- bathtub at the bottom left -->
  ${path_("M60 1250 q0 -70 70 -70 H620 q70 0 70 70 V1420 q0 40 -40 40 H100 q-40 0 -40 -40 Z", "#FFFFFF", 8)}
  ${path_("M100 1200 H660 q40 0 40 40 v20 H60 v-20 q0 -40 40 -40 Z", "#F7F0DE", 6)}
  ${circ(120, 1490, 20, "#8C948C")}${circ(640, 1490, 20, "#8C948C")}${line("M560 1140 v40 M560 1140 q40 0 40 40", "#8C948C", 12)}
  <!-- washbasin on the right with a small mirror -->
  ${rect(760, 700, 220, 40, "#FFFFFF", 10)}${path_("M760 740 H980 L940 860 H800 Z", "#FFFFFF", 6)}${line("M870 660 v40", "#8C948C", 12)}${rect(840, 640, 60, 24, "#8C948C", 8)}
  ${rect(800, 780, 40, 26, "#F7F0DE", 6)}${rect(846, 776, 60, 30, INFO, 8)}
  <!-- home of the toilet (the lid is an object): the open toilet with the rubber duck -->
  ${path_("M860 1150 q0 -60 60 -60 H1100 q60 0 60 60 V1320 q0 40 -40 40 H900 q-40 0 -40 -40 Z", "#FFFFFF", 8)}
  ${ell(980, 1200, 100, 60, "#BFD7E6", 6)}${ell(980, 1200, 70, 40, INFO, 0)}
  ${path_("M955 1190 q10 -30 40 -25 q25 5 22 30 q-5 20 -32 20 q-28 -5 -30 -25 Z", ACTION, 5)}${circ(1000, 1180, 4, INK, 0)}${path_("M1012 1185 l14 6 l-14 6 Z", "#E86A2C", 3)}
  ${rect(1040, 1000, 120, 100, "#FFFFFF", 12)}${rect(1080, 1010, 40, 20, "#8C948C", 6)}
  ${web(1160, 1000, true)}
  <!-- roll holder (empty) and the plunger's wet mark -->
  ${line("M170 860 h40 M190 860 v-30", INK, 8)}${circ(190, 820, 8, INK, 0)}
  ${ell(720, 1420, 60, 22, "#BFD7E6", 4)}
  ${rect(0, 1560, 1200, 40, "#555E55", 0, 0)}
`));
write("tool_paper", fastRound(`${circ(256, 256, 236, PAPER)}${circ(256, 256, 70, "#D8D0BB")}${circ(256, 256, 40, "#EDE6D2")}<path d="M256 20 a236 236 0 0 1 236 236" fill="none" stroke="#D8D0BB" stroke-width="18"/><path d="M256 40 v150" stroke="#D8D0BB" stroke-width="8"/>`));
write("tool_plunger", fastRound(`${circ(256, 256, 236, FURY)}${circ(256, 256, 150, "#8E2F24")}${circ(256, 256, 60, WOOD)}${circ(256, 256, 30, WOOD2)}<path d="M120 170 Q200 100 300 110" fill="none" stroke="${PAPER}" stroke-width="14" stroke-opacity="0.4"/>`));
write("tool_lid", giant(`${ell(384, 166, 372, 150, "#FFFFFF")}${ell(384, 166, 300, 110, "#F7F0DE", 8)}<path d="M120 100 Q384 40 650 100" fill="none" stroke="${PAPER}" stroke-width="16" stroke-opacity="0.8"/>`));
write("home_lid", svg(300, 220, `<g ${st(8)}>${ell(150, 110, 140, 100, "#FFFFFF")}${ell(150, 110, 100, 70, "#F7F0DE", 6)}</g>`));
write("icon_paper", icon(`${circ(64, 64, 52, PAPER, 7)}${circ(64, 64, 18, "#D8D0BB", 5)}`));
write("icon_plunger", icon(`${circ(64, 80, 40, FURY, 7)}${rect(56, 8, 16, 60, WOOD, 6, 6)}`));
write("icon_lid", icon(`${ell(64, 64, 56, 36, "#FFFFFF", 7)}${ell(64, 64, 34, 20, "#F7F0DE", 5)}`));
write("bath_dmg_1", svg(256, 256, `<path d="M128 40 C170 50 210 80 205 120 C225 150 190 185 160 195 C130 215 90 205 65 180 C35 160 40 120 65 100 C85 70 100 46 128 40 Z" fill="${INFO}" fill-opacity="0.45" ${st(5)}/>${circ(215, 155, 12, INFO, 4)}`));
write("bath_dmg_2", svg(256, 256, `<g ${st(4)}><path d="M40 120 l60 -30 l30 40 l-50 30 Z" fill="${PAPER}"/><path d="M130 60 l50 10 l-10 50 l-50 -10 Z" fill="${PAPER}"/><path d="M110 170 l60 -10 l20 40 l-70 10 Z" fill="${PAPER}"/></g>`));
write("bath_dmg_3", svg(256, 256, `<g stroke="${INK}" stroke-width="5" fill="none" stroke-linecap="round"><path d="M128 128 l-40 -50 l-10 -30 M128 128 l50 -30 l40 -10 M128 128 l10 60 l-20 30 M128 128 l-60 30"/></g>`));
write("bath_dmg_4", svg(256, 256, `${ell(128, 128, 90, 50, "#DCE6EA", 4)}${ell(120, 122, 40, 18, "#FFFFFF", 0)}<circle cx="200" cy="100" r="10" fill="#FFFFFF" ${st(3)}/><circle cx="60" cy="150" r="7" fill="#FFFFFF" ${st(3)}/>`));

// =====================================================================================
// LIVING ROOM - sofa at the bottom left (giant), cushion on the sofa (medium), newspaper on the table (fast)
// homes: newspaper (0.62, 0.36) w0.11 · cushion (0.30, 0.34) w0.16 · sofa (0.30, 0.22) w0.50
write("living_bg", bg(`
  <rect width="1200" height="1150" fill="#E9DDC4"/>
  <path d="M0 0 H1200 M0 120 H1200 M0 240 H1200 M0 360 H1200 M0 480 H1200 M0 600 H1200 M0 720 H1200 M0 840 H1200 M0 960 H1200" stroke="#DCCDB0" stroke-width="10"/>
  <rect x="0" y="1150" width="1200" height="450" fill="#C9A36A"/><path d="M0 1150 H1200" stroke="${INK}" stroke-width="6"/><path d="M0 1260 H1200 M0 1370 H1200 M0 1480 H1200" stroke="#B98F55" stroke-width="5"/>
  ${rect(120, 140, 300, 220, "#F7F0DE", 6, 8)}${rect(150, 170, 240, 160, SKY, 0, 4)}${path_("M160 320 Q250 220 380 320 Z", GRASS2, 4)}${circ(340, 220, 24, ACTION, 4)}
  ${rect(760, 120, 340, 460, "#BFD7E6", 10, 8)}<path d="M930 120 V580 M760 350 H1100" stroke="${INK}" stroke-width="8"/>
  ${path_("M740 100 q-20 250 0 500 H790 q-30 -250 0 -500 Z", FURY, 6)}${path_("M1120 100 q20 250 0 500 H1070 q30 -250 0 -500 Z", FURY, 6)}
  ${rect(560, 700, 60, 300, WOOD2, 6)}${path_("M520 560 H680 L660 700 H540 Z", ACTION, 6)}
  <!-- TV at the bottom right -->
  ${rect(800, 1080, 360, 100, WOOD, 8)}${rect(820, 780, 320, 280, INK, 12, 8)}${rect(840, 800, 280, 240, "#3E6FC4", 6, 0)}${path_("M860 820 L1000 820 L860 960 Z", "#FFFFFF", 0)}
  <!-- home of the sofa (object): rug with a mark, dust, lost remote, coins, a sock -->
  ${rect(60, 1180, 620, 340, "#B98F55", 16, 0)}${rect(80, 1200, 580, 300, "#D9B27A", 12, 0)}
  ${dust(200, 1300)}${dust(500, 1250)}
  ${rect(300, 1420, 120, 40, INK, 8, 4)}${circ(330, 1440, 8, FURY, 0)}${circ(360, 1440, 8, "#FFFFFF", 0)}${circ(390, 1440, 8, "#FFFFFF", 0)}
  ${circ(560, 1380, 16, ACTION, 4)}${circ(600, 1400, 12, ACTION, 4)}
  ${path_("M120 1400 q30 -40 60 -10 q10 30 -20 40 q-30 0 -40 -30 Z", INFO, 5)}
  <!-- coffee table with a popcorn bowl and glass marks -->
  ${ell(720, 1240, 190, 70, WOOD, 7)}${rect(700, 1250, 40, 200, WOOD2, 6)}
  ${path_("M600 1215 q60 -60 120 0 Z", FURY, 6)}${circ(640, 1170, 14, PAPER, 4)}${circ(665, 1160, 14, PAPER, 4)}${circ(690, 1172, 14, PAPER, 4)}
  ${circ(820, 1235, 26, "none", 4)}${circ(850, 1250, 20, "none", 4)}
  ${web(0, 0, false)}
  ${rect(0, 1560, 1200, 40, "#555E55", 0, 0)}
`));
write("tool_newspaper", fastRound(`${circ(256, 256, 236, PAPER)}<path d="M256 256 m-180 0 a180 180 0 1 1 360 0 a140 140 0 1 1 -280 0 a100 100 0 1 1 200 0 a60 60 0 1 1 -120 0" fill="none" stroke="#8C948C" stroke-width="14"/><path d="M120 140 h100 M140 180 h60 M300 330 h90 M320 370 h50" stroke="${INK}" stroke-width="10"/>`));
write("tool_cushion", fastRound(`<path d="M60 60 Q256 20 452 60 Q492 256 452 452 Q256 492 60 452 Q20 256 60 60 Z" fill="${INFO}"/><path d="M110 110 Q256 80 402 110 Q432 256 402 402 Q256 432 110 402 Q80 256 110 110 Z" fill="none" stroke="${PAPER}" stroke-width="10" stroke-opacity="0.6"/>${circ(256, 256, 22, ACTION, 8)}`));
write("tool_sofa", giant(`${rect(10, 40, 748, 282, FURY, 40)}${rect(60, 90, 300, 190, "#C9553F", 24, 8)}${rect(408, 90, 300, 190, "#C9553F", 24, 8)}<path d="M10 120 H758" fill="none" stroke="${INK}" stroke-width="10"/>`));
write("home_sofa", svg(640, 340, `<g ${st(8)}>${rect(10, 90, 620, 220, FURY, 30)}${rect(10, 40, 90, 200, "#C9553F", 24)}${rect(540, 40, 90, 200, "#C9553F", 24)}${rect(100, 110, 210, 120, "#C9553F", 20, 6)}${rect(330, 110, 210, 120, "#C9553F", 20, 6)}${rect(60, 300, 40, 30, WOOD2, 6, 6)}${rect(540, 300, 40, 30, WOOD2, 6, 6)}</g>`));
write("icon_newspaper", icon(`${circ(64, 64, 52, PAPER, 7)}<path d="M64 64 m-36 0 a36 36 0 1 1 72 0 a24 24 0 1 1 -48 0" fill="none" stroke="#8C948C" stroke-width="8"/>`));
write("icon_cushion", icon(`<path d="M20 20 Q64 10 108 20 Q118 64 108 108 Q64 118 20 108 Q10 64 20 20 Z" fill="${INFO}" ${st(7)}/>${circ(64, 64, 8, ACTION, 4)}`));
write("icon_sofa", icon(`${rect(10, 44, 108, 60, FURY, 12, 7)}${rect(10, 30, 22, 50, "#C9553F", 8, 5)}${rect(96, 30, 22, 50, "#C9553F", 8, 5)}`));
write("living_dmg_1", svg(256, 256, `${ell(128, 128, 96, 60, "#8F6A38", 4)}${ell(128, 128, 60, 34, "#B98F55", 0)}`));
write("living_dmg_2", svg(256, 256, `<g ${st(4)}>${circ(90, 110, 16, PAPER)}${circ(130, 90, 16, PAPER)}${circ(160, 130, 16, PAPER)}${circ(110, 160, 16, PAPER)}${circ(190, 170, 12, PAPER)}${circ(60, 160, 12, PAPER)}</g>`));
write("living_dmg_3", svg(256, 256, `<g ${st(4)}><path d="M60 80 l90 -20 l20 30 l-30 60 l-70 10 Z" fill="${PAPER}"/><path d="M80 100 h50 M85 120 h40 M90 140 h30" stroke="#8C948C" stroke-width="5"/></g>`));
write("living_dmg_4", svg(256, 256, `${circ(128, 128, 60, "none", 8)}${circ(128, 128, 48, "none", 4)}<circle cx="128" cy="128" r="60" fill="none" stroke="#8F6A38" stroke-width="10" stroke-opacity="0.6"/>`));

// =====================================================================================
// BEACH - parasol on the right (giant), ball on the sand (medium), flip-flop on the towel (fast)
// homes: flip-flop (0.30, 0.30) w0.10 · ball (0.70, 0.62) w0.16 · parasol (0.78, 0.30) w0.30 (lying)
write("beach_bg", bg(`
  <rect width="1200" height="520" fill="${SKY}"/>${circ(200, 130, 70, ACTION, 6)}
  <g fill="${PAPER}" ${st(6)}><path d="M700 200 q20 -50 70 -40 q30 -40 80 -10 q60 0 55 45 q40 15 15 40 H680 q-20 -30 20 -35 Z"/></g>
  <rect x="0" y="520" width="1200" height="260" fill="#3E6FC4"/><path d="M0 520 H1200" stroke="${INK}" stroke-width="6"/>
  <path d="M0 600 q60 -20 120 0 t120 0 t120 0 t120 0 t120 0 t120 0 t120 0 t120 0 t120 0 t120 0" fill="none" stroke="${PAPER}" stroke-width="8" stroke-opacity="0.8"/>
  <path d="M0 700 q60 -20 120 0 t120 0 t120 0 t120 0 t120 0 t120 0 t120 0 t120 0 t120 0 t120 0" fill="none" stroke="${PAPER}" stroke-width="8" stroke-opacity="0.6"/>
  <path d="M0 780 q100 -30 200 0 t200 0 t200 0 t200 0 t200 0 t200 0 V1600 H0 Z" fill="#E9C77B"/><path d="M0 780 q100 -30 200 0 t200 0 t200 0 t200 0 t200 0 t200 0" fill="none" stroke="${INK}" stroke-width="6"/>
  <g fill="${INK}" stroke="none"><circle cx="90" cy="900" r="4"/><circle cx="1100" cy="1000" r="4"/><circle cx="160" cy="1500" r="4"/><circle cx="1000" cy="1480" r="4"/><circle cx="560" cy="860" r="4"/></g>
  <!-- striped towel -->
  ${rect(120, 1080, 620, 360, "#FFFFFF", 12, 8)}<path d="M120 1140 H740 M120 1220 H740 M120 1300 H740 M120 1380 H740" stroke="${INFO}" stroke-width="30"/>
  <!-- cooler and sandcastle -->
  ${rect(800, 1300, 200, 150, INFO, 14, 7)}${rect(800, 1300, 200, 40, "#FFFFFF", 10, 6)}${rect(870, 1290, 60, 18, "#8C948C", 6, 4)}
  ${path_("M860 1150 h120 v-40 h-30 v20 h-30 v-20 h-30 Z", "#D9B27A", 6)}${path_("M880 1110 v-40 l30 20 Z", FURY, 4)}
  <!-- home of the parasol (object): pale shadow on the sand and a crab; ball mark; flip-flop footprint -->
  ${ell(940, 940, 170, 60, "#F7F0DE", 0)}${ell(940, 940, 170, 60, "none", 4)}
  ${path_("M980 960 q-10 -30 30 -30 q40 0 30 30 Z", FURY, 4)}${line("M985 940 l-20 -15 M1035 940 l20 -15 M990 965 l-15 15 M1030 965 l15 15", INK, 4)}${circ(1000, 945, 3, INK, 0)}${circ(1020, 945, 3, INK, 0)}
  ${circ(840, 1180, 60, "#D9B27A", 0)}${circ(840, 1180, 60, "none", 4)}
  ${path_("M340 1240 q-20 -60 20 -70 q40 0 30 60 q5 30 -25 34 q-25 0 -25 -24 Z", "#D9B27A", 4)}
  <!-- ice cream and Berlin doughnut on the towel -->
  ${path_("M620 1250 l30 -80 l30 80 Z", "#D9B27A", 5)}${circ(650, 1160, 34, "#F4A7B9", 5)}${circ(650, 1130, 30, PAPER, 5)}
  ${ell(240, 1300, 60, 40, "#E9C77B", 5)}${path_("M190 1300 h100", "#F7F0DE", 8)}<circle cx="215" cy="1290" r="5" fill="${PAPER}"/><circle cx="260" cy="1285" r="5" fill="${PAPER}"/>
  ${rect(0, 1560, 1200, 40, "#D9B27A", 0, 0)}
`));
write("tool_flipflop", fastRound(`<path d="M80 60 Q256 20 432 60 Q470 256 432 452 Q256 492 80 452 Q40 256 80 60 Z" fill="${ACTION}"/><path d="M256 150 Q200 230 150 260 M256 150 Q312 230 362 260" fill="none" stroke="${FURY}" stroke-width="18"/>${circ(256, 150, 14, FURY, 6)}`));
write("tool_ball", fastRound(`${circ(256, 256, 236, "#FFFFFF")}<path d="M256 20 q-150 236 0 472 M256 20 q150 236 0 472" fill="${FURY}" stroke="${INK}" stroke-width="8"/><path d="M20 256 q236 -150 472 0 q-236 150 -472 0" fill="${INFO}" stroke="${INK}" stroke-width="8"/>${circ(256, 256, 236, "none")}`));
write("tool_umbrella", giant(`<path d="M20 166 L700 60 q40 -8 48 30 L740 190 q6 30 -30 40 L60 250 q-40 8 -48 -30 Z" fill="${FURY}"/><path d="M60 170 L700 70 M60 250 L720 140" fill="none" stroke="${PAPER}" stroke-width="16" stroke-opacity="0.7"/>${rect(700, 150, 60, 40, WOOD, 8, 8)}`));
write("home_umbrella", svg(400, 620, `<g ${st(8)}><path d="M20 260 q180 -260 360 0 Z" fill="${FURY}"/><path d="M110 260 q90 -200 180 0 M60 260 q140 -120 280 0" fill="none" stroke="${PAPER}" stroke-width="12" stroke-opacity="0.7"/>${rect(190, 260, 20, 340, WOOD, 6, 6)}</g>`));
write("icon_flipflop", icon(`<path d="M30 16 Q64 8 98 16 Q108 64 98 112 Q64 120 30 112 Q20 64 30 16 Z" fill="${ACTION}" ${st(7)}/><path d="M64 40 Q50 60 38 68 M64 40 Q78 60 90 68" fill="none" stroke="${FURY}" stroke-width="6"/>`));
write("icon_ball", icon(`${circ(64, 64, 52, "#FFFFFF", 7)}<path d="M64 12 q-30 52 0 104 M64 12 q30 52 0 104" fill="${FURY}" stroke="${INK}" stroke-width="4"/>`));
write("icon_umbrella", icon(`<path d="M12 60 q52 -70 104 0 Z" fill="${FURY}" ${st(7)}/>${rect(60, 60, 8, 56, WOOD, 4, 4)}`));
write("beach_dmg_1", svg(256, 256, `${ell(128, 128, 100, 60, "#D9B27A", 0)}<g fill="${INK}"><circle cx="90" cy="110" r="4"/><circle cx="150" cy="140" r="4"/><circle cx="120" cy="160" r="4"/></g>`));
write("beach_dmg_2", svg(256, 256, `${ell(128, 128, 96, 56, INFO, 0)}<ellipse cx="128" cy="128" rx="96" ry="56" fill="${INFO}" fill-opacity="0.4"/>`));
write("beach_dmg_3", svg(256, 256, `<path d="M128 50 C170 60 210 80 205 120 C225 150 190 185 160 195 C130 215 90 205 65 180 C35 160 40 120 65 100 C85 70 100 46 128 50 Z" fill="#F4A7B9" fill-opacity="0.85" ${st(5)}/>${circ(215, 155, 12, "#F4A7B9", 4)}`));
write("beach_dmg_4", svg(256, 256, `<path d="M40 180 q40 -60 90 -20 q40 30 90 -30" fill="none" stroke="${GRASS2}" stroke-width="12" stroke-linecap="round"/><path d="M70 120 q30 -40 60 -10" fill="none" stroke="${GRASS2}" stroke-width="8" stroke-linecap="round"/>`));

// =====================================================================================
// CAFÉ TERRACE - umbrella on the right (giant), tray on the table (medium), menu on the table (fast)
// homes: menu (0.36, 0.47) w0.10 · tray (0.62, 0.44) w0.16 · umbrella (0.80, 0.30) w0.30 (lying)
write("cafe_bg", bg(`
  <rect width="1200" height="1150" fill="#E9DDC4"/>
  <path d="M0 0 H1200 V220 q-60 60 -120 0 q-60 60 -120 0 q-60 60 -120 0 q-60 60 -120 0 q-60 60 -120 0 q-60 60 -120 0 q-60 60 -120 0 q-60 60 -120 0 q-60 60 -120 0 q-60 60 -120 0 Z" fill="${FURY}" ${st(6)}/>
  <path d="M120 0 V240 M360 0 V240 M600 0 V240 M840 0 V240 M1080 0 V240" stroke="${PAPER}" stroke-width="60" stroke-opacity="0.85"/>
  ${rect(80, 360, 300, 380, INK, 10, 8)}<path d="M120 420 h220 M120 480 h160 M120 540 h200 M120 600 h120 M120 660 h180" stroke="${PAPER}" stroke-width="10" stroke-opacity="0.8"/>
  ${rect(520, 320, 220, 830, WOOD2, 8, 8)}${rect(540, 340, 180, 790, WOOD, 6, 4)}${circ(700, 760, 12, ACTION, 4)}
  ${rect(0, 1150, 1200, 450, "#B0AEA3")}<path d="M0 1150 H1200" stroke="${INK}" stroke-width="6"/><path d="M0 1300 H1200 M0 1450 H1200 M200 1150 V1600 M600 1150 V1600 M1000 1150 V1600" stroke="#9A988D" stroke-width="5"/>
  <!-- round table with an espresso, a custard tart and a glass; chairs -->
  ${ell(420, 1200, 300, 90, "#FFFFFF", 8)}${rect(400, 1250, 40, 250, "#8C948C", 6)}${path_("M330 1500 h180 v40 h-180 Z", "#8C948C", 6)}
  ${rect(80, 1120, 120, 220, INK, 12, 6)}${rect(100, 1140, 80, 90, "#F7F0DE", 8, 4)}${rect(760, 1120, 120, 220, INK, 12, 6)}${rect(780, 1140, 80, 90, "#F7F0DE", 8, 4)}
  ${path_("M300 1170 h70 v40 q0 20 -20 20 h-30 q-20 0 -20 -20 Z", "#FFFFFF", 5)}${path_("M370 1180 q30 5 25 30 q-5 15 -25 12", "none", 5)}${ell(335, 1172, 30, 8, "#6B4E28", 0)}
  ${circ(480, 1190, 34, "#E9C77B", 5)}${circ(480, 1190, 22, "#F4CD3C", 0)}${path_("M470 1180 q10 -8 20 0", "#8F6A38", 4)}
  ${rect(560, 1130, 40, 70, "#BFD7E6", 6, 5)}
  <!-- home of the umbrella (object): base of the pole and a pigeon -->
  ${ell(960, 1160, 90, 30, "#8C948C", 6)}${rect(950, 1040, 20, 120, "#8C948C", 6, 5)}
  ${ell(1080, 1210, 40, 26, "#8C948C", 5)}${circ(1115, 1195, 16, "#8C948C", 5)}${path_("M1128 1195 l14 4 l-14 4 Z", ACTION, 3)}${circ(1120, 1191, 3, INK, 0)}${line("M1070 1236 v16 M1090 1236 v16", FURY, 4)}
  ${circ(600, 1240, 26, "none", 4)}<g fill="${ACTION}" stroke="${INK}" stroke-width="3"><circle cx="360" cy="1230" r="5"/><circle cx="345" cy="1245" r="4"/><circle cx="380" cy="1250" r="4"/></g>
  ${rect(0, 1560, 1200, 40, "#555E55", 0, 0)}
`));
write("tool_menu", fastRound(`<path d="M70 70 H442 V442 H70 Z" fill="${PAPER}"/><path d="M256 70 V442" stroke="${INK}" stroke-width="8"/><path d="M110 130 h100 M110 180 h80 M110 230 h100 M300 130 h100 M300 180 h70 M300 230 h100 M300 280 h60" stroke="${INK}" stroke-width="10"/><path d="M110 330 h100 v60 h-100 Z" fill="${FURY}" stroke="none"/>`));
write("tool_tray", fastRound(`${circ(256, 256, 236, "#8C948C")}${circ(256, 256, 190, "#B0AEA3", 8)}<path d="M120 170 Q200 100 300 110" fill="none" stroke="${PAPER}" stroke-width="14" stroke-opacity="0.6"/>`));
write("tool_parasol", giant(`<path d="M20 166 L700 60 q40 -8 48 30 L740 190 q6 30 -30 40 L60 250 q-40 8 -48 -30 Z" fill="${INFO}"/><path d="M60 170 L700 70 M60 250 L720 140" fill="none" stroke="${PAPER}" stroke-width="16" stroke-opacity="0.7"/>${rect(700, 150, 60, 40, "#8C948C", 8, 8)}`));
write("home_parasol", svg(400, 620, `<g ${st(8)}><path d="M20 260 q180 -260 360 0 Z" fill="${INFO}"/><path d="M110 260 q90 -200 180 0 M60 260 q140 -120 280 0" fill="none" stroke="${PAPER}" stroke-width="12" stroke-opacity="0.7"/>${rect(190, 260, 20, 340, "#8C948C", 6, 6)}</g>`));
write("icon_menu", icon(`${rect(22, 16, 84, 96, PAPER, 6, 7)}<path d="M64 16 V112 M34 40 h20 M34 60 h16 M74 40 h20 M74 60 h16" stroke="${INK}" stroke-width="5"/>`));
write("icon_tray", icon(`${circ(64, 64, 52, "#8C948C", 7)}${circ(64, 64, 38, "#B0AEA3", 5)}`));
write("icon_parasol", icon(`<path d="M12 60 q52 -70 104 0 Z" fill="${INFO}" ${st(7)}/>${rect(60, 60, 8, 56, "#8C948C", 4, 4)}`));
write("cafe_dmg_1", svg(256, 256, `<path d="M128 50 C170 60 210 80 205 120 C225 150 190 185 160 195 C130 215 90 205 65 180 C35 160 40 120 65 100 C85 70 100 46 128 50 Z" fill="#6B4E28" fill-opacity="0.8" ${st(5)}/>${circ(215, 155, 12, "#6B4E28", 4)}`));
write("cafe_dmg_2", svg(256, 256, `<g fill="${PAPER}" stroke="${INK}" stroke-width="3"><circle cx="90" cy="110" r="7"/><circle cx="130" cy="90" r="6"/><circle cx="160" cy="130" r="7"/><circle cx="110" cy="160" r="6"/><circle cx="190" cy="170" r="5"/><circle cx="60" cy="160" r="5"/><circle cx="140" cy="200" r="6"/></g>`));
write("cafe_dmg_3", svg(256, 256, `<g stroke="${INK}" stroke-width="6" fill="none" stroke-linecap="round" stroke-opacity="0.7"><path d="M60 170 l40 -60 M110 180 l30 -70 M170 170 l20 -60"/></g>`));
write("cafe_dmg_4", svg(256, 256, `<g fill="${ACTION}" stroke="${INK}" stroke-width="3"><circle cx="100" cy="120" r="6"/><circle cx="125" cy="100" r="5"/><circle cx="150" cy="135" r="6"/><circle cx="120" cy="150" r="5"/><circle cx="170" cy="110" r="4"/></g>`));

// =====================================================================================
// BACKYARD - barbecue on the right (lid = giant), grill leaning (medium), fan on the table (fast)
// homes: fan (0.30, 0.44) w0.11 · grill (0.58, 0.30) w0.17 · lid (0.80, 0.42) w0.30
write("yard_bg", bg(`
  <rect width="1200" height="520" fill="${SKY}"/>${circ(1000, 120, 60, ACTION, 6)}
  <rect x="0" y="480" width="1200" height="200" fill="${WOOD}" ${st(6)}/><path d="M100 480 V680 M250 480 V680 M400 480 V680 M550 480 V680 M700 480 V680 M850 480 V680 M1000 480 V680 M1150 480 V680" stroke="${WOOD2}" stroke-width="8"/><path d="M0 560 H1200 M0 620 H1200" stroke="${WOOD2}" stroke-width="8"/>
  ${rect(180, 330, 40, 170, WOOD2, 6)}${circ(200, 300, 110, GRASS2, 6)}${circ(140, 340, 70, GRASS2, 6)}${circ(260, 350, 70, GRASS2, 6)}
  <rect x="0" y="680" width="1200" height="920" fill="${GRASS}"/><path d="M0 680 H1200" stroke="${INK}" stroke-width="6"/>
  <g stroke="${GRASS2}" stroke-width="6" stroke-linecap="round" fill="none"><path d="M70 800 l10 -40 M85 800 l12 -30 M100 800 l6 -36"/><path d="M1080 900 l10 -40 M1095 900 l12 -30 M1110 900 l6 -36"/><path d="M140 1480 l10 -40 M155 1480 l12 -30 M170 1480 l6 -36"/><path d="M560 760 l10 -40 M575 760 l12 -30 M590 760 l6 -36"/></g>
  <!-- table with sausages, corn and beer -->
  ${rect(120, 1080, 520, 40, WOOD, 6, 7)}${rect(150, 1120, 30, 320, WOOD2, 6)}${rect(580, 1120, 30, 320, WOOD2, 6)}
  ${ell(280, 1060, 90, 26, "#FFFFFF", 5)}${path_("M215 1050 q10 -12 24 0 q10 -12 24 0 q10 -12 24 0 q10 -12 24 0 q10 -12 24 0", "#C9553F", 6)}${path_("M215 1064 q10 -12 24 0 q10 -12 24 0 q10 -12 24 0 q10 -12 24 0 q10 -12 24 0", "#C9553F", 6)}
  ${path_("M440 1040 h90 q10 0 10 10 v10 q0 10 -10 10 h-90 q-10 0 -10 -10 v-10 q0 -10 10 -10 Z", ACTION, 5)}${path_("M540 1050 l40 -6 v22 l-40 -6 Z", GRASS2, 4)}
  ${rect(560, 980, 40, 90, "#E9C77B", 6, 5)}${rect(560, 970, 40, 20, "#FFFFFF", 6, 4)}
  <!-- home of the lid (object): open barbecue with sausages and smoke; grease mark of the grill; base of the fan -->
  ${ell(960, 1230, 190, 70, INK, 8)}${ell(960, 1230, 150, 50, "#3A3F3A", 0)}${path_("M880 1220 q10 -12 24 0 q10 -12 24 0 q10 -12 24 0 q10 -12 24 0 q10 -12 24 0 q10 -12 24 0", "#C9553F", 6)}${path_("M890 1240 q10 -12 24 0 q10 -12 24 0 q10 -12 24 0 q10 -12 24 0 q10 -12 24 0", "#C9553F", 6)}
  <path d="M900 1150 q-20 -60 20 -90 q30 -30 10 -70 M960 1150 q-20 -60 20 -90 q30 -30 10 -70 M1020 1150 q-20 -60 20 -90 q30 -30 10 -70" fill="none" stroke="#8C948C" stroke-width="10" stroke-opacity="0.7" stroke-linecap="round"/>
  ${rect(940, 1290, 40, 200, INK, 6, 6)}${rect(860, 1480, 200, 24, INK, 8, 6)}${circ(880, 1520, 22, INK, 5)}${circ(1040, 1520, 22, INK, 5)}
  ${ell(700, 1380, 70, 30, "#6B4E28", 0)}<ellipse cx="700" cy="1380" rx="70" ry="30" fill="#3A3F3A" fill-opacity="0.35"/>
  ${circ(360, 1000, 22, "none", 4)}
  ${rect(0, 1560, 1200, 40, GRASS2, 0, 0)}
`));
write("tool_fan", fastRound(`${circ(256, 256, 236, "#E9C77B")}<path d="M256 256 L256 20 M256 256 L440 110 M256 256 L490 256 M256 256 L440 400 M256 256 L256 492 M256 256 L72 400 M256 256 L22 256 M256 256 L72 110" fill="none" stroke="${WOOD2}" stroke-width="10"/>${circ(256, 256, 40, WOOD, 8)}`));
write("tool_grate", fastRound(`${circ(256, 256, 236, "#3A3F3A")}<path d="M60 130 H452 M40 200 H472 M40 270 H472 M40 340 H472 M70 410 H442" fill="none" stroke="#8C948C" stroke-width="16"/>${circ(256, 256, 236, "none")}`));
write("tool_grilllid", giant(`${ell(384, 166, 372, 150, INK)}${ell(384, 166, 300, 110, "#3A3F3A", 8)}<path d="M120 100 Q384 40 650 100" fill="none" stroke="#8C948C" stroke-width="16" stroke-opacity="0.7"/>${rect(340, 130, 90, 40, "#8C948C", 10, 8)}`));
write("home_grilllid", svg(400, 240, `<g ${st(8)}><path d="M20 220 q0 -200 180 -200 q180 0 180 200 Z" fill="${INK}"/><path d="M60 220 q0 -150 140 -150 q140 0 140 150" fill="none" stroke="#3A3F3A" stroke-width="20"/>${rect(160, 20, 80, 30, "#8C948C", 10, 6)}</g>`));
write("icon_fan", icon(`${circ(64, 64, 52, "#E9C77B", 7)}<path d="M64 64 V12 M64 64 L104 26 M64 64 L116 64 M64 64 L104 102 M64 64 V116 M64 64 L24 102 M64 64 L12 64 M64 64 L24 26" fill="none" stroke="${WOOD2}" stroke-width="4"/>`));
write("icon_grate", icon(`${circ(64, 64, 52, "#3A3F3A", 7)}<path d="M20 44 H108 M14 64 H114 M20 84 H108" fill="none" stroke="#8C948C" stroke-width="6"/>`));
write("icon_grilllid", icon(`<path d="M12 100 q0 -80 52 -80 q52 0 52 80 Z" fill="${INK}" ${st(7)}/>${rect(52, 14, 24, 12, "#8C948C", 4, 4)}`));
write("yard_dmg_1", svg(256, 256, `${ell(128, 128, 90, 60, "#3A3F3A", 0)}<ellipse cx="128" cy="128" rx="90" ry="60" fill="${INK}" fill-opacity="0.5"/>`));
write("yard_dmg_2", svg(256, 256, `<path d="M128 50 C170 60 210 80 205 120 C225 150 190 185 160 195 C130 215 90 205 65 180 C35 160 40 120 65 100 C85 70 100 46 128 50 Z" fill="${FURY}" fill-opacity="0.85" ${st(5)}/>${circ(215, 155, 12, FURY, 4)}`));
write("yard_dmg_3", svg(256, 256, `<g fill="#8C948C" fill-opacity="0.7"><circle cx="100" cy="120" r="14"/><circle cx="140" cy="100" r="10"/><circle cx="160" cy="150" r="16"/><circle cx="110" cy="170" r="9"/></g>`));
write("yard_dmg_4", svg(256, 256, `${ell(128, 128, 100, 70, GRASS2, 0)}<ellipse cx="128" cy="128" rx="100" ry="70" fill="${GRASS2}" fill-opacity="0.55"/><g stroke="#3E6338" stroke-width="6" stroke-linecap="round" fill="none"><path d="M60 150 l-30 -20 M90 170 l-24 -30 M140 175 l-6 -34 M180 160 l26 -24"/></g>`));
console.log("scenes written");
