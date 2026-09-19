"""Generates the characters of the face bonus (ADR-019): art/svg/face{k}_bg.svg (head, hair, clothes) and face{k}_over.svg (glasses, over the eyes).
Eyes and mouth are shared layers (face_eyes_*, face_mouth_*) at the same positions on every face.
Usage: python tools/art/faces.py  (then: node tools/art/export.mjs)"""
import os
root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
out = os.path.join(root, "art", "svg")

INK = "#202622"
def skin_grad(a, b): return f'<linearGradient id="skin" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{a}"/><stop offset="1" stop-color="{b}"/></linearGradient>'

def head(c):
    s = []
    # background (wall) the same for all, with a slightly different tone
    s.append(f'<rect width="768" height="1024" fill="{c["wall"]}"/>')
    s.append(f'<g stroke="{INK}" stroke-opacity="0.08" stroke-width="3"><path d="M0 200 H768 M0 400 H768 M0 600 H768 M0 800 H768 M192 0 V1024 M384 0 V1024 M576 0 V1024"/></g>')
    # shoulders and clothes
    s.append(f'<path d="M40 1024 V930 c0 -70 90 -110 200 -130 h288 c110 20 200 60 200 130 v94 Z" fill="{c["shirt"]}" stroke="{INK}" stroke-width="12" stroke-linejoin="round"/>')
    if c.get("collar"): s.append(f'<path d="M300 800 l84 90 l84 -90" fill="none" stroke="{INK}" stroke-width="10" stroke-linejoin="round"/>')
    s.append(f'<path d="M300 800 h168 v70 c0 30 -30 40 -84 40 s-84 -10 -84 -40 Z" fill="url(#skin)" stroke="{INK}" stroke-width="12" stroke-linejoin="round"/>')
    # ears (scale)
    e = c.get("ears", 1.0)
    s.append(f'<g transform="translate(132 600) scale({e}) translate(-132 -600)"><path d="M132 540 c-50 -20 -90 20 -78 80 c10 50 50 80 96 70 Z" fill="url(#skin)" stroke="{INK}" stroke-width="12" stroke-linejoin="round"/><path d="M110 590 c-14 20 -10 50 8 68" fill="none" stroke="{INK}" stroke-width="8" stroke-linecap="round" stroke-opacity="0.5"/></g>')
    s.append(f'<g transform="translate(636 600) scale({e}) translate(-636 -600)"><path d="M636 540 c50 -20 90 20 78 80 c-10 50 -50 80 -96 70 Z" fill="url(#skin)" stroke="{INK}" stroke-width="12" stroke-linejoin="round"/><path d="M658 590 c14 20 10 50 -8 68" fill="none" stroke="{INK}" stroke-width="8" stroke-linecap="round" stroke-opacity="0.5"/></g>')
    # long hair behind the head (before the head)
    if c["hair"] == "bun":
        s.append(f'<ellipse cx="384" cy="250" rx="90" ry="70" fill="{c["hairc"]}" stroke="{INK}" stroke-width="12"/>')
        s.append(f'<path d="M150 560 c-20 -160 60 -300 234 -300 s254 140 234 300 c-30 -60 -90 -100 -234 -100 s-204 40 -234 100 Z" fill="{c["hairc"]}" stroke="{INK}" stroke-width="12" stroke-linejoin="round"/>')
    if c["hair"] == "long":
        s.append(f'<path d="M120 640 c-30 -260 90 -400 264 -400 s294 140 264 400 c-40 40 -100 60 -264 60 s-224 -20 -264 -60 Z" fill="{c["hairc"]}" stroke="{INK}" stroke-width="12" stroke-linejoin="round"/>')
    # head
    w = c.get("headw", 250)
    s.append(f'<path d="M384 250 c{w*0.6:.0f} 0 {w} 120 {w} 300 c0 160 -{w-150} 300 -{w} 300 s-{w} -140 -{w} -300 c0 -180 {w-150} -300 {w} -300 Z" fill="url(#skin)" stroke="{INK}" stroke-width="12"/>')
    # hair in front
    if c["hair"] == "sides":
        s.append(f'<path d="M150 470 c10 -60 40 -110 90 -150 c-10 60 -20 110 -20 170 Z M618 470 c-10 -60 -40 -110 -90 -150 c10 60 20 110 20 170 Z" fill="{c["hairc"]}" stroke="{INK}" stroke-width="8" stroke-linejoin="round"/>')
    elif c["hair"] == "tufts":
        s.append(f'<path d="M150 480 c-40 -40 -50 -110 0 -140 c30 -18 60 10 60 60 c0 30 -20 60 -60 80 Z M618 480 c40 -40 50 -110 0 -140 c-30 -18 -60 10 -60 60 c0 30 20 60 60 80 Z" fill="{c["hairc"]}" stroke="{INK}" stroke-width="10" stroke-linejoin="round"/>')
    elif c["hair"] == "bun":
        s.append(f'<path d="M160 470 c40 -120 120 -170 224 -170 s184 50 224 170 c-60 -50 -130 -80 -224 -80 s-164 30 -224 80 Z" fill="{c["hairc"]}" stroke="{INK}" stroke-width="12" stroke-linejoin="round"/>')
    elif c["hair"] == "cap":
        s.append(f'<path d="M150 420 c20 -140 110 -210 234 -210 s214 70 234 210 c-80 -40 -150 -60 -234 -60 s-154 20 -234 60 Z" fill="{c["hairc"]}" stroke="{INK}" stroke-width="12" stroke-linejoin="round"/>')
        s.append(f'<path d="M120 420 c100 -30 428 -30 528 0 c10 20 -10 40 -40 40 c-100 -20 -348 -20 -448 0 c-30 0 -50 -20 -40 -40 Z" fill="{c["hairc"]}" stroke="{INK}" stroke-width="12" stroke-linejoin="round"/>')
        s.append(f'<circle cx="384" cy="215" r="18" fill="{c["hairc"]}" stroke="{INK}" stroke-width="10"/>')
    elif c["hair"] == "chef":
        s.append(f'<path d="M170 440 c-40 -160 60 -260 214 -260 s254 100 214 260 c-70 -30 -140 -40 -214 -40 s-144 10 -214 40 Z" fill="#FFFFFF" stroke="{INK}" stroke-width="12" stroke-linejoin="round"/>')
        s.append(f'<path d="M150 160 c30 -70 110 -90 160 -50 c30 -60 120 -60 150 0 c50 -40 130 -20 160 50 c30 70 -20 130 -70 120 c-20 60 -110 70 -150 20 c-40 50 -130 40 -150 -20 c-50 10 -100 -50 -100 -120 Z" fill="#FFFFFF" stroke="{INK}" stroke-width="12" stroke-linejoin="round"/>')
        s.append(f'<path d="M170 440 h428" fill="none" stroke="{INK}" stroke-width="8" stroke-opacity="0.35"/>')
    elif c["hair"] == "long":
        s.append(f'<path d="M160 500 c30 -150 110 -220 224 -220 s194 70 224 220 c-50 -80 -110 -120 -224 -120 s-174 40 -224 120 Z" fill="{c["hairc"]}" stroke="{INK}" stroke-width="12" stroke-linejoin="round"/>')
    # wrinkles
    if c.get("wrinkles"):
        s.append(f'<path d="M280 420 c20 -8 40 -8 60 0 M428 420 c20 -8 40 -8 60 0 M330 380 c30 -12 78 -12 108 0" fill="none" stroke="{INK}" stroke-width="6" stroke-linecap="round" stroke-opacity="0.35"/>')
        s.append(f'<path d="M250 600 c-6 30 -6 60 0 90 M518 600 c6 30 6 60 0 90" fill="none" stroke="{INK}" stroke-width="6" stroke-linecap="round" stroke-opacity="0.3"/>')
    # nose
    n = c.get("nose", "round")
    if n == "round": s.append(f'<path d="M384 545 c-30 30 -56 74 -44 104 c10 26 78 26 88 0 c12 -30 -14 -74 -44 -104 Z" fill="{c["nosec"]}" stroke="{INK}" stroke-width="12" stroke-linejoin="round"/><ellipse cx="384" cy="640" rx="34" ry="18" fill="#E8A98A" fill-opacity="0.5"/>')
    elif n == "long": s.append(f'<path d="M384 535 c-22 40 -50 90 -36 122 c10 24 62 24 72 0 c14 -32 -14 -82 -36 -122 Z" fill="{c["nosec"]}" stroke="{INK}" stroke-width="12" stroke-linejoin="round"/>')
    elif n == "button": s.append(f'<ellipse cx="384" cy="620" rx="34" ry="28" fill="{c["nosec"]}" stroke="{INK}" stroke-width="12"/>')
    elif n == "big": s.append(f'<path d="M384 530 c-40 40 -70 90 -56 130 c12 34 100 34 112 0 c14 -40 -16 -90 -56 -130 Z" fill="{c["nosec"]}" stroke="{INK}" stroke-width="12" stroke-linejoin="round"/><ellipse cx="384" cy="648" rx="40" ry="20" fill="#E8A98A" fill-opacity="0.45"/>')
    # cheeks
    s.append(f'<circle cx="230" cy="650" r="34" fill="{c["cheek"]}" fill-opacity="0.45"/><circle cx="538" cy="650" r="34" fill="{c["cheek"]}" fill-opacity="0.45"/>')
    # facial hair (below the mouth, which is a separate layer)
    fh = c.get("facial")
    if fh == "moustache": s.append(f'<path d="M290 706 c30 -30 70 -24 94 0 c24 -24 64 -30 94 0 c-20 30 -60 40 -94 22 c-34 18 -74 8 -94 -22 Z" fill="{c.get("facialc", c["hairc"])}"/>')
    elif fh == "beard": fc = c.get("facialc", c["hairc"]); s.append(f'<path d="M190 660 c20 120 90 180 194 180 s174 -60 194 -180 c-30 90 -90 130 -194 130 s-164 -40 -194 -130 Z" fill="{fc}" stroke="{INK}" stroke-width="10" stroke-linejoin="round"/><path d="M296 700 c30 -26 66 -22 88 0 c22 -22 58 -26 88 0 c-20 26 -56 36 -88 20 c-32 16 -68 6 -88 -20 Z" fill="{fc}"/>')
    elif fh == "goatee": s.append(f'<path d="M330 790 c20 40 88 40 108 0 c-10 40 -30 60 -54 60 s-44 -20 -54 -60 Z" fill="{c["hairc"]}" stroke="{INK}" stroke-width="8" stroke-linejoin="round"/>')
    # earrings
    if c.get("earrings"): s.append(f'<circle cx="90" cy="700" r="12" fill="{c["shirt"]}" stroke="{INK}" stroke-width="6"/><circle cx="678" cy="700" r="12" fill="{c["shirt"]}" stroke="{INK}" stroke-width="6"/>')
    return "\n  ".join(s)

