// Regenerates the landing screenshots (TemplateData/shots/{pt,en}/{start,round,face,boss,result}.jpg) from a DEV web build,
// through the headless Chrome of tools/headless-check.mjs (dev commands lang:xx / boss / bonus). The captures keep the phone's
// aspect ratio (390×844 window → width 420, height follows): the page shows them with width:100%, so no stretching.
// Usage: node tools/shots.mjs [dist/web-dev] [port]   (needs tools/art/node_modules/sharp; a dev build with TDF_DEVBUILD)
import fs from "node:fs"; import path from "node:path"; import { fileURLToPath } from "node:url"; import { spawn, spawnSync } from "node:child_process";
import sharp from "./art/node_modules/sharp/lib/index.js";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const dist = path.resolve(root, process.argv[2] || "dist/web-dev"); const port = Number(process.argv[3] || 8080); const only = process.argv.slice(4);   // optional: names to regenerate
const outDir = path.join(root, "app/Assets/WebGLTemplates/TDF/TemplateData/shots"); const tmp = process.env.TEMP;
if (!fs.existsSync(path.join(dist, "index.html"))) throw new Error("dev build missing: " + dist);

const server = spawn(process.execPath, [path.join(root, "web/serve.mjs"), dist, String(port)], { stdio: "ignore" });
await new Promise(r => setTimeout(r, 1500));
const url = `http://localhost:${port}/?ui=m`;

// name → dev commands, headless mode, env, source png (the phone is emulated at 390×844, DPR 2)
const PLAN = {
  start:  { dev: "", mode: "start", env: {}, src: "tdf-shot.png" },
  round:  { dev: "", mode: "hunt", env: { TDF_HUNT: "14" }, src: "tdf-shot-19.png" },
  face:   { dev: "wait:2500;bonus", mode: "hunt", env: { TDF_HUNT: "9", TDF_HUNTSNAP: "4", TDF_HUNTOFF: "42" }, src: "tdf-shot-7.png" },
  boss:   { dev: "boss", mode: "hunt", env: { TDF_HUNT: "10" }, src: "tdf-shot-11.png" },
  result: { dev: "stars", mode: "hunt", env: { TDF_HUNT: "45", TDF_HUNTMS: "900", TDF_HUNTOFF: "30", TDF_HUNTSNAP: "0", TDF_HUNTEND: "70", TDF_AFTER: "500" }, src: "tdf-shot.png" },   // stars: no new star → no bonus interstitial, straight to the result
};
const LANG = { pt: "pt-PT", en: "en" };
function run(lang, name) {
  const p = PLAN[name]; const dev = ["lang:" + LANG[lang], p.dev].filter(Boolean).join(";");   // the lang command rebuilds the screens; the next command waits 1.2 s in headless-check
  for (const f of fs.readdirSync(tmp)) if (/^tdf-shot.*\.png$/.test(f)) fs.unlinkSync(path.join(tmp, f));
  const args = [path.join(root, "tools/headless-check.mjs"), url, "25", "play"]; if (p.mode) args.push(p.mode);
  const r = spawnSync(process.execPath, args, { env: { ...process.env, TDF_EMU: "390,844", TDF_DEV: dev, ...p.env }, encoding: "utf-8", timeout: 400000 });
  const state = (r.stdout || "").split("\n").find(l => l.startsWith("STATE:")) || "(no state)";
  console.log(`${lang}/${name}: ${state.slice(0, 120)}`);
  const src = path.join(tmp, p.src); if (!fs.existsSync(src)) throw new Error("shot missing: " + src);
  return src;
}
for (const lang of ["pt", "en"]) {
  fs.mkdirSync(path.join(outDir, lang), { recursive: true });
  for (const name of Object.keys(PLAN)) { if (only.length && !only.includes(name)) continue;
    const src = run(lang, name); const dst = path.join(outDir, lang, name + ".jpg");
    const meta = await sharp(src).metadata();
    await sharp(src).resize({ width: 420, height: Math.round(420 * meta.height / meta.width), fit: "fill" }).jpeg({ quality: 82, mozjpeg: true }).toFile(dst);
    console.log(`  → ${path.relative(root, dst)} ${meta.width}x${meta.height} → 420x${Math.round(420 * meta.height / meta.width)}`);
  }
}
server.kill(); console.log("shots done");
