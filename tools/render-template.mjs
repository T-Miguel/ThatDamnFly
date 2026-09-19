// Renders app/Assets/WebGLTemplates/TDF/index.html into a built web folder WITHOUT Unity, when only the page (not the game) changed:
// fills Unity's placeholders from the existing Build/ files, resolves the #if blocks the way the WebGL build does (wasm on, threads/memory/symbols off),
// writes the static texts in English (the page still switches language on load), and copies TemplateData/ (screenshots, icons).
// Usage: node tools/render-template.mjs [dist/web-release]
import fs from "node:fs";
import path from "node:path";
const root = path.resolve(path.dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, "$1")), "..");
const out = path.resolve(root, process.argv[2] || "dist/web-release");
const tplDir = path.join(root, "app/Assets/WebGLTemplates/TDF");
let html = fs.readFileSync(path.join(tplDir, "index.html"), "utf8");
const build = fs.readdirSync(path.join(out, "Build"));
const pick = (suffix) => { const f = build.filter(n => n.endsWith(suffix)); if (f.length !== 1) throw new Error(`Build/: expected exactly one *${suffix}, found ${f.length} - run a Unity build first`); return f[0]; };
const version = /Version = "([^"]+)"/.exec(fs.readFileSync(path.join(root, "app/Assets/Editor/ProjectSetup.cs"), "utf8"))[1];
const built = fs.existsSync(path.join(out, "index.html")) ? fs.readFileSync(path.join(out, "index.html"), "utf8") : "";
const builtVersion = /productVersion: "([^"]+)"/.exec(built)?.[1] || version;
if (builtVersion !== version) console.log(`note: the Build/ files are version ${builtVersion}; the page will say ${builtVersion} until the game is rebuilt`);
const vars = { LOADER_FILENAME: pick(".loader.js"), DATA_FILENAME: pick(".data.br"), FRAMEWORK_FILENAME: pick(".framework.js.br"), CODE_FILENAME: pick(".wasm.br"), PRODUCT_VERSION: builtVersion };
// #if blocks (Unity's template preprocessor): keep USE_WASM, drop the others
html = html.replace(/#if (USE_THREADS|MEMORY_FILENAME|SYMBOLS_FILENAME)\n[\s\S]*?#endif\n/g, "").replace(/#if USE_WASM\n([\s\S]*?)#endif\n/g, "$1");
html = html.replace(/\{\{\{ JSON\.stringify\(COMPANY_NAME\) \}\}\}/g, '"That Damn Fly"').replace(/\{\{\{ JSON\.stringify\(PRODUCT_NAME\) \}\}\}/g, '"That Damn Fly"').replace(/\{\{\{ JSON\.stringify\(PRODUCT_VERSION\) \}\}\}/g, JSON.stringify(builtVersion));
for (const [k, v] of Object.entries(vars)) html = html.replace(new RegExp(`\\{\\{\\{ ${k} \\}\\}\\}`, "g"), v);
if (/\{\{\{|#if |#endif/.test(html)) throw new Error("unresolved template markers remain");
// static texts in English (crawlers and no-JS readers); the language switch on load still applies PT/ES
const m = /var I18N = (\{[\s\S]*?\n      \});/.exec(html); const I18N = new Function("return " + m[1])();
const en = I18N.en;
html = html.replace(/(<[a-z0-9]+[^>]*data-i18n="([^"]+)"[^>]*>)([\s\S]*?)(<\/[a-z0-9]+>)/g, (all, open, key, inner, close) => en[key] != null && !inner.includes("<div") && !inner.includes("<figure") ? open + en[key] + close : all);
fs.writeFileSync(path.join(out, "index.html"), html);
// TemplateData (icons, screenshots, og image) from the template folder, without Unity's .meta files
const copyDir = (src, dst) => { fs.mkdirSync(dst, { recursive: true }); for (const e of fs.readdirSync(src, { withFileTypes: true })) { if (e.name.endsWith(".meta")) continue; const s = path.join(src, e.name), d = path.join(dst, e.name); e.isDirectory() ? copyDir(s, d) : fs.copyFileSync(s, d); } };
copyDir(path.join(tplDir, "TemplateData"), path.join(out, "TemplateData"));
console.log(`rendered ${path.relative(root, path.join(out, "index.html"))} (game ${builtVersion}) + TemplateData`);
