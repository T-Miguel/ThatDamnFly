// Generates art variants from the base SVGs (own work): flies per kind (bluebottle, fruit fly) by recolouring the body,
// comic-book balloons (burst and speech) and the trail dot. Usage: node tools/art/variants.mjs && node tools/art/export.mjs
import fs from "node:fs";
import path from "node:path";

const root = path.resolve(path.dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, "$1")), "..", "..");
const src = path.join(root, "art", "svg");

// flies: only the ellipses/circles of the body change colour (legs, outlines and wings stay ink)
const kinds = {
  bb: { body: "#2E5C8A", hi: "#5B8FC4", eye: "#7A2E22" },   // bluebottle: metallic blue, dark eyes
  ff: { body: "#B8742A", hi: "#D9A05B", eye: "#D6362A" },   // fruit fly: orange-brown, bright red eyes
  hf: { body: "#4A4F3C", hi: "#6E7457", eye: "#3FA37A" },   // horsefly: greenish grey, iridescent green eyes
  bs: { body: "#5A1E1E", hi: "#8A3A3A", eye: "#F4CD3C" },   // boss fly: dark red, yellow eyes
  gd: { body: "#C99A1E", hi: "#F4CD3C", eye: "#7A1F1F" },   // golden fly (ADR-016)
};
for (const base of ["fly_top_wings0", "fly_top_wings1", "fly_top_wings2", "fly_top_flat", "fly_top_landed"]) {
  const svg = fs.readFileSync(path.join(src, base + ".svg"), "utf8");
  for (const [k, c] of Object.entries(kinds)) {
    let out = svg.replace(/(<(?:ellipse|circle)\b[^>]*?)fill="#202622"/g, `$1fill="${c.body}"`)
                 .replace(/fill="#343B36"/g, `fill="${c.hi}"`)
                 .replace(/fill="#AF3D2F"/g, `fill="${c.eye}"`);
    fs.writeFileSync(path.join(src, `fly_${k}_${base.replace("fly_top_", "")}.svg`), out);
  }
}

// burst balloon (SPLAT): irregular 14-point star, action yellow with an ink outline
function burst(seedPts) {
  const cx = 256, cy = 192, n = 14, pts = [];
  for (let i = 0; i < n * 2; i++) {
    const a = (i / (n * 2)) * Math.PI * 2 - Math.PI / 2;
    const rOuter = 1 + 0.10 * Math.sin(i * 2.3) ; const rx = i % 2 === 0 ? 240 * rOuter : 172, ry = i % 2 === 0 ? 176 * rOuter : 122;
    pts.push([cx + Math.cos(a) * rx, cy + Math.sin(a) * ry]);
  }
  return pts.map((p, i) => (i ? "L" : "M") + p[0].toFixed(1) + " " + p[1].toFixed(1)).join(" ") + " Z";
}
fs.writeFileSync(path.join(src, "balloon_burst.svg"),
  `<svg xmlns="http://www.w3.org/2000/svg" width="512" height="384" viewBox="0 0 512 384"><path d="${burst()}" fill="#F4CD3C" stroke="#202622" stroke-width="10" stroke-linejoin="round"/><path d="${burst()}" transform="translate(256 192) scale(0.86) translate(-256 -192)" fill="none" stroke="#F7F0DE" stroke-width="6" stroke-opacity="0.7" stroke-linejoin="round"/></svg>`);

// trail dot (soft circle)
fs.writeFileSync(path.join(src, "fx_trail.svg"),
  `<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 64 64"><circle cx="32" cy="32" r="26" fill="#202622"/></svg>`);
console.log("variants written");
