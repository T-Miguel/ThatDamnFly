from pathlib import Path
from fontTools.ttLib import TTFont
from fontTools.pens.svgPathPen import SVGPathPen
import json

import sys
ROOT=Path(sys.argv[1]).resolve() if len(sys.argv)>1 else Path(__file__).resolve().parent.parent
for d in ['svg','png','web','android','ios','source','docs']:(ROOT/d).mkdir(exist_ok=True)
INK='#202622'; PAPER='#F7F0DE'; YELLOW='#F4CD3C'; RED='#AF3D2F'
font=TTFont(ROOT/'fonts/ArchivoBlack-Regular.ttf')
glyphs=font.getGlyphSet(); cmap=font.getBestCmap()

def word(text,x,y,w,h,fill=INK):
    parts=[]; advance=0
    for c in text:
        g=glyphs[cmap[ord(c)]]; pen=SVGPathPen(glyphs); g.draw(pen)
        if pen.getCommands():parts.append(f'<path transform="translate({advance} 0)" d="{pen.getCommands()}"/>')
        advance+=g.width
    cap=font['OS/2'].sCapHeight
    return f'<g aria-label="{text}" fill="{fill}" transform="translate({x} {y+h}) scale({w/advance:.7f} {-h/cap:.7f})">'+''.join(parts)+'</g>'

def fly(ink=INK, mono=False, simple=False):
    # Original editable Bézier reconstruction of the approved character.
    # All six legs are separate geometry; no raster images are embedded.
    legs=['M148 175 L123 203 L113 233','M181 185 L189 214 L187 239','M217 182 L239 208 L255 230',
          'M123 172 L99 200 L80 231','M158 187 L147 216 L150 243','M201 190 L211 221 L231 242']
    body='M123 131 C149 119 183 132 208 154 C230 174 240 196 219 206 C197 218 153 207 128 190 C105 176 102 145 123 131 Z'
    wings=['M146 135 C145 95 173 40 201 26 C221 16 232 29 228 52 C224 77 198 112 169 141 Z',
           'M161 140 C191 106 236 67 262 70 C283 73 278 98 260 116 C237 140 201 154 170 151 Z']
    z='<g id="fly-character" stroke-linecap="round" stroke-linejoin="round">'
    if not simple:
        for i,p in enumerate(legs):z+=f'<path id="leg-{i+1}" d="{p}" stroke="{ink}" stroke-width="7" fill="none"/>'
    for i,p in enumerate(wings):z+=f'<path id="wing-{i+1}" d="{p}" fill="{PAPER if not mono else "none"}" stroke="{ink}" stroke-width="7"/>'
    z+=f'<path id="body" d="{body}" fill="{ink}"/>'
    z+=f'<path d="M77 120 Q63 91 39 87 M72 131 Q46 113 29 115" fill="none" stroke="{ink}" stroke-width="6"/>'
    if mono:
        # True negative spaces via even-odd fill, not background-colored paint.
        z+=f'<path id="head" fill="{ink}" fill-rule="evenodd" d="M52 143 A40 38 0 1 0 132 143 A40 38 0 1 0 52 143 Z M65 141 A11 21 0 1 0 87 141 A11 21 0 1 0 65 141 Z M94 141 A15 24 0 1 0 124 141 A15 24 0 1 0 94 141 Z"/>'
    else:
        z+=f'<ellipse id="head" cx="92" cy="143" rx="40" ry="38" fill="{ink}"/>'
        z+=f'<ellipse id="eye-left" cx="76" cy="141" rx="13" ry="23" fill="{RED}"/><ellipse id="eye-right" cx="108" cy="141" rx="18" ry="27" fill="{RED}"/>'
    return z+'</g>'

def place_fly(x,y,w,ink=INK,mono=False,simple=False):
    return f'<g transform="translate({x} {y}) scale({w/300})">{fly(ink,mono,simple)}</g>'

def svg(w,h,body,title):
    return f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}" role="img" aria-label="{title}"><title>{title}</title>{body}</svg>'

def save(name,w,h,body,title='That Damn Fly'):
    (ROOT/'svg'/f'{name}.svg').write_text(svg(w,h,body,title))

def stacked(variant='color'):
    light=variant in ['reverse','mono-light']; ink=PAPER if light else INK
    mono=variant.startswith('mono')
    patch=ink if mono else YELLOW
    thatfill=INK if (light or not mono) else PAPER
    b='<g transform="rotate(-2 245 155)">'
    if mono:
        b+='<defs><mask id="that-cutout" maskUnits="userSpaceOnUse" x="70" y="60" width="380" height="180"><rect x="70" y="60" width="380" height="180" fill="white"/>'+word('THAT',100,93,318,113,'black')+'</mask></defs>'
        b+=f'<rect x="78" y="74" width="360" height="156" rx="3" fill="{patch}" mask="url(#that-cutout)"/></g>'
    else:
        b+=f'<rect x="78" y="74" width="360" height="156" rx="3" fill="{patch}"/>'+word('THAT',100,93,318,113,thatfill)+'</g>'
    b+=word('DAMN',80,283,790,280,ink)+word('FLY',910,283,610,280,ink)
    b+=place_fly(1328,82,240,ink,mono)
    return b

