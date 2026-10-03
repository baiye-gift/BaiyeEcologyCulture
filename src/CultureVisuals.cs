using UnityEngine;

namespace Baiye.EcologyCulture
{
    public sealed class CultureVisuals : KMonoBehaviour,ISim200ms
    {
        private MeterController cells,products,water;
        private KBatchedAnimController anim;
        private CultureProcess process;
        private bool? active;
        private string currentLoop;
        private int seenHarvest;
        private Storage waterStorage;
        protected override void OnSpawn()
        {
            base.OnSpawn();anim=GetComponent<KBatchedAnimController>();process=GetComponent<CultureProcess>();
            waterStorage=GetComponents<Storage>()[0];
            cells=new MeterController(anim,"meter_cells_target","meter_cells",Meter.Offset.Infront,Grid.SceneLayer.NoLayer,"meter_cells_target"){interpolateFunction=MeterController.StandardLerp};
            products=new MeterController(anim,"meter_products_target","meter_products",Meter.Offset.Infront,Grid.SceneLayer.NoLayer,"meter_products_target"){interpolateFunction=MeterController.StandardLerp};
            water=new MeterController(anim,"meter_water_target","meter_water",Meter.Offset.Infront,Grid.SceneLayer.NoLayer,"meter_water_target"){interpolateFunction=MeterController.StandardLerp};
        }
        public void Sim200ms(float dt)
        {
            cells.SetPositionPercent(Mathf.Clamp01((float)(process.State.LiveKg/CultureModel.Capacity)));products.SetPositionPercent(Mathf.Clamp01(process.Products.ExactMassStored()/50));
            water.SetPositionPercent(Mathf.Clamp01(waterStorage.ExactMassStored()/40));
            Color c=process.State.Species==CultureSpecies.Spirulina?new Color(.42f,.86f,.81f):process.State.Species==CultureSpecies.Saline?new Color(.82f,.84f,.51f):new Color(.56f,.86f,.58f);
            cells.SetSymbolTint("meter_cells",(Color32)c);
            anim.SetSymbolTint(new KAnimHashedString("warning"),(Color32)(process.State.Health>=.8?Color.white:process.State.Health>.1?new Color(1,.66f,.18f):new Color(1,.3f,.1f)));
            float now=GameClock.Instance.GetTime();
            bool running=process.Activity.Running(now,process.Operational);
            string loop=CultureActivity.Loop(process.State,process.Activity.Maintaining);
            bool harvested=seenHarvest!=process.Activity.HarvestSequence;
            seenHarvest=process.Activity.HarvestSequence;
            if(running&&harvested){anim.Play("harvesting",KAnim.PlayMode.Once);anim.Queue(loop,KAnim.PlayMode.Loop);}
            else if(running&&active!=true){anim.Play("working_pre",KAnim.PlayMode.Once);anim.Queue(loop,KAnim.PlayMode.Loop);}
            else if(running&&loop!=currentLoop){anim.Play(loop,KAnim.PlayMode.Loop);}
            else if(active==running)return;
            else if(active==true){anim.Play("working_pst",KAnim.PlayMode.Once);anim.Queue("off",KAnim.PlayMode.Loop);}
            else anim.Play("off",KAnim.PlayMode.Loop);
            active=running;currentLoop=loop;
        }
        protected override void OnCleanUp(){cells?.Unlink();products?.Unlink();water?.Unlink();base.OnCleanUp();}
    }
}
