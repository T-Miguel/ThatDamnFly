// Static test server for the Unity Web build: MIME for .wasm, Content-Encoding for .br/.gz, immutable paths with long cache,
// entry page without cache. Usage: node web/serve.mjs <dir> [port]. Local tests/tunnel only; the real publication uses Bluehost + .htaccess.
import http from "node:http";
import fs from "node:fs";
import path from "node:path";

const root = path.resolve(process.argv[2] || "dist/web-dev");
const port = Number(process.argv[3] || 8080);
const types = { ".html": "text/html; charset=utf-8", ".js": "application/javascript", ".wasm": "application/wasm", ".data": "application/octet-stream", ".json": "application/json", ".png": "image/png", ".svg": "image/svg+xml", ".ico": "image/x-icon", ".tdfm": "application/octet-stream", ".apk": "application/vnd.android.package-archive", ".txt": "text/plain; charset=utf-8", ".md": "text/markdown; charset=utf-8", ".css": "text/css" };

http.createServer((req, res) => {
  let url = decodeURIComponent((req.url || "/").split("?")[0]);
  if (url.endsWith("/")) url += "index.html";
  const file = path.normalize(path.join(root, url));
  if (!file.startsWith(root)) { res.writeHead(403); return res.end(); }
  fs.stat(file, (err, st) => {
    if (err || !st.isFile()) { res.writeHead(404); return res.end("not found: " + url); }
    let ext = path.extname(file).toLowerCase(); const headers = {};
    if (ext === ".br" || ext === ".gz") { headers["Content-Encoding"] = ext === ".br" ? "br" : "gzip"; ext = path.extname(file.slice(0, -ext.length)).toLowerCase(); }
    headers["Content-Type"] = types[ext] || "application/octet-stream";
    headers["Content-Length"] = st.size;
    headers["Cache-Control"] = url.endsWith("index.html") ? "no-cache" : (url.startsWith("/Build/") || url.startsWith("/model/") ? "public, max-age=31536000, immutable" : "public, max-age=3600");
    headers["Access-Control-Allow-Origin"] = "*";
    res.writeHead(200, headers);
    fs.createReadStream(file).pipe(res);
  });
}).listen(port, "0.0.0.0", () => console.log(`serving ${root} on http://0.0.0.0:${port}`));
