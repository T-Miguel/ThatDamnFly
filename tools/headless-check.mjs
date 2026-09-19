// Opens the web build in a headless Chrome via CDP and collects console/exceptions for N seconds.
// Usage: node tools/headless-check.mjs <url> [seconds] [play [start|shots|share|sceneN|boss|daily|wait|settings|picnic|hunt]]
import { spawn } from "node:child_process";
import fs from "node:fs";
const url = process.argv[2] || "http://localhost:8080/"; const secs = Number(process.argv[3] || 30);
const chrome = "C:/Program Files/Google/Chrome/Application/chrome.exe"; const port = 9333; const prof = process.env.TEMP + "/tdf-chrome-prof";
fs.rmSync(prof, { recursive: true, force: true });
const proc = spawn(chrome, ["--headless=new", `--remote-debugging-port=${port}`, "--use-angle=swiftshader", "--enable-unsafe-swiftshader", "--ignore-gpu-blocklist", `--user-data-dir=${prof}`, "--no-first-run", `--window-size=${process.env.TDF_WINDOW || "390,844"}`, ...(process.env.TDF_RESOLVE ? [`--host-resolver-rules=MAP ${process.env.TDF_RESOLVE.split("=")[0]} ${process.env.TDF_RESOLVE.split("=")[1]}`] : []), "about:blank"], { stdio: "ignore" });   // TDF_RESOLVE=host=ip to test before DNS propagation
setTimeout(() => { console.log("TIMEOUT: the page did not respond"); for (const l of out) console.log(l.slice(0, 400)); try { proc.kill(); } catch {} process.exit(2); }, (secs + 100 + Number(process.env.TDF_HUNT || 0) + Number(process.env.TDF_HUNTEND || 0)) * 1000 + Number(process.env.TDF_AFTER || 0)).unref();   // never hang (frozen page, dialog)
await new Promise(r => setTimeout(r, 2500));
const list = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json(); const page = list.find(t => t.type === "page");
const ws = new WebSocket(page.webSocketDebuggerUrl); let id = 0; const send = (method, params = {}) => ws.send(JSON.stringify({ id: ++id, method, params }));
const out = [];
ws.onmessage = (ev) => {
  const m = JSON.parse(ev.data);
  if (m.method === "Runtime.consoleAPICalled") out.push(`[console.${m.params.type}] ` + m.params.args.map(a => a.value ?? a.description ?? "").join(" "));
  if (m.method === "Runtime.exceptionThrown") out.push("[exception] " + (m.params.exceptionDetails.exception?.description || m.params.exceptionDetails.text) + " @ " + m.params.exceptionDetails.url + ":" + m.params.exceptionDetails.lineNumber);
  if (m.method === "Log.entryAdded") out.push(`[log.${m.params.entry.level}] ` + m.params.entry.text + (m.params.entry.url ? " @ " + m.params.entry.url : ""));
};
await new Promise(r => ws.onopen = r);
send("Runtime.enable"); send("Log.enable"); send("Page.enable");
const EMU = process.env.TDF_EMU ? process.env.TDF_EMU.split(",").map(Number) : null;   // TDF_EMU=width,height: emulates a narrow phone (the Chrome window never goes below 500 px)
if (EMU) send("Emulation.setDeviceMetricsOverride", { width: EMU[0], height: EMU[1], deviceScaleFactor: 2, mobile: true });
const PX = EMU ? EMU[0] / 2 : 250, PLAYY = EMU ? EMU[1] - 209 : 526, TUTY = EMU ? EMU[1] / 2 + 33 : 410; send("Page.setDownloadBehavior", { behavior: "allow", downloadPath: process.env.TEMP }); send("Page.navigate", { url });
await new Promise(r => setTimeout(r, Math.min(secs, 25) * 1000));
if (process.argv[4] === "play") {
  if (process.env.TDF_DEV) for (const cmd of process.env.TDF_DEV.split(";")) { if (cmd.startsWith("wait:")) { await new Promise(r => setTimeout(r, Number(cmd.slice(5)))); continue; } send("Runtime.evaluate", { expression: `window.tdfUnity && window.tdfUnity.SendMessage("Bootstrap", "OnDevCommand", ${JSON.stringify(cmd)})` }); await new Promise(r => setTimeout(r, 1200)); }   // TDF_DEV=unlock|stars|boss|bonus|lang:xx|wait:ms (several with ";"; development builds only)
  const click = async (x, y) => { send("Input.dispatchMouseEvent", { type: "mousePressed", x, y, button: "left", clickCount: 1 }); await new Promise(r => setTimeout(r, 60)); send("Input.dispatchMouseEvent", { type: "mouseReleased", x, y, button: "left", clickCount: 1 }); };
  if (process.argv[5] === "settings") { await click(298, 619); await new Promise(r => setTimeout(r, 1200)); }
  else if (process.argv[5] === "start") { await new Promise(r => setTimeout(r, 500)); }
  else if (process.argv[5] === "wait") { await new Promise(r => setTimeout(r, Number(process.env.TDF_WAIT || 22000))); }   // just wait (e.g. the bonus running by itself) and take the shot
  else if (process.argv[5] === "boss") { await new Promise(r => setTimeout(r, 1500)); await click(250, 410); await new Promise(r => setTimeout(r, 900)); }   // TDF_DEV=boss already started the round
  else if (process.argv[5] === "daily") { await click(106, 619); await new Promise(r => setTimeout(r, 1500)); await click(250, 410); await new Promise(r => setTimeout(r, 900)); }
  else { if (process.argv[5] === "picnic") { await click(430, 343); await new Promise(r => setTimeout(r, 500)); }
    if (/^scene\d+$/.test(process.argv[5] || "")) { const n = Number(process.argv[5].slice(5)); for (let k = 0; k < n; k++) { await click(430, 343); await new Promise(r => setTimeout(r, 350)); } }   // sceneN: N taps on the right arrow
    await click(PX, PLAYY); await new Promise(r => setTimeout(r, 1500)); await click(PX, TUTY); await new Promise(r => setTimeout(r, 900)); }            // Play (S02: center −20 u); then "Let's go" of the guided first round (clean profile)
  const snap = async (name) => { const data = await new Promise((resolve) => { const id0 = id + 1; ws.onmessage = (ev) => { const m = JSON.parse(ev.data); if (m.id === id0) resolve(m.result?.data); else if (m.method === "Runtime.consoleAPICalled") out.push("[console] " + m.params.args.map(a => a.value ?? "").join(" ")); }; send("Page.captureScreenshot", { format: "png" }); }); if (data) fs.writeFileSync(process.env.TEMP + "/" + name, Buffer.from(data, "base64")); };
  const evalValue = async (expression) => new Promise((resolve) => { const id0 = id + 1; ws.onmessage = (ev) => { const m = JSON.parse(ev.data); if (m.id === id0) resolve(m.result?.result?.value); else if (m.method === "Runtime.consoleAPICalled") out.push("[console] " + m.params.args.map(a => a.value ?? "").join(" ")); }; send("Runtime.evaluate", { expression, returnByValue: true }); });
  if (process.argv[5] === "hunt") {   // hunt: for TDF_HUNT seconds taps where the fly is (window.tdfFly, published by dev builds), shots every 8 taps; the round is really played by the circuit
    const secs = Number(process.env.TDF_HUNT || 60); const t0 = Date.now(); let k = 0;
    let ended = false;
    while (Date.now() - t0 < secs * 1000 && !ended) {
      const v = await evalValue("(function(){ var f = window.tdfFly, c = document.getElementById('unity-canvas'); if (!f || !c) return null; var r = c.getBoundingClientRect(); return [f.x < 0 ? -1 : r.left + f.x / f.w * r.width, f.x < 0 ? -1 : r.top + (1 - f.y / f.h) * r.height, f.s]; })()");
      if (v && v[2] === 3) { ended = true; break; }
      if (v && v[2] === 2) { send("Runtime.evaluate", { expression: `window.tdfUnity && window.tdfUnity.SendMessage("Bootstrap", "OnDevCommand", "resume")` }); await new Promise(r => setTimeout(r, 3600)); continue; }   // technical pause (lag): resume + 3 s countdown
      const off = Number(process.env.TDF_HUNTOFF || 0) * (k % 2 ? 1 : 0);   // TDF_HUNTOFF=px: every other tap lands beside the fly (near miss: fury; in the bonus the hand lands on the face)
      if (v && v[0] >= 0) await click(v[0] + off, v[1] + off * 0.6);
      if (Number(process.env.TDF_HUNTSNAP ?? 8) > 0 && k % Number(process.env.TDF_HUNTSNAP ?? 8) === 3) { await new Promise(r => setTimeout(r, 650)); await snap(`tdf-shot-${k}.png`); }   // let the catch flash/zoom settle before the shot
      k++;
      await new Promise(r => setTimeout(r, Number(process.env.TDF_HUNTMS || 380)));
    }
    if (process.env.TDF_HUNTEND) { while (!ended && Date.now() - t0 < (secs + Number(process.env.TDF_HUNTEND)) * 1000) { const st = await evalValue("window.tdfFly && window.tdfFly.s"); if (st === 3) ended = true; else if (st === 2) send("Runtime.evaluate", { expression: `window.tdfUnity && window.tdfUnity.SendMessage("Bootstrap", "OnDevCommand", "resume")` }); await new Promise(r => setTimeout(r, 1000)); } await new Promise(r => setTimeout(r, 2500)); }   // TDF_HUNTEND=s: after the taps, wait (up to s) for the round to end, resuming technical pauses
  }
  if (process.env.TDF_DRAG) {   // TDF_DRAG=x1,y1,x2,y2: drags (e.g. scene object → hand) with shots midway and at the end
    const [x1, y1, x2, y2] = process.env.TDF_DRAG.split(",").map(Number);
    send("Input.dispatchMouseEvent", { type: "mousePressed", x: x1, y: y1, button: "left", clickCount: 1 }); await new Promise(r => setTimeout(r, 80));
    for (let k = 1; k <= 10; k++) { send("Input.dispatchMouseEvent", { type: "mouseMoved", x: x1 + (x2 - x1) * k / 10, y: y1 + (y2 - y1) * k / 10, button: "left" }); await new Promise(r => setTimeout(r, 40)); if (k === 6) await snap("tdf-drag-mid.png"); }
    await new Promise(r => setTimeout(r, 200)); await snap("tdf-drag-over.png");
    send("Input.dispatchMouseEvent", { type: "mouseReleased", x: x2, y: y2, button: "left", clickCount: 1 }); await new Promise(r => setTimeout(r, 250)); await snap("tdf-drag-fly.png"); await new Promise(r => setTimeout(r, 600)); await snap("tdf-drag-end.png");
  }
  if (process.argv[5] !== "settings" && process.argv[5] !== "start" && process.argv[5] !== "wait" && process.argv[5] !== "hunt") for (let i = 0; i < 12; i++) { await click((EMU ? EMU[0] * 0.3 : 150) + (i % 3) * (EMU ? EMU[0] * 0.18 : 90), 220 + (i % 4) * 70); await new Promise(r => setTimeout(r, Number(process.env.TDF_SNAPDELAY || 450))); if (process.argv[5] === "shots" || process.argv[5] === "boss") await snap(`tdf-shot-${i}.png`); await new Promise(r => setTimeout(r, 900 - Number(process.env.TDF_SNAPDELAY || 450))); }   // TDF_SNAPDELAY: ms between the tap and the shot (e.g. 120 to catch the object coming down) // attacks with the cloth
  if (process.argv[5] === "share") send("Runtime.evaluate", { expression: "window.tdfCardHook = function (b) { var r = new FileReader(); r.onload = function () { console.log('TDFCARD:' + r.result.split(',')[1]); }; r.readAsDataURL(b); };" });
  if (process.argv[5] === "share") {   // pause → quit → confirm → result (interrupted) → share (card)
    await click(453, 38); await new Promise(r => setTimeout(r, 800)); await click(250, 452); await new Promise(r => setTimeout(r, 800)); await click(250, 443); await new Promise(r => setTimeout(r, 2500));
    await click(180, 498); await new Promise(r => setTimeout(r, 2500));
  }
  await new Promise(r => setTimeout(r, 1500 + Number(process.env.TDF_AFTER || 0)));   // TDF_AFTER: extra ms before the final shot (e.g. 70000 to let a round end and photograph the result)
  const shot = await new Promise((resolve) => { ws.onmessage = (ev) => { const m = JSON.parse(ev.data); if (m.id === id) resolve(m.result?.data); else if (m.method === "Runtime.consoleAPICalled") out.push("[console] " + m.params.args.map(a => a.value ?? "").join(" ")); }; send("Page.captureScreenshot", { format: "png" }); });
  if (shot) { fs.writeFileSync(process.env.TEMP + "/tdf-shot.png", Buffer.from(shot, "base64")); console.log("SHOT: " + process.env.TEMP + "/tdf-shot.png"); }
  await new Promise(r => setTimeout(r, 2500));
}
send("Runtime.evaluate", { expression: "location.href + ' | ' + document.body.className + ' | ' + document.getElementById('status')?.textContent + ' | err: ' + document.getElementById('errtext')?.textContent + ' | loading visible: ' + (document.getElementById('loading')?.style.display !== 'none')", returnByValue: true });
await new Promise(r => setTimeout(r, 1500));
ws.onmessage = null;
// last result of the evaluate
const res = await new Promise((resolve) => { const w2 = ws; w2.onmessage = (ev) => { const m = JSON.parse(ev.data); if (m.id === id) resolve(m.result?.result?.value); }; send("Runtime.evaluate", { expression: "location.href + ' | ' + document.body.className + ' | ' + document.getElementById('status')?.textContent + ' | err: ' + document.getElementById('errtext')?.textContent + ' | loading visible: ' + (document.getElementById('loading')?.style.display !== 'none')", returnByValue: true }); });
for (const line of out) { const i = line.indexOf("TDFCARD:"); if (i >= 0) { fs.writeFileSync(process.env.TEMP + "/tdf-card.png", Buffer.from(line.slice(i + 8).trim(), "base64")); console.log("CARD: " + process.env.TEMP + "/tdf-card.png"); } }
console.log("STATE:", res);
const res2 = await new Promise((resolve) => { ws.onmessage = (ev) => { const m = JSON.parse(ev.data); if (m.id === id) resolve(m.result?.result?.value); }; send("Runtime.evaluate", { expression: "performance.getEntriesByType('resource').map(e => e.name.replace(location.origin, '') + ' ' + Math.round(e.duration) + 'ms ' + (e.transferSize||0)).join('\n')", returnByValue: true }); });
console.log("RESOURCES: " + res2);
for (const l of out) console.log(l.slice(0, 400));
proc.kill();
