// Exports the SVGs of art/svg to PNG (art/out and app/Assets/Resources/art). Usage: node tools/art/export.mjs
// Each entry: SVG file, width in px (the height follows the viewBox), destination. Editable originals stay in art/svg.
import sharp from "sharp";
import fs from "node:fs";
import path from "node:path";

const root = path.resolve(path.dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, "$1")), "..", "..");
const src = path.join(root, "art", "svg"); const outDir = path.join(root, "art", "out"); const resDir = path.join(root, "app", "Assets", "Resources", "art");
fs.mkdirSync(outDir, { recursive: true }); fs.mkdirSync(resDir, { recursive: true });

const jobs = [
  ["fly_top_wings0.svg", 256], ["fly_top_wings1.svg", 256], ["fly_top_wings2.svg", 256], ["fly_top_flat.svg", 256], ["fly_top_landed.svg", 256],
  ["tool_cloth.svg", 512], ["tool_pan.svg", 512], ["tool_fridge.svg", 768],
  ["fx_impact_ring.svg", 256], ["fx_stars.svg", 256],
  ["kitchen_bg.svg", 1200],
  ["dmg_tile_crack.svg", 256], ["dmg_splat.svg", 256], ["dmg_dent.svg", 256], ["dmg_cracks.svg", 256],
  ["icon_cloth.svg", 128], ["icon_pan.svg", 128], ["icon_fridge.svg", 128],
  ...["bb", "ff", "hf", "bs", "gd"].flatMap(k => ["wings0", "wings1", "wings2", "flat", "landed"].map(f => [`fly_${k}_${f}.svg`, 256])),
  ["balloon_burst.svg", 512], ["fx_trail.svg", 64],
  // picnic (second scene)
  ["picnic_bg.svg", 1200], ["tool_napkin.svg", 512], ["tool_frisbee.svg", 512], ["tool_basket.svg", 768],
  ["icon_napkin.svg", 128], ["icon_frisbee.svg", 128], ["icon_basket.svg", 128],
  ["pdmg_grass.svg", 256], ["pdmg_juice.svg", 256], ["pdmg_melon.svg", 256], ["pdmg_dirt.svg", 256],
  ["home_fridge.svg", 280], ["home_basket.svg", 320],
  // extra scenes (ADR-011): backgrounds, tools, "at home" sprites, icons, decals
  ...["bath", "living", "beach", "cafe", "yard"].flatMap(s => [[`${s}_bg.svg`, 1200], ...[1, 2, 3, 4].map(i => [`${s}_dmg_${i}.svg`, 256])]),
  ...["paper", "plunger", "newspaper", "cushion", "flipflop", "ball", "menu", "tray", "fan", "grate"].map(t => [`tool_${t}.svg`, 512]),
  ...["lid", "sofa", "umbrella", "parasol", "grilllid"].map(t => [`tool_${t}.svg`, 768]),
  ...["lid", "sofa", "umbrella", "parasol", "grilllid"].map(t => [`home_${t}.svg`, 400]),
  ...["paper", "plunger", "lid", "newspaper", "cushion", "sofa", "flipflop", "ball", "umbrella", "menu", "tray", "parasol", "fan", "grate", "grilllid"].map(t => [`icon_${t}.svg`, 128]),
  // ADR-013: second objects per role
  ...["spatula", "potlid", "strawhat", "plate", "sponge", "brush", "remote", "book", "shovel", "bucket", "coaster", "pot"].map(t => [`tool_${t}.svg`, 512]),
  ...["table", "cooler", "bathmat", "rug", "towel", "chair"].map(t => [`tool_${t}.svg`, 768]),
  ...["table", "cooler", "bathmat", "rug", "towel", "chair"].map(t => [`home_${t}.svg`, 400]),
  ...["spatula", "potlid", "table", "strawhat", "plate", "cooler", "sponge", "brush", "bathmat", "remote", "book", "rug", "shovel", "bucket", "towel", "coaster", "chair", "pot"].map(t => [`icon_${t}.svg`, 128]), ["icon_brain.svg", 128], ["qr.svg", 264, "brand"], ["icon_daily.svg", 128], ["icon_records.svg", 128], ["icon_settings.svg", 128], ["icon_play_fly.svg", 128], ["icon_star.svg", 128], ["icon_star_empty.svg", 128], ["icon_lock.svg", 128], ["icon_bait.svg", 128], ["bait.svg", 256], ["icon_hand.svg", 128], ["hand.svg", 512], ["face1_bg.svg", 1024], ["face2_bg.svg", 1024], ["face3_bg.svg", 1024], ["face4_bg.svg", 1024], ["face5_bg.svg", 1024], ["face2_over.svg", 400], ["face_eyes_open.svg", 360], ["face_eyes_squint.svg", 360], ["face_eyes_angry.svg", 360], ["face_mouth_neutral.svg", 220], ["face_mouth_ouch.svg", 220], ["face_mouth_angry.svg", 220], ["face_mark.svg", 200], ["face_blush.svg", 512], ["tool_slap.svg", 512], ["icon_slap.svg", 128], ["icon_brain_eyes.svg", 128], ["icon_brain_eyes_on.svg", 128],
];
for (const [file, width, sub] of jobs) {
  const p = path.join(src, file); if (!fs.existsSync(p)) { console.log("skip (missing)", file); continue; }
  const png = await sharp(p, { density: 300 }).resize({ width }).png({ compressionLevel: 9 }).toBuffer();
  const name = file.replace(/\.svg$/, ".png");
  const dest = sub ? path.join(path.dirname(resDir), sub) : resDir; fs.mkdirSync(dest, { recursive: true });
  fs.writeFileSync(path.join(outDir, name), png); fs.writeFileSync(path.join(dest, name), png);
  const meta = await sharp(png).metadata(); console.log(name, meta.width + "x" + meta.height, (png.length / 1024).toFixed(0) + " KB");
}
