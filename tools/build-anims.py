"""Export generated source art plus native mechanical/density layers to SCML.

Source originals are retained. Requires Pillow and the existing Kanimal CLI.
This is the game's asset compilation pipeline, not runtime image processing.
"""
from pathlib import Path
from PIL import Image, ImageDraw
import xml.etree.ElementTree as ET
import math, subprocess, os, argparse

ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT/'art/source'
ASSETS=ROOT/'anim/assets'

def save(img,path):
    path.parent.mkdir(parents=True,exist_ok=True)
    img.save(path)

def export(name,sprites,animations,kanimal):
    work=SOURCE/name
    root=ET.Element('spriter_data',scml_version='1.0',generator='BaiyeEcologyCulture',generator_version='1.0')
    folder=ET.SubElement(root,'folder',id='0')
    ids={}
    for n,(symbol,image,pivot) in enumerate(sprites):
        filename=symbol+'.png';save(image,work/filename);ids[symbol]=n
        ET.SubElement(folder,'file',id=str(n),name=filename,width=str(image.width),height=str(image.height),pivot_x=str(pivot[0]),pivot_y=str(pivot[1]))
    entity=ET.SubElement(root,'entity',id='0',name=name)
    for ai,(aname,frames,loop) in enumerate(animations):
        a=ET.SubElement(entity,'animation',id=str(ai),name=aname,length=str(max(100,len(frames)*100)),interval='100',looping=str(loop).lower())
        main=ET.SubElement(a,'mainline')
        timelines={}
        for ki,frame in enumerate(frames):
            key=ET.SubElement(main,'key',id=str(ki),time=str(ki*100))
            for z,(timeline,sprite,attrs) in enumerate(frame):
                if timeline not in timelines:
                    timelines[timeline]=ET.SubElement(a,'timeline',id=str(len(timelines)),name=timeline)
                tl=timelines[timeline];tk=ET.SubElement(tl,'key',id=str(ki),time=str(ki*100))
                ET.SubElement(tk,'object',folder='0',file=str(ids[sprite]),**{k:str(round(v,5)) for k,v in attrs.items()})
                ET.SubElement(key,'object_ref',id=str(z),timeline=tl.attrib['id'],key=str(ki),z_index=str(z))
    ET.indent(root)
    scml=work/(name+'.scml');ET.ElementTree(root).write(scml,encoding='utf-8',xml_declaration=True)
    dest=ASSETS/name;dest.mkdir(parents=True,exist_ok=True)
    env=os.environ.copy();env['DOTNET_ROLL_FORWARD']='Major'
    subprocess.run([kanimal,'kanim',str(scml),'-o',str(dest)],env=env,check=True)
    for atlas in dest.glob('*.png'):
        with Image.open(atlas) as img:assert max(img.size)<=2048,(name,img.size)

