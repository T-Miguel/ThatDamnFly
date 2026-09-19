// Minimal web bridge: storage (localStorage), sharing (Web Share / clipboard), vibration, visibility. No remote scripts.
mergeInto(LibraryManager.library, {
  TDF_StorageAvailable: function () { try { var k = "__tdf_probe"; localStorage.setItem(k, "1"); localStorage.removeItem(k); return 1; } catch (e) { return 0; } },
  TDF_StorageRead: function (keyPtr) {
    var key = UTF8ToString(keyPtr); var v = null; try { v = localStorage.getItem(key); } catch (e) { v = null; }
    if (v === null) v = ""; var len = lengthBytesUTF8(v) + 1; var buf = _malloc(len); stringToUTF8(v, buf, len); return buf;
  },
  TDF_StorageWrite: function (keyPtr, valPtr) { try { localStorage.setItem(UTF8ToString(keyPtr), UTF8ToString(valPtr)); return 1; } catch (e) { return 0; } },
  TDF_StorageDelete: function (keyPtr) { try { localStorage.removeItem(UTF8ToString(keyPtr)); } catch (e) {} },
  TDF_Share: function (textPtr, urlPtr) {
    var text = UTF8ToString(textPtr), url = UTF8ToString(urlPtr);
    var copy = function () { try { if (navigator.clipboard && navigator.clipboard.writeText) { navigator.clipboard.writeText(text + " " + url); return 1; } } catch (e) {} return 0; };
    try {
      if (navigator.share) { navigator.share({ title: "That Damn Fly", text: text, url: url }).then(function () {}, function () { copy(); }); return 2; }
    } catch (e) {}
    return copy();
  },
  TDF_ShareImage: function (pngPtr, len, textPtr, urlPtr) {
    var text = UTF8ToString(textPtr), url = UTF8ToString(urlPtr);
    var bytes = new Uint8Array(HEAPU8.buffer, pngPtr, len).slice(0);
    var blob = new Blob([bytes], { type: "image/png" });
    try { if (window.tdfCardHook) window.tdfCardHook(blob); } catch (e) {}   // test hook (headless): receives the PNG
    var copy = function () { try { if (navigator.clipboard && navigator.clipboard.writeText) { navigator.clipboard.writeText(text + " " + url); return 1; } } catch (e) {} return 0; };
    var download = function () { try { var a = document.createElement("a"); a.href = URL.createObjectURL(blob); a.download = "thatdamnfly.png"; document.body.appendChild(a); a.click(); setTimeout(function () { URL.revokeObjectURL(a.href); a.remove(); }, 2000); return true; } catch (e) { return false; } };
    try {
      var file = new File([blob], "thatdamnfly.png", { type: "image/png" });
      if (navigator.canShare && navigator.canShare({ files: [file] })) { navigator.share({ files: [file], title: "That Damn Fly", text: text + " " + url }).then(function () {}, function () { download(); copy(); }); return 2; }
    } catch (e) {}
    var ok = download(); var c = copy();
    return (ok || c) ? 1 : 0;
  },
  TDF_DevFly: function (x, y, w, h, s) { window.tdfFly = { x: x, y: y, w: w, h: h, s: s }; },   // dev builds: first active fly in Unity screen pixels (origin bottom-left) + screen size + round state (1 playing, 2 paused, 3 ended); the headless "hunt" mode clicks there
  TDF_Vibrate: function (ms) { try { if (navigator.vibrate) navigator.vibrate(ms); } catch (e) {} },
  TDF_CopyText: function (textPtr) { try { var t = UTF8ToString(textPtr); if (navigator.clipboard && navigator.clipboard.writeText) { navigator.clipboard.writeText(t); return 1; } } catch (e) {} return 0; }
});
