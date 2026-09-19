// ADR-013: second object per role in every scene (rotating hand) - tools, icons and "at home" sprites of the giants. Own work, same style.
// Usage: node tools/art/scenes_alt.mjs && node tools/art/export.mjs
import fs from "node:fs";
import path from "node:path";
const root = path.resolve(path.dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, "$1")), "..", "..");
const src = path.join(root, "art", "svg");
const INK = "#202622", PAPER = "#F7F0DE", ACTION = "#F4CD3C", FURY = "#AF3D2F", INFO = "#2755A5", WOOD = "#B98F55", WOOD2 = "#8F6A38", GRASS2 = "#5F8A55";
const svg = (w, h, body) => `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">${body}</svg>`;
const st = (w = 6) => `stroke="${INK}" stroke-width="${w}" stroke-linejoin="round" stroke-linecap="round"`;
const rect = (x, y, w, h, fill, r = 0, sw = 6) => `<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${r}" fill="${fill}" ${st(sw)}/>`;
const circ = (cx, cy, r, fill, sw = 6) => `<circle cx="${cx}" cy="${cy}" r="${r}" fill="${fill}" ${st(sw)}/>`;
const write = (name, s) => fs.writeFileSync(path.join(src, name + ".svg"), s);
const fastRound = (inner) => svg(512, 512, `<g ${st(12)}>${inner}</g>`);
const giant = (inner) => svg(768, 332, `<g ${st(12)}>${inner}</g>`);
const icon = (inner) => svg(128, 128, inner);
const iconRound = (fill, inner = "") => icon(`${circ(64, 64, 52, fill, 7)}${inner}`);
const shine = `<path d="M120 170 Q200 100 300 110" fill="none" stroke="${PAPER}" stroke-width="14" stroke-opacity="0.5"/>`;

