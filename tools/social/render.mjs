// Renders tools/social/card.html to PNG at 2× (landscape 1200×627 for article covers / link shares, square 1200×1200 for posts).
// Usage: node tools/social/render.mjs [outDir]   (headless Chrome via CDP; fonts: Archivo Black from brand/fonts, Archivo from Google Fonts)
import { spawn } from "node:child_process"; import fs from "node:fs"; import path from "node:path"; import { fileURLToPath, pathToFileURL } from "node:url";
const here = path.dirname(fileURLToPath(import.meta.url)); const outDir = path.resolve(process.argv[2] || path.resolve(here, "../../dist/social")); fs.mkdirSync(outDir, { recursive: true });
const chrome = "C:/Program Files/Google/Chrome/Application/chrome.exe"; const port = 9335; const prof = process.env.TEMP + "/tdf-chrome-prof3"; fs.rmSync(prof, { recursive: true, force: true });
const proc = spawn(chrome, ["--headless=new", `--remote-debugging-port=${port}`, "--use-angle=swiftshader", `--user-data-dir=${prof}`, "--no-first-run", "--window-size=1200,1200", "--hide-scrollbars", "--allow-file-access-from-files", "about:blank"], { stdio: "ignore" });
await new Promise(r => setTimeout(r, 2500));
const list = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json(); const page = list.find(t => t.type === "page");
const ws = new WebSocket(page.webSocketDebuggerUrl); let id = 0; const pending = new Map();
const call = (method, params = {}) => new Promise(res => { const i = ++id; pending.set(i, res); ws.send(JSON.stringify({ id: i, method, params })); });
ws.onmessage = ev => { const m = JSON.parse(ev.data); if (m.id && pending.has(m.id)) { pending.get(m.id)(m.result); pending.delete(m.id); } };
await new Promise(r => ws.onopen = r);
await call("Page.enable"); await call("Page.navigate", { url: pathToFileURL(path.join(here, "card.html")).href }); await new Promise(r => setTimeout(r, 4000));
for (const [name, cls, W, H] of [["linkedin-cover-1200x627.png", "", 1200, 627], ["linkedin-post-1200x1200.png", "square", 1200, 1200]]) {
  await call("Runtime.evaluate", { expression: `document.body.className = ${JSON.stringify(cls)}; document.fonts.ready.then(() => true)`, awaitPromise: true });
  await call("Emulation.setDeviceMetricsOverride", { width: W, height: H, deviceScaleFactor: 2, mobile: false }); await new Promise(r => setTimeout(r, 700));
  const shot = await call("Page.captureScreenshot", { format: "png", clip: { x: 0, y: 0, width: W, height: H, scale: 2 } });
  const out = path.join(outDir, name); fs.writeFileSync(out, Buffer.from(shot.data, "base64")); console.log("CARD:", out, `${W * 2}x${H * 2}`);
}
proc.kill();
