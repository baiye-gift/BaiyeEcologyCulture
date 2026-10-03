using KSerialization;

namespace Baiye.EcologyCulture
{
    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class CultureSampling : Workable,ISim200ms
    {
        [Serialize] public bool Requested;
        [Serialize] private CultureTrait trait;
        [Serialize] private float remaining=30;
        private Chore chore;
        public CultureTrait RequestedTrait=>trait;
        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();SetWorkTime(30);synchronizeAnims=false;
            workAnims=new HashedString[]{"working_pre","working_loop"};
            overrideAnims=new[]{Assets.GetAnim("anim_interacts_research_center_kanim")};
            faceTargetWhenWorking=true;SetOffsets(new[]{new CellOffset(-2,0),new CellOffset(3,0)});
            attributeConverterId=Db.Get().AttributeConverters.ResearchSpeed.Id;
        }
        public void Request(CultureTrait t){if(Requested)return;trait=t;Requested=true;remaining=30;SetWorkTime(30);}
        protected override void OnSpawn(){base.OnSpawn();SetWorkTime(Requested?System.Math.Max(.01f,remaining):30);}
        protected override bool OnWorkTick(WorkerBase worker,float dt){remaining=System.Math.Max(0,WorkTimeRemaining);return base.OnWorkTick(worker,dt);}
        public void Sim200ms(float dt)
        {
            if(Requested&&chore==null&&GetComponent<CultureProcess>().CanSample(trait))
                chore=new WorkChore<CultureSampling>(Db.Get().ChoreTypes.Research,this,run_until_complete:true,only_when_operational:true);
            else if(Requested&&chore!=null&&!GetComponent<CultureProcess>().CanSample(trait))CancelChore();
        }
        protected override void OnCompleteWork(WorkerBase worker)
        {
            base.OnCompleteWork(worker);bool success=GetComponent<CultureProcess>().FinishSample(trait);Requested=!success;chore=null;remaining=success?30:1;SetWorkTime(remaining);
        }
        private void CancelChore(){chore?.Cancel("Culture sampling paused");chore=null;}
        public void Cancel(){Requested=false;CancelChore();remaining=30;SetWorkTime(30);}
        protected override void OnCleanUp(){CancelChore();base.OnCleanUp();}
    }
}