CHARS = [
    dict(id=1, wall="#EFE6D0", shirt="#F4CD3C", skin=("#F6D2AB", "#E8B78A"), nosec="#F1C79E", cheek="#F0A98A", hair="sides", hairc=INK, facial="moustache", nose="round", ears=1.0),
    dict(id=2, wall="#E8ECE0", shirt="#2755A5", skin=("#F3D9C0", "#E6C2A2"), nosec="#EFCBAE", cheek="#E9A0A0", hair="bun", hairc="#9A9A96", nose="button", ears=0.85, glasses=True, earrings=True, wrinkles=True),
    dict(id=3, wall="#EDE3D5", shirt="#AF3D2F", skin=("#8D5A3B", "#6E4229"), nosec="#7E4F33", cheek="#B8705A", hair="cap", hairc="#2E7D57", facialc="#2A1A12", facial="beard", nose="round", ears=0.95),
    dict(id=4, wall="#EFE6D0", shirt="#CBD3B9", skin=("#F2CDB0", "#E2B48E"), nosec="#ECC2A4", cheek="#E8A0A0", hair="tufts", hairc="#F2F2EA", nose="big", ears=1.15, wrinkles=True, collar=True),
    dict(id=5, wall="#EAE4D8", shirt="#FFFFFF", skin=("#F8D7B0", "#EBBE93"), nosec="#F3CBA6", cheek="#F09A90", hair="chef", hairc=INK, facial="goatee", nose="long", ears=1.0, collar=True),
]

for c in CHARS:
    svg = f'<svg xmlns="http://www.w3.org/2000/svg" width="768" height="1024" viewBox="0 0 768 1024">\n  <defs>{skin_grad(*c["skin"])}</defs>\n  {head(c)}\n</svg>\n'
    open(os.path.join(out, f'face{c["id"]}_bg.svg'), "w", encoding="utf-8", newline="\n").write(svg)
    if c.get("glasses"):
        over = f'<svg xmlns="http://www.w3.org/2000/svg" width="400" height="160" viewBox="0 0 400 160"><g fill="none" stroke="{INK}" stroke-width="10"><circle cx="116" cy="84" r="52"/><circle cx="284" cy="84" r="52"/><path d="M168 84 h64 M64 78 l-40 -12 M336 78 l40 -12"/></g><circle cx="116" cy="84" r="46" fill="#9BC7E8" fill-opacity="0.18"/><circle cx="284" cy="84" r="46" fill="#9BC7E8" fill-opacity="0.18"/></svg>\n'
        open(os.path.join(out, f'face{c["id"]}_over.svg'), "w", encoding="utf-8", newline="\n").write(over)
print("faces:", len(CHARS))
