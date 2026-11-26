"""Draw the folded timer ticket and the individually composed 0–24 cut-ink values."""
import hashlib
import json
import math
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
NAME = "loan_maturity_atlas"
# Clockwise silhouettes, followed by ink counters / small cuts. Coordinates are hand
# authored in a 40-high cap box. The shared rake is applied after the individual outlines.
DIGITS = {
    0: [[(3,8),(10,1),(24,0),(29,5),(29,29),(22,38),(7,40),(1,34)],
        [(11,11),(15,8),(21,7),(21,26),(16,32),(10,32)]],
    1: [[(0,12),(11,3),(21,0),(19,31),(23,32),(21,38),(5,40),(5,34),(10,32),(12,12),(2,16)]],
    2: [[(1,9),(8,2),(24,0),(29,5),(27,15),(20,22),(11,28),(7,32),(29,28),(27,37),(0,40),(0,32),
         (7,24),(18,16),(20,11),(19,8),(11,9),(7,14),(0,16)]],
    3: [[(1,7),(11,2),(28,0),(27,8),(19,17),(26,18),(27,24),(24,32),(17,38),(4,40),(0,37),(2,31),
         (9,32),(16,30),(18,24),(17,22),(8,24),(9,18),(19,8),(2,12)]],
    4: [[(14,1),(22,0),(12,17),(7,23),(17,22),(18,14),(25,9),(24,21),(30,19),(28,26),(23,28),
         (22,38),(13,40),(15,29),(1,31),(0,24)]],
    5: [[(3,4),(28,0),(26,8),(10,10),(9,16),(19,13),(25,15),(27,21),(25,31),(17,38),(4,40),(0,36),
         (2,30),(8,32),(15,30),(18,25),(17,21),(14,20),(2,24),(0,22)]],
    6: [[(26,0),(24,7),(17,9),(11,14),(8,19),(19,15),(26,19),(27,26),(23,34),(16,39),(5,40),
         (0,35),(1,24),(5,13),(12,5),(19,2)],
        [(9,25),(15,22),(19,23),(19,27),(15,32),(8,33)]],
    7: [[(0,5),(29,0),(28,7),(23,14),(16,26),(11,38),(2,40),(8,26),(17,10),(1,13)]],
    8: [[(4,8),(10,2),(23,0),(28,5),(27,12),(22,17),(26,21),(26,28),(21,35),(14,39),(4,40),
         (0,35),(1,26),(7,20),(3,17)],
        [(11,9),(19,7),(21,9),(19,13),(13,16),(10,14)],
        [(10,25),(16,21),(19,23),(18,28),(13,32),(8,33),(7,30)]],
    9: [[(4,8),(11,2),(23,0),(29,6),(27,22),(23,31),(15,38),(4,40),(6,33),(14,29),(18,23),
         (10,25),(3,22),(1,17)],
        [(10,11),(15,8),(21,7),(19,15),(14,19),(9,18)]],
}
# Short, pointed splits sit inside the stroke. No font, highlight, shadow or paper is drawn.
SPLITS = {
    0:[(3.8,27),(5.8,14),(5.2,23)], 1:[(13.7,24),(15.5,10),(14.7,21)],
    2:[(4.5,34.3),(22,31.8),(13,33.8)], 3:[(5.2,6.8),(20.5,3.8),(14,5.8)],
    4:[(3,24.8),(13,8.8),(8,18.7)], 5:[(5.8,6.6),(21,3.8),(12,6.3)],
    6:[(4.2,25),(10,12),(7.8,19.7)], 7:[(9,31),(19.7,12.8),(16,21)],
    8:[(2.9,32),(7.1,24),(5.6,29)], 9:[(21.9,24.1),(25.4,12),(24.2,20.3)],
}
# Per-pair optical spacing, width of the second numeral, and its baseline adjustment.
# For example 11 retains two separate flags; 14 tucks into the open side of the 4.
PAIRS = {
    10:(3.0,.98,0),11:(3.7,.94,0),12:(2.8,1.00,.3),13:(2.7,.98,.2),14:(2.8,.98,-.3),
    15:(2.8,.97,.3),16:(2.8,.98,.2),17:(2.5,1.04,-.2),18:(3.1,.98,0),19:(2.9,.98,-.2),
    20:(2.6,.98,-.1),21:(2.5,.96,-.5),22:(2.8,.98,0),23:(2.8,.99,.2),24:(2.5,.99,-.3),
}
S = 10
CELL_W,CELL_H = 84,48
ATLAS_W,ATLAS_H = 512,256

