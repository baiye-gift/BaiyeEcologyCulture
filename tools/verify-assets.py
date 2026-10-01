"""Inspect SCML and produce a source-only mechanical animation preview."""
from pathlib import Path
from PIL import Image,ImageDraw
import xml.etree.ElementTree as ET
import re,math

ROOT=Path(__file__).resolve().parents[1]
def symbol(name):return re.sub(r'_\d+$','',Path(name).stem)
def load(name):
    folder=ROOT/'art/source'/name;root=ET.parse(folder/(name+'.scml')).getroot()
    return folder,{f.attrib['id']:f.attrib for f in root.findall('folder/file')},{a.attrib['name']:a for a in root.findall('entity/animation')}
for name in ['baiye_culture']+['baiye_culture_item'+str(n) for n in range(9)]:
    folder,files,anims=load(name)
    for file in files.values():
        with Image.open(folder/file['name']) as img:assert img.mode=='RGBA' and img.size==(int(file['width']),int(file['height']))
    for a in anims.values():
        for tl in a.findall('timeline'):
            for k in tl.findall('key'):
                assert symbol(tl.attrib['name'])==symbol(files[k.find('object').attrib['file']]['name'])
    with Image.open(folder/'ui_0.png') as img:assert img.size==(128,128)
    with Image.open(next((ROOT/'anim/assets'/name).glob('*.png'))) as img:assert max(img.size)<=2048
    if name=='baiye_culture':
        for side in ('cells','products','water'):
            assert len(anims['meter_'+side].find('timeline').findall('key'))==51
            assert not Image.open(folder/('meter_'+side+'_0.png')).getchannel('A').getbbox()
        required={'off','working_pre','working_loop','inoculating_loop','preserving_loop','harvesting','recovery_loop','cleaning_loop','working_pst'}
        assert required<=anims.keys(), 'missing stage animations'
        native=(ROOT/'anim/assets'/name/(name+'_anim.bytes')).read_bytes()
        for stage in required:
            assert stage.encode() in native, ('missing native animation',stage)
            a=anims[stage]
            timelines={t.attrib['name']:t for t in a.findall('timeline')}
            body=[tuple(sorted(k.find('object').attrib.items())) for k in timelines['body_0'].findall('key')]
            assert len(set(body))==1, 'whole building moved instead of mechanisms'
            if stage!='off':
                for moving in ('motion_0','blade0_0','flow0_0'):
                    keys=timelines[moving].findall('key')
                    assert len({tuple(sorted(k.find('object').attrib.items())) for k in keys})>3,(stage,moving)
            if stage=='harvesting':
                assert len({k.find('object').attrib['angle'] for k in timelines['valve_0'].findall('key')})>3
            if stage=='cleaning_loop':
                keys=timelines['wash_wave_0'].findall('key')
                assert len({k.find('object').attrib['y'] for k in keys})>3 and all(float(k.find('object').attrib['a'])>0 for k in keys)
            if stage.endswith('_loop'):
                keys=timelines['flow0_0'].findall('key')
                positions=[(float(k.find('object').attrib['x']),float(k.find('object').attrib['y'])) for k in keys]
                assert max(math.dist(positions[j],positions[(j+1)%len(positions)]) for j in range(len(positions)))<30, 'discontinuous fluid loop'
        for n in (1,25,50):
            with Image.open(folder/('meter_cells_'+str(n)+'.png')) as img:
                colors=set(img.get_flattened_data());assert len(colors)<=2, 'speckled culture layer'
        print('PASS all native stages, planted body, impeller/pump/fluid motion, harvest valve and uniform broth')
    print('PASS',name,'sprite references, icon and atlas bounds')

