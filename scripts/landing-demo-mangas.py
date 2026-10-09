# Genera mangas de ejemplo inventados (.cbz) para las capturas de la landing:
#   python3 scripts/landing-demo-mangas.py /tmp/demo  (ver tests/Hakufu.Tests/LandingScreenshots.cs)
import random, math, zipfile, io, os, sys
from PIL import Image, ImageDraw, ImageFont
OUT=sys.argv[1]
W,H=600,900
# La de macOS (capturas de la landing y del simulador de iOS); en Linux (emulador de Android),
# la letra de títulos de la propia app.
IMPACT="/System/Library/Fonts/Supplemental/DIN Condensed Bold.ttf"
if not os.path.exists(IMPACT):
    IMPACT=os.path.join(os.path.dirname(os.path.abspath(__file__)),"..","Assets","Fonts","HakufuDisplay-Black.ttf")
SERIES=[ # name, bg, ink, accent, vols
 ("KUROGANE",   (20,20,20),   (240,240,240),(229,0,26),  3),
 ("MAREA ROJA", (229,0,26),   (255,255,255),(20,20,20),  2),
 ("CIUDAD SIN LUNA",(38,52,92),(240,236,226),(244,196,48),2),
 ("HOSHIKAGE",  (240,236,226),(20,20,20),  (229,0,26),  3),
 ("ÚLTIMO TREN",(138,138,138),(255,255,255),(20,20,20), 1),
 ("RAIJIN CLUB",(244,196,48), (20,20,20),  (38,52,92),  2),
]
def tone(d,box,color,step=9,r=2.2):
    x0,y0,x1,y1=box
    for yy in range(int(y0),int(y1),step):
        off=(step//2) if (yy//step)%2 else 0
        for xx in range(int(x0)+off,int(x1),step):
            d.ellipse([xx-r,yy-r,xx+r,yy+r],fill=color)
def speed(d,cx,cy,box,color,n=90,rnd=None):
    pass
    for i in range(n):
        a=rnd.random()*math.tau; r0=rnd.uniform(60,140); r1=900
        d.line([cx+math.cos(a)*r0,cy+math.sin(a)*r0,cx+math.cos(a)*r1,cy+math.sin(a)*r1],fill=color,width=rnd.choice([1,2,3]))
def cover(name,bg,ink,acc,vol,rnd):
    im=Image.new("RGB",(W,H),bg); d=ImageDraw.Draw(im)
    # diagonal accent slab
    k=rnd.uniform(0.25,0.55)
    d.polygon([(0,H*k),(W,H*(k-0.25)),(W,H*(k+0.12)),(0,H*(k+0.37))],fill=acc)
    tone(d,(0,H*0.55,W,H),ink if bg!=ink else acc,step=11,r=2.4)
    speed(d,W*rnd.uniform(.3,.7),H*rnd.uniform(.35,.6),(0,0,W,H),ink,n=40,rnd=rnd)
    d.rectangle([0,H-150,W,H],fill=bg)
    f=ImageFont.truetype(IMPACT,120 if len(name)<12 else 92)
    words=name.split(" ")
    y=40
    for w in words:
        d.text((34,y),w,font=f,fill=ink); y+=f.size*0.92
    fv=ImageFont.truetype(IMPACT,160)
    d.text((W-40,H-20),str(vol),font=fv,fill=acc,anchor="rb")
    d.text((34,H-40),"HAKUFU DEMO",font=ImageFont.truetype(IMPACT,30),fill=ink,anchor="lb")
    return im
def page(rnd):
    im=Image.new("RGB",(W,H),(250,250,248)); d=ImageDraw.Draw(im)
    m=24; g=12
    rows=rnd.choice([[0.3,0.4,0.3],[0.45,0.55],[0.25,0.45,0.3]])
    y=m
    for rh in rows:
        h=(H-2*m-g*(len(rows)-1))*rh
        cols=rnd.choice([1,2,2,3])
        x=m; widths=[rnd.uniform(.7,1.3) for _ in range(cols)]; s=sum(widths)
        for wv in widths:
            w=(W-2*m-g*(cols-1))*wv/s
            box=(x,y,x+w,y+h)
            d.rectangle(box,outline=(0,0,0),width=4)
            kind=rnd.random()
            if kind<.4:
                tone(d,(box[0]+4,box[1]+h*.5,box[2]-4,box[3]-4),(40,40,40),step=7,r=1.5)
            if kind>.6:
                im2=Image.new("RGB",(int(w),int(h)),(250,250,248)); d2=ImageDraw.Draw(im2)
                speed(d2,w/2,h/2,None,(20,20,20),n=120,rnd=rnd); im.paste(im2,(int(x),int(y))); d.rectangle(box,outline=(0,0,0),width=4)
            if rnd.random()<.55:
                bw=min(w*.5,150); bh=bw*.6; bx=x+rnd.uniform(10,max(11,w-bw-10)); by=y+12
                d.ellipse([bx,by,bx+bw,by+bh],fill=(255,255,255),outline=(0,0,0),width=3)
                for i in range(3):
                    d.line([bx+bw*.25,by+bh*(.32+i*.18),bx+bw*.75,by+bh*(.32+i*.18)],fill=(120,120,120),width=3)
            x+=w+g
        y+=h+g
    return im
def png(im):
    b=io.BytesIO(); im.save(b,"PNG",optimize=True); return b.getvalue()
for si,(name,bg,ink,acc,vols) in enumerate(SERIES):
    d=os.path.join(OUT,name.title()); os.makedirs(d,exist_ok=True)
    for v in range(1,vols+1):
        rnd=random.Random(si*10+v)
        with zipfile.ZipFile(os.path.join(d,f"{name.title()} {v}.cbz"),"w") as z:
            z.writestr("000.png",png(cover(name,bg,ink,acc,v,rnd)))
            for p in range(1,7): z.writestr(f"{p:03}.png",png(page(rnd)))
print("ok")