def digit_image(n, stretch=1.0):
    glyph=Image.new("L",(42*S,44*S))
    draw=ImageDraw.Draw(glyph)
    def pts(poly): return [((.16*(40-y)+x)*stretch*S,(y+2)*S) for x,y in poly]
    for i,contour in enumerate(DIGITS[n]):
        draw.polygon(pts(contour),fill=255 if i==0 else 0)
    draw.polygon(pts(SPLITS[n]),fill=0)
    l,t,r,b=glyph.getbbox()
    return glyph.crop((l,0,r,glyph.height))

def compose(n):
    result=Image.new("L",(CELL_W*S,CELL_H*S))
    first=digit_image(int(str(n)[0]))
    start=3*S
    result.paste(first,(start,S))
    if n>=10:
        gap,stretch,dy=PAIRS[n]
        second=digit_image(n%10,stretch)
        vertical=round(dy*S)
        # Optical gap is measured across matching scanlines. Bounding-box spacing would
        # leave unwanted air between the sloping strokes of 11, 17 and 21.
        left=0
        for y in range(0,first.height,2):
            y2=y-vertical
            if not 0<=y2<second.height: continue
            a=first.crop((0,y,first.width,y+1)).getbbox()
            b=second.crop((0,y2,second.width,y2+1)).getbbox()
            if a and b: left=max(left,a[2]-b[0]+round(gap*S))
        result.paste(second,(start+left,S+vertical),second)
    bounds=result.getbbox()
    centered=Image.new("L",result.size)
    centered.paste(result.crop((bounds[0],0,bounds[2],result.height)),
                   ((result.width-(bounds[2]-bounds[0]))//2,0))
    return centered

def ticket(atlas):
    # A folded slip cut from a single sheet. Both the lip and the triangular turned
    # corner use the paper's reverse color. The small glass is printed on the sheet.
    im=Image.new("RGBA",(76*S,66*S))
    d=ImageDraw.Draw(im)
    ink="#272722"; paper="#FAF4E4"; reverse="#DCCFB4"; crease="#B9AA8B"
    def poly(points,color):
        d.polygon([(int(x*S),int(y*S)) for x,y in points],fill=color)
    poly([(8,14),(60,8),(68,12),(73,49),(61,62),(16,65),(11,60),(7,26)],(44,42,35,115))
    poly([(7,13),(60,7),(66,11),(71,48),(58,60),(15,64),(10,59),(6,24)],ink)
    poly([(10,17),(60,11),(63,13),(67.8,46.8),(56.4,56.6),(16.5,60.6),(13,57.4)],paper)
    poly([(11.6,18),(62,12.6),(63,15.1),(12,22.2)],reverse)
    poly([(16,58.6),(51,54),(51.5,55.1),(16.4,60.3)],reverse)
    poly([(55,47.5),(69.3,45.4),(58.4,58.5)],ink)
    poly([(58,49.3),(66.7,47.8),(59.5,54.6)],reverse)
    # One top curl with visible open mouth; asymmetric tapered facets, no rod.
    poly([(1,10),(4.8,5.8),(59,1),(66,3),(69,7.2),(68.3,12.2),(63.3,16.3),
          (11.5,21.6),(5.9,20.8),(1.8,17.8),(.5,14)],ink)
    poly([(11,8.1),(58.6,4.1),(63.2,5.9),(65.5,8.2),(64.5,10.3),(62,12.9),
          (12.7,18.2),(14.8,15.9),(15.2,12.7),(14.1,9.9)],paper)
    poly([(14.5,15.4),(60.5,11.2),(63.1,9.8),(61.4,12.3),(12.6,17.7)],reverse)
    poly([(54,5.5),(56.4,5.1),(59.5,7.4),(58.4,9.9),(56.6,10),(57.2,7.8)],crease)
    # Cut a cream spiral from the ink mouth of the rolled lip, then taper the inner end.
    poly([(4.2,15.4),(3.5,12.2),(5,9.5),(7.7,8),(10.2,8.5),(12.3,10.8),(12.7,13.9),
          (11.3,16.5),(8.8,17.5),(7,16.8),(5.8,14.9),(6,12.8),(7.7,11.5),(9.3,11.8),
          (10.1,13.1),(9.6,14.2),(8.4,14.7),(8.6,13.4),(7.7,13.2),(7.4,14.4),
          (8.1,15.6),(9.5,15.8),(11,14.4),(11,12),(9.5,10.2),(7.8,10),(5.8,11),
          (5,13.5),(5.4,15.8)],reverse)
    # Hourglass at the lower left, sharing the page angle with the numerals.
    def hx(points,color):
        poly([(15+x,28+y-x*.105) for x,y in points],color)
    hx([(0,0),(13.5,0),(12.8,2.3),(1,2.3)],ink)
    hx([(1.4,2.5),(12.2,2.5),(11,7),(8.2,9.3),(11.3,12.5),(12.4,16.1),
        (1.6,16.1),(2.4,12.1),(5.2,9.7),(2.3,6.8)],ink)
    hx([(3.8,3.5),(10,3.5),(9.3,5.9),(7.1,8),(4.6,6.2)],paper)
    hx([(4,13.4),(6.9,10.4),(9.8,13.8),(8,13),(6.5,12.7),(5,13.7)],paper)
    hx([(0,16.8),(13.6,16.8),(13.3,19),(0.4,19)],ink)
    atlas.paste(im,(432*S,2*S))

def main():
    atlas=Image.new("RGBA",(ATLAS_W*S,ATLAS_H*S),(255,255,255,0))
    for n in range(25):
        mask=compose(n)
        tile=Image.new("RGBA",mask.size,"white");tile.putalpha(mask)
        atlas.paste(tile,(int(n%5*84*S),int(n//5*50*S)))
    ticket(atlas)
    atlas=atlas.resize((2048,1024),Image.Resampling.LANCZOS)
    side={"id":NAME,"file":NAME+".png","width":2048,"height":1024,"category":"inventory",
          "spriteBorder":[0,0,0,0],"pivot":[.5,.5],"pixelsPerUnit":100,"tintable":True,
          "wrapMode":"Clamp","filterMode":"Bilinear","sourceScript":"XingShangMod/tools/draw_loan_maturity_atlas.py"}
    for art in (ROOT/"UnityUI/Assets/Art/V4", ROOT.parent/"UI/unity_assets/v4"):
        if not art.is_dir(): continue
        file=art/"inventory"/side["file"]
        atlas.save(file,optimize=True)
        def write(path,obj):path.write_text(json.dumps(obj,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
        write(file.with_suffix(".json"),side)
        manifest=art/"manifest.json"
        cfg=json.loads(manifest.read_text(encoding="utf-8"))
        cfg["assets"]=[a for a in cfg["assets"] if a["id"]!=NAME]
        entry=dict(side,file="inventory/"+side["file"],actualMode="RGBA",reused=False,
                   sha256=hashlib.sha256(file.read_bytes()).hexdigest())
        index=next(i for i,a in enumerate(cfg["assets"]) if a["category"]=="inventory")
        cfg["assets"].insert(index,entry)
        cfg["totalSpriteCount"]=len(cfg["assets"])
        cfg["generatedCount"]=sum(not a.get("reused") for a in cfg["assets"])
        cfg["categoryCounts"]["inventory"]=sum(a["category"]=="inventory" for a in cfg["assets"])
        write(manifest,cfg)

if __name__=="__main__":main()