folder,files,anims=load('baiye_culture')
def obj(canvas,obj,origin,tint=None):
    a=obj.attrib;f=files[a['file']];img=Image.open(folder/f['name']).convert('RGBA')
    img=img.resize((max(1,round(img.width*float(a.get('scale_x',1)))),max(1,round(img.height*float(a.get('scale_y',1))))),Image.Resampling.LANCZOS)
    if tint:
        # Preview the same tinting done by the native MeterController.
        r,g,b,alpha=img.split();img=Image.merge('RGBA',(r.point(lambda v:round(v*tint[0])),g.point(lambda v:round(v*tint[1])),b.point(lambda v:round(v*tint[2])),alpha))
    img.putalpha(img.getchannel('A').point(lambda v:round(v*float(a.get('a',1)))))
    px=float(f.get('pivot_x',.5))*img.width;py=(1-float(f.get('pivot_y',.5)))*img.height
    angle=float(a.get('angle',0))
    if angle:img=img.rotate(angle,Image.Resampling.BICUBIC,expand=True);px=img.width/2;py=img.height/2
    canvas.alpha_composite(img,(round(origin[0]+float(a.get('x',0))-px),round(origin[1]-float(a.get('y',0))-py)))
def frame(i,ratios,tint,stage="working_loop"):
    canvas=Image.new('RGBA',(520,470),(25,34,35,255));origin=(260,440)
    ImageDraw.Draw(canvas).line((0,440,520,440),fill=(142,168,161,255),width=2)
    anim=anims[stage];i%=len(anim.find('mainline').findall('key'));refs=anim.find('mainline').findall('key')[i].findall('object_ref');tls={t.attrib['id']:t for t in anim.findall('timeline')}
    targets={}
    for ref in sorted(refs,key=lambda r:int(r.attrib['z_index'])):
        ob=tls[ref.attrib['timeline']].findall('key')[i].find('object');f=files[ob.attrib['file']]
        if '_target_' in f['name']:targets[symbol(f['name']).replace('_target','')]=ob;continue
        obj(canvas,ob,origin)
    for side,ratio in zip(('cells','products','water'),ratios):
        target=targets['meter_'+side].attrib;ob=anims['meter_'+side].find('timeline').findall('key')[round(ratio*50)].find('object')
        obj(canvas,ob,(origin[0]+float(target['x']),origin[1]-float(target['y'])),tint if side=='cells' else None)
    ImageDraw.Draw(canvas).text((12,12),'SOURCE PREVIEW: '+stage,fill=(225,238,230))
    return canvas
out=ROOT/'art/preview';out.mkdir(parents=True,exist_ok=True)
tints=((.56,.86,.58),(.42,.86,.81),(.82,.84,.51))
# Inventories are fixed example inputs, not a live-game recording.
for stage in ('working_pre','working_loop','inoculating_loop','preserving_loop','harvesting','recovery_loop','cleaning_loop','working_pst'):
    count=len(anims[stage].find('mainline').findall('key'))
    frames=[frame(i,(.7,.4,.8),tints[0],stage).convert('RGB') for i in range(count)]
    frames[0].save(out/(stage+'.gif'),save_all=True,append_images=frames[1:],duration=100,loop=0)
frames=[frame(i,(.7,.4,.8),tints[0]).convert('RGB') for i in range(24)]
frames[0].save(out/'culture-motion.gif',save_all=True,append_images=frames[1:],duration=100,loop=0)
states=Image.new('RGBA',(1560,470))
for n,(ratios,tint) in enumerate(zip(((.1,.1,.25),(.7,.65,.9),(.8,.9,.8)),tints)):
    states.alpha_composite(frame(7,ratios,tint),(520*n,0))
states.save(out/'culture-states.png')
stages=Image.new('RGBA',(1560,940))
for n,stage in enumerate(('working_pre','working_loop','preserving_loop','harvesting','cleaning_loop','off')):
    stages.alpha_composite(frame(5,(.7,.4,.8),tints[0],stage),(520*(n%3),470*(n//3)))
stages.save(out/'culture-stages.png')
print('Preview:',out/'culture-motion.gif','and culture-stages.png (source previews, not Unity renders)')