// kitchen: spatula (fast), pot lid (medium), table (giant)
write("tool_spatula", fastRound(`${rect(40, 150, 300, 212, "#8C948C", 30)}<path d="M100 200 H300 M100 256 H300 M100 312 H300" stroke="${INK}" stroke-width="10"/>${rect(330, 226, 150, 60, WOOD, 20)}`));
write("tool_potlid", fastRound(`${circ(256, 256, 236, "#B0AEA3")}${circ(256, 256, 180, "#8C948C", 8)}${circ(256, 256, 44, INK, 8)}${shine}`));
write("tool_table", giant(`${rect(10, 40, 748, 252, WOOD, 20)}<path d="M40 100 H728 M40 170 H728 M40 240 H728" stroke="${WOOD2}" stroke-width="8"/>${rect(40, 292, 60, 30, WOOD2, 6, 6)}${rect(668, 292, 60, 30, WOOD2, 6, 6)}`));
write("home_table", svg(400, 260, `<g ${st(8)}>${rect(10, 40, 380, 40, WOOD, 8)}${rect(40, 80, 30, 170, WOOD2, 6)}${rect(330, 80, 30, 170, WOOD2, 6)}</g>`));
write("icon_spatula", icon(`${rect(16, 40, 64, 48, "#8C948C", 10, 7)}<path d="M28 54 h40 M28 74 h40" stroke="${INK}" stroke-width="5"/>${rect(78, 56, 36, 16, WOOD, 6, 5)}`));
write("icon_potlid", iconRound("#B0AEA3", `${circ(64, 64, 12, INK, 5)}`));
write("icon_table", icon(`${rect(14, 40, 100, 20, WOOD, 6, 7)}${rect(24, 60, 12, 50, WOOD2, 4, 5)}${rect(92, 60, 12, 50, WOOD2, 4, 5)}`));
// picnic: straw hat (fast), plate (medium), cooler (giant)
write("tool_strawhat", fastRound(`${circ(256, 256, 236, "#E9C77B")}${circ(256, 256, 130, "#D9B27A", 10)}<path d="M126 256 a130 130 0 0 0 260 0" fill="none" stroke="${FURY}" stroke-width="24"/>`));
write("tool_plate", fastRound(`${circ(256, 256, 236, "#FFFFFF")}${circ(256, 256, 170, "#F7F0DE", 8)}<path d="M100 256 a156 156 0 0 1 312 0" fill="none" stroke="${INFO}" stroke-width="10" stroke-opacity="0.7"/>`));
write("tool_cooler", giant(`${rect(10, 40, 748, 252, INFO, 30)}${rect(10, 40, 748, 70, "#FFFFFF", 24, 8)}${rect(340, 20, 90, 30, "#8C948C", 8, 8)}`));
write("home_cooler", svg(300, 240, `<g ${st(8)}>${rect(10, 60, 280, 170, INFO, 24)}${rect(10, 60, 280, 50, "#FFFFFF", 18, 6)}${rect(110, 40, 80, 24, "#8C948C", 8, 6)}</g>`));
write("icon_strawhat", iconRound("#E9C77B", `${circ(64, 64, 26, "#D9B27A", 5)}<path d="M38 64 a26 26 0 0 0 52 0" fill="none" stroke="${FURY}" stroke-width="7"/>`));
write("icon_plate", iconRound("#FFFFFF", `${circ(64, 64, 34, "#F7F0DE", 5)}`));
write("icon_cooler", icon(`${rect(14, 34, 100, 74, INFO, 12, 7)}${rect(14, 34, 100, 22, "#FFFFFF", 8, 5)}`));
// bathroom: sponge (fast), toilet brush (medium), bath mat (giant)
write("tool_sponge", fastRound(`<path d="M60 90 Q256 40 452 90 Q472 256 452 422 Q256 472 60 422 Q40 256 60 90 Z" fill="${ACTION}"/><path d="M60 300 Q256 250 452 300 Q472 360 452 422 Q256 472 60 422 Q40 360 60 300 Z" fill="${GRASS2}" stroke="none"/><g fill="${INK}" fill-opacity="0.25"><circle cx="150" cy="170" r="14"/><circle cx="300" cy="140" r="10"/><circle cx="380" cy="220" r="12"/><circle cx="200" cy="240" r="9"/></g>`));
write("tool_brush", fastRound(`${circ(256, 256, 236, "#FFFFFF")}${circ(256, 256, 190, "#DCE6EA", 8)}<g stroke="${INK}" stroke-width="6" stroke-opacity="0.35"><path d="M256 256 L256 80 M256 256 L390 130 M256 256 L432 256 M256 256 L390 382 M256 256 L256 432 M256 256 L122 382 M256 256 L80 256 M256 256 L122 130"/></g>${circ(256, 256, 40, INFO, 8)}`));
write("tool_bathmat", giant(`${rect(10, 30, 748, 272, "#BFD7E6", 24)}<g stroke="${INFO}" stroke-width="8" stroke-opacity="0.6"><path d="M40 80 H728 M40 140 H728 M40 200 H728 M40 260 H728"/></g>`));
write("home_bathmat", svg(400, 160, `<g ${st(8)}>${rect(10, 10, 380, 140, "#BFD7E6", 20)}<path d="M40 50 H360 M40 90 H360 M40 130 H360" stroke="${INFO}" stroke-width="7" stroke-opacity="0.6"/></g>`));
write("icon_sponge", icon(`<path d="M20 30 Q64 22 108 30 Q114 64 108 98 Q64 106 20 98 Q14 64 20 30 Z" fill="${ACTION}" ${st(7)}/><path d="M20 70 Q64 62 108 70 Q112 84 108 98 Q64 106 20 98 Q16 84 20 70 Z" fill="${GRASS2}" stroke="none"/>`));
write("icon_brush", iconRound("#FFFFFF", `${circ(64, 64, 12, INFO, 5)}<path d="M64 64 V24 M64 64 L98 40 M64 64 L30 40" stroke="${INK}" stroke-width="4" stroke-opacity="0.4"/>`));
write("icon_bathmat", icon(`${rect(12, 34, 104, 60, "#BFD7E6", 12, 7)}<path d="M28 54 H100 M28 74 H100" stroke="${INFO}" stroke-width="5"/>`));
// living room: remote (fast), book (medium), rug (giant)
write("tool_remote", fastRound(`${rect(150, 40, 212, 432, INK, 40)}${circ(256, 120, 26, FURY, 6)}<g fill="${PAPER}" stroke="none"><circle cx="216" cy="200" r="16"/><circle cx="296" cy="200" r="16"/><circle cx="216" cy="260" r="16"/><circle cx="296" cy="260" r="16"/><circle cx="216" cy="320" r="16"/><circle cx="296" cy="320" r="16"/><rect x="200" y="370" width="112" height="60" rx="14"/></g>`));
write("tool_book", fastRound(`${rect(60, 60, 392, 392, FURY, 16)}${rect(90, 90, 332, 332, "#C9553F", 10, 8)}<path d="M256 90 V422" stroke="${INK}" stroke-width="10"/><path d="M130 160 h90 M130 200 h60 M300 160 h90 M300 200 h70" stroke="${PAPER}" stroke-width="10" stroke-opacity="0.8"/>`));
write("tool_rug", giant(`${rect(10, 30, 748, 272, "#B98F55", 16)}${rect(50, 70, 668, 192, "#D9B27A", 12, 8)}<path d="M90 110 H678 M90 220 H678" stroke="${FURY}" stroke-width="10" stroke-opacity="0.7"/>`));
write("home_rug", svg(500, 200, `<g ${st(8)}>${rect(10, 20, 480, 160, "#B98F55", 14)}${rect(40, 50, 420, 100, "#D9B27A", 10, 6)}</g>`));
write("icon_remote", icon(`${rect(44, 12, 40, 104, INK, 10, 6)}${circ(64, 32, 7, FURY, 3)}<g fill="${PAPER}"><circle cx="56" cy="56" r="4"/><circle cx="72" cy="56" r="4"/><circle cx="56" cy="72" r="4"/><circle cx="72" cy="72" r="4"/><rect x="52" y="86" width="24" height="16" rx="5"/></g>`));
write("icon_book", icon(`${rect(20, 20, 88, 88, FURY, 8, 7)}<path d="M64 20 V108" stroke="${INK}" stroke-width="5"/><path d="M32 40 h18 M32 52 h12 M78 40 h18 M78 52 h14" stroke="${PAPER}" stroke-width="5"/>`));
write("icon_rug", icon(`${rect(12, 36, 104, 56, "#B98F55", 8, 7)}${rect(24, 48, 80, 32, "#D9B27A", 6, 4)}`));
// beach: shovel (fast), bucket (medium), towel (giant)
write("tool_shovel", fastRound(`<path d="M256 40 L420 200 L256 470 L92 200 Z" fill="${FURY}"/><path d="M256 100 L360 200 L256 400 L152 200 Z" fill="#C9553F" stroke="none"/>${rect(236, 20, 40, 100, WOOD, 12, 8)}`));
write("tool_bucket", fastRound(`${circ(256, 256, 236, ACTION)}${circ(256, 256, 190, "#E9C77B", 8)}${circ(256, 256, 120, "#D9B27A", 8)}${shine}`));
write("tool_towel", giant(`${rect(10, 30, 748, 272, "#FFFFFF", 16)}<path d="M10 80 H758 M10 140 H758 M10 200 H758 M10 260 H758" stroke="${INFO}" stroke-width="26"/>`));
write("home_towel", svg(500, 200, `<g ${st(8)}>${rect(10, 20, 480, 160, "#FFFFFF", 12)}<path d="M10 60 H490 M10 100 H490 M10 140 H490" stroke="${INFO}" stroke-width="20"/></g>`));
write("icon_shovel", icon(`<path d="M64 30 L104 70 L64 116 L24 70 Z" fill="${FURY}" ${st(7)}/>${rect(58, 10, 12, 30, WOOD, 4, 5)}`));
write("icon_bucket", iconRound(ACTION, `${circ(64, 64, 34, "#E9C77B", 5)}${circ(64, 64, 18, "#D9B27A", 4)}`));
write("icon_towel", icon(`${rect(12, 34, 104, 60, "#FFFFFF", 8, 7)}<path d="M12 50 H116 M12 66 H116 M12 82 H116" stroke="${INFO}" stroke-width="9"/>`));
// café terrace: coaster (fast), plate (medium - reuses tool_plate), chair (giant)
write("tool_coaster", fastRound(`${circ(256, 256, 236, "#D9B27A")}${circ(256, 256, 200, "#E9C77B", 8)}<path d="M180 256 h152 M256 180 v152" stroke="${INK}" stroke-width="12" stroke-opacity="0.5"/>${circ(256, 256, 60, "#6B4E28", 6)}`));
write("tool_chair", giant(`${rect(10, 40, 748, 252, INK, 30)}${rect(60, 90, 648, 152, "#F7F0DE", 20, 8)}${rect(40, 292, 60, 30, INK, 6, 6)}${rect(668, 292, 60, 30, INK, 6, 6)}`));
write("home_chair", svg(220, 340, `<g ${st(8)}>${rect(30, 20, 160, 200, INK, 18)}${rect(50, 40, 120, 100, "#F7F0DE", 12, 6)}${rect(20, 220, 180, 40, INK, 10)}${rect(30, 260, 24, 70, INK, 6, 6)}${rect(166, 260, 24, 70, INK, 6, 6)}</g>`));
write("icon_coaster", iconRound("#D9B27A", `${circ(64, 64, 16, "#6B4E28", 4)}`));
write("icon_chair", icon(`${rect(28, 12, 72, 60, INK, 10, 7)}${rect(40, 24, 48, 34, "#F7F0DE", 6, 4)}${rect(22, 72, 84, 18, INK, 6, 6)}${rect(28, 90, 10, 28, INK, 3, 4)}${rect(90, 90, 10, 28, INK, 3, 4)}`));
// backyard: spatula (fast - reuses tool_spatula), pot (medium), table (giant - reuses tool_table/home_table)
write("tool_pot", fastRound(`${circ(256, 256, 236, "#3A3F3A")}${circ(256, 256, 180, "#555E55", 8)}${rect(40, 236, 70, 40, "#8C948C", 12, 8)}${rect(402, 236, 70, 40, "#8C948C", 12, 8)}${shine}`));
write("icon_pot", iconRound("#3A3F3A", `${circ(64, 64, 34, "#555E55", 5)}${rect(8, 58, 16, 12, "#8C948C", 4, 4)}${rect(104, 58, 16, 12, "#8C948C", 4, 4)}`));
console.log("alternates written");