def horizontal(variant='color'):
    light=variant in ['reverse','mono-light']; ink=PAPER if light else INK; mono=variant.startswith('mono')
    b=word('THAT',45,85,350,128,ink)+word('DAMN',430,85,440,128,ink)+word('FLY',906,85,274,128,ink)
    return b+place_fly(1212,21,240,ink,mono)

for name,fn,w,h in [('logo-stacked',stacked,1600,630),('logo-horizontal',horizontal,1490,290)]:
    for v in ['color','reverse','mono-dark','mono-light']:save(f'{name}-{v}-v1',w,h,fn(v))
save('fly-color-v1',300,270,fly(),'That Damn Fly fly symbol')
save('fly-mono-dark-v1',300,270,fly(mono=True),'That Damn Fly fly symbol')
save('fly-mono-light-v1',300,270,fly(PAPER,True),'That Damn Fly fly symbol')

# A fully opaque square master; platform masking is applied by the operating system.
icon=f'<rect width="1024" height="1024" fill="{YELLOW}"/>'+place_fly(64,100,900)
save('app-icon-master-v1',1024,1024,icon,'That Damn Fly app icon')
# All foreground geometry fits inside the central safe circle of diameter 66 dp.
fg=place_fly(21,22,66)
save('android-adaptive-foreground-v1',108,108,fg,'That Damn Fly adaptive foreground')
save('android-adaptive-background-v1',108,108,f'<rect width="108" height="108" fill="{YELLOW}"/>','Yellow background')
save('android-adaptive-monochrome-v1',108,108,place_fly(21,22,66,INK,True),'That Damn Fly monochrome foreground')
# Maskable web icon uses the same conservative center placement.
save('web-maskable-v1',108,108,f'<rect width="108" height="108" fill="{YELLOW}"/>'+fg,'That Damn Fly maskable icon')
small=f'<rect width="64" height="64" fill="{YELLOW}"/>'+place_fly(-1,4,67,simple=True)
save('favicon-v1',64,64,small,'That Damn Fly')

# Editable text counterparts for future typography work. Production SVG files above use outlines only.
for name,w,h,body in [('logo-stacked',1600,630,stacked()),('logo-horizontal',1490,290,horizontal())]:
    import re
    pattern=r'<g aria-label="([A-Z]+)" fill="([^"]+)" transform="translate\(([^ ]+) ([^)]+)\) scale\(([^ ]+) ([^)]+)\)">.*?</g>'
    def replace(m):
        text,fill,x,y,sx,sy=m.groups(); cap=font['OS/2'].sCapHeight
        # Font size is unitsPerEm so outlines and editable glyph coordinates match.
        return f'<text x="0" y="0" fill="{fill}" font-family="Archivo Black" font-size="{font["head"].unitsPerEm}" style="font-kerning:none" transform="translate({x} {y}) scale({sx} {abs(float(sy))})">{text}</text>'
    editable=re.sub(pattern,replace,body)
    (ROOT/'source'/f'{name}-editable-v1.svg').write_text(svg(w,h,editable,'That Damn Fly editable typography'))

# Presentation made from the actual exported vectors, not the concept raster.
board=f'<rect width="1600" height="1260" fill="{PAPER}"/>'
board+='<text x="70" y="60" font-family="sans-serif" font-size="20" letter-spacing="3" fill="#202622">THAT DAMN FLY / BRAND KIT 01</text>'
board+=f'<g transform="translate(0 80)">{stacked()}</g>'
board+=f'<g transform="translate(15 790) scale(.67)">{horizontal()}</g>'
board+='<text x="55" y="1010" font-family="sans-serif" font-size="28" letter-spacing="3" fill="#202622">thatdamnfly.com</text>'
board+=f'<svg x="1170" y="750" width="340" height="340" viewBox="0 0 1024 1024">{icon}</svg>'
board+='<text x="60" y="1178" font-family="sans-serif" font-size="22" fill="#202622">LOGÓTIPO PRINCIPAL · VERSÃO HORIZONTAL · ÍCONE</text>'
(ROOT/'docs/Brand_Kit_Preview.svg').write_text(svg(1600,1260,board,'That Damn Fly production brand preview'))
(ROOT/'source/brand-tokens.json').write_text(json.dumps({'brand':'That Damn Fly','domain':'thatdamnfly.com','version':'1.0','colors':{'ink':INK,'paper':PAPER,'yellow':YELLOW,'eyes':RED,'info':'#2755A5'},'logoFont':'Archivo Black','uiFont':'Archivo','accessibleName':'That Damn Fly'},indent=2))
print('Vector masters created:',len(list((ROOT/'svg').glob('*.svg'))))