def icon(image):
    result=Image.new('RGBA',(128,128));copy=image.copy();copy.thumbnail((116,116),Image.Resampling.LANCZOS)
    result.alpha_composite(copy,((128-copy.width)//2,(128-copy.height)//2));return result

def building(kanimal):
    original=Image.open(SOURCE/'culture-housing-v2.png').convert('RGBA')
    body=original.resize((800,800),Image.Resampling.LANCZOS)
    glass=Image.new('RGBA',(128,198));d=ImageDraw.Draw(glass)
    d.rounded_rectangle((3,3,124,194),radius=18,fill=(26,69,76,80))
    rotor=Image.open(SOURCE/'pump-rotor.png').convert('RGBA');rotor=rotor.crop(rotor.getchannel('A').getbbox());rotor=rotor.resize((39,39),Image.Resampling.LANCZOS)
    bubble=Image.new('RGBA',(6,6));ImageDraw.Draw(bubble).ellipse((0,0,5,5),outline=(213,255,241,130),width=1)
    shaft=Image.new('RGBA',(10,169));d=ImageDraw.Draw(shaft)
    d.rounded_rectangle((2,0,7,168),radius=2,fill=(126,159,162,230),outline=(30,53,62,255),width=2)
    d.line((4,3,4,164),fill=(204,232,229,240),width=2)
    blade=Image.new('RGBA',(96,16));d=ImageDraw.Draw(blade)
    d.polygon([(0,9),(6,2),(45,5),(50,0),(91,4),(95,11),(52,15),(47,10),(5,14)],fill=(112,165,174,245),outline=(27,51,60,255),width=2)
    d.line((8,4,44,7),fill=(213,238,232,255),width=2);d.line((54,3,87,6),fill=(213,238,232,255),width=2)
    flow=Image.new('RGBA',(12,28));d=ImageDraw.Draw(flow)
    d.arc((1,-6,20,25),90,210,fill=(130,219,230,145),width=2)
    d.arc((-5,0,15,27),-70,20,fill=(210,255,240,85),width=1)
    valve=Image.new('RGBA',(26,26));d=ImageDraw.Draw(valve)
    d.ellipse((1,1,24,24),fill=(57,77,83,255),outline=(20,34,40,255),width=2)
    d.line((4,13,21,13),fill=(221,162,63,255),width=4);d.line((13,4,13,21),fill=(221,162,63,255),width=4)
    d.ellipse((9,9,17,17),fill=(238,209,118,255),outline=(20,34,40,255),width=2)
    latch=Image.new('RGBA',(30,8));d=ImageDraw.Draw(latch);d.rounded_rectangle((0,0,29,7),radius=3,fill=(209,157,80,255),outline=(32,45,49,255),width=2)
    warning=Image.new('RGBA',(10,7));ImageDraw.Draw(warning).rounded_rectangle((0,0,9,6),radius=2,fill=(231,233,166,255),outline=(20,34,40,255),width=1)
    wash=Image.new('RGBA',(102,12));d=ImageDraw.Draw(wash)
    for y,alpha in ((2,85),(5,150),(9,80)):
        d.line([(x,y+2*math.sin(x/14)) for x in range(102)],fill=(153,221,244,alpha),width=2)
    target=Image.new('RGBA',(2,2));target.putpixel((0,0),(255,255,255,1))
    sprites=[('body_0',body,(.5,.05625)),('glass_0',glass,(.5,.5)),('motion_0',rotor,(.5,.5)),('shaft_0',shaft,(.5,.5)),('valve_0',valve,(.5,.5)),('latch_0',latch,(.5,.5)),('warning_0',warning,(.5,.5)),('wash_wave_0',wash,(.5,.5)),('ui_0',icon(original),(.5,.5))]
    sprites.extend(('blade'+str(j)+'_0',blade,(.5,.5)) for j in range(3))
    sprites.extend(('flow'+str(j)+'_0',flow,(.5,.5)) for j in range(4))
    sprites.extend(('bubble'+str(j)+'_0',bubble,(.5,.5)) for j in range(4))
    for side in ('cells','products','water'):
        sprites.append(('meter_'+side+'_target_0',target,(.5,.5)))
        for n in range(51):
            size=(116,180) if side=='cells' else (14,86)
            img=Image.new('RGBA',size);draw=ImageDraw.Draw(img)
            if side=='cells' and n:
                # Uniform translucent broth, never static points or mold patches.
                draw.rounded_rectangle((0,0,115,179),radius=12,fill=(255,255,255,round(n*1.6)))
            elif n:
                h=round(86*n/50);draw.rounded_rectangle((0,86-h,13,85),radius=min(5,h//2),fill=(65,189,209,230) if side=='water' else (217,192,91,230))
            sprites.append(('meter_'+side+'_'+str(n),img,(.5,.5) if side=='cells' else (.5,0)))
    def frame(i,count,kind):
        phase=i/count
        ramp=i/max(1,count-1)
        stopped=kind in ('off','on','idle','place')
        strength=ramp if kind=='working_pre' else 1-ramp if kind=='working_pst' else 0 if stopped else 1
        theta=0 if stopped else ramp*ramp*360 if kind=='working_pre' else ramp*(2-ramp)*360 if kind=='working_pst' else phase*360
        clean=kind=='cleaning_loop'
        harvest=kind in ('harvesting','recovery_loop')
        stroke=math.sin(phase*math.pi)**2 if harvest else 0
        frame=[('body_0','body_0',dict(x=0,y=0,scale_x=.512,scale_y=.512)),('glass_0','glass_0',dict(x=14,y=218)),('shaft_0','shaft_0',dict(x=14,y=219)),('motion_0','motion_0',dict(x=-140,y=279,angle=theta)),('valve_0','valve_0',dict(x=14,y=134,angle=stroke*90)),('latch_0','latch_0',dict(x=14+stroke*12,y=78)),('warning_0','warning_0',dict(x=102,y=81)),('meter_cells_target_0','meter_cells_target_0',dict(x=14,y=218)),('meter_products_target_0','meter_products_target_0',dict(x=109,y=145)),('meter_water_target_0','meter_water_target_0',dict(x=-81,y=145))]
        for j in range(3):
            c=math.cos(math.radians(theta)+j*.7)
            frame.append(('blade'+str(j)+'_0','blade'+str(j)+'_0',dict(x=14,y=166+j*51,scale_x=.2+.8*abs(c),angle=4*math.sin(math.radians(theta)+j*.7)*strength)))
        frame.append(('wash_wave_0','wash_wave_0',dict(x=14,y=218+73*math.sin(phase*2*math.pi),a=strength if clean else 0)))
        for j in range(4):
            left=j%2==0;pos=(phase+j/4)%1
            frame.append(('flow'+str(j)+'_0','flow'+str(j)+'_0',dict(x=14+42*math.cos(2*math.pi*pos),y=218+72*math.sin(2*math.pi*pos),angle=pos*360+90,scale_y=1.5 if clean else 1,a=strength*(1 if clean else .75))))
            frame.append(('bubble'+str(j)+'_0','bubble'+str(j)+'_0',dict(x=-21+j*23+2*math.sin((phase+j)*2*math.pi),y=148+((phase+j/4)%1)*145,a=strength*(.3 if clean else .5)*math.sin(math.pi*((phase+j/4)%1)))))
        return frame
    animations=[]
    for kind,count,loop in [('off',1,True),('on',1,True),('idle',1,True),('place',1,True),('working_pre',10,False),('working_loop',24,True),('inoculating_loop',32,True),('preserving_loop',48,True),('harvesting',8,False),('recovery_loop',16,True),('cleaning_loop',20,True),('working_pst',10,False)]:
        animations.append((kind,[frame(i,count,kind) for i in range(count)],loop))
    animations.append(('ui',[[('ui_0','ui_0',dict(x=0,y=0))]],True))
    for side in ('cells','products','water'):
        animations.append(('meter_'+side,[[('meter_'+side+'_0','meter_'+side+'_'+str(n),dict(x=0,y=0))] for n in range(51)],False))
    export('baiye_culture',sprites,animations,kanimal)

def items(kanimal):
    board=Image.open(SOURCE/'culture-items.png').convert('RGBA')
    for n in range(9):
        col,row=n%3,n//3
        crop=board.crop((round(board.width*col/3),round(board.height*row/3),round(board.width*(col+1)/3),round(board.height*(row+1)/3)))
        # Ignore faint isolated alpha speckles when deriving export bounds.
        # The source remains intact; all substantive artwork must fit its cell.
        box=crop.getchannel('A').point(lambda p:255 if p>64 else 0).getbbox();assert box and box[0]>0 and box[1]>0 and box[2]<crop.width and box[3]<crop.height,(n,box)
        crop=crop.crop(box);crop.thumbnail((128,128),Image.Resampling.LANCZOS)
        sprites=[('object_0',crop,(.5,.5)),('ui_0',icon(crop),(.5,.5))]
        export('baiye_culture_item'+str(n),sprites,[('object',[[('object_0','object_0',dict(x=0,y=0,scale_x=.5,scale_y=.5))]],True),('ui',[[('ui_0','ui_0',dict(x=0,y=0))]],True)],kanimal)

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--kanimal',required=True);parser.add_argument('--building-only',action='store_true');args=parser.parse_args();building(args.kanimal)
    if not args.building_only:items(args.kanimal)
