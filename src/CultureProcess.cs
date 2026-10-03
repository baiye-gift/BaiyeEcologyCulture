using System;
using KSerialization;
using Klei;
using UnityEngine;

namespace Baiye.EcologyCulture
{
    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class CultureProcess : KMonoBehaviour,ISim200ms,ISaveLoadable
    {
        [Serialize] public CultureState State=new CultureState();
        [Serialize] private CultureSpecies selectedSpecies;
        [Serialize] private CultureTrait selectedTrait;
        [Serialize] private bool keepSample=true;
        [Serialize] private bool usePollutedDirt;
        [Serialize] private bool showDetails;
        [Serialize] private bool showAdvanced;
        [Serialize] private CultureTrait sampleTrait;
        private Storage[] stores;
        private Operational operational;
        private EnergyConsumer energy;
        private ConduitConsumer[] consumers;
        private ManualDeliveryKG sampleDelivery;
        private ManualDeliveryKG mediumDelivery;
        private ElementConsumer atmosphere;
        private CultureBlock block;
        private float lastWork=float.NegativeInfinity;
        public CultureActivity Activity { get; } = new CultureActivity();
        private double bucketOxygen,bucketProduct,bucketTime,averageOxygen,averageProduct;
        private bool spawned;
        private Guid statusHandle,healthHandle,carbonHandle,inventoryHandle,ratesHandle,exposureHandle;
        private int alertSeverity=-1,healthEpisode;
        private Notification healthNotification;
        private CultureOutput output;
        public float LastWorkTime=>lastWork;
        public Storage Live=>stores[3];
        public Storage Products=>stores[4];
        public bool Operational=>operational!=null && operational.IsOperational;
        public CultureSpecies SelectedSpecies=>selectedSpecies;
        public CultureTrait SelectedTrait=>selectedTrait;
        public bool KeepSample=>keepSample;
        public bool UsePollutedDirt=>usePollutedDirt;
        public bool ShowDetails=>showDetails;
        public bool ShowAdvanced=>showAdvanced;
        public CultureTrait SampleTrait=>SampleRequested?GetComponent<CultureSampling>().RequestedTrait:sampleTrait;
        public bool NeedsChange=>!State.Switching&&(State.Species!=selectedSpecies||State.Trait!=selectedTrait);
        public bool Inactive=>State.Health<=.1;
        public bool CanReturnToBase=>State.TargetTrait!=CultureTrait.Base&&(State.Switching||State.Stage==CultureStage.Inoculate);
        public CultureOutput Output=>output;

        protected override void OnSpawn()
        {
            base.OnSpawn();stores=GetComponents<Storage>();operational=GetComponent<Operational>();energy=GetComponent<EnergyConsumer>();consumers=GetComponents<ConduitConsumer>();
            var deliveries=GetComponents<ManualDeliveryKG>();mediumDelivery=deliveries[0];sampleDelivery=deliveries[1];atmosphere=GetComponent<ElementConsumer>();
            output=GetComponent<CultureOutput>();
            if(State==null)State=new CultureState();
            if(!Enum.IsDefined(typeof(CultureSpecies),State.Species)||!Enum.IsDefined(typeof(CultureTrait),State.Trait)||!Enum.IsDefined(typeof(CultureStage),State.Stage))
                throw new InvalidOperationException("Unsupported saved culture state; refusing to rename inventory.");
            RestoreLimits();SyncLive();spawned=true;
            statusHandle=Status("STATE",Summary,ProblemDetail);
            healthHandle=Status("HEALTH",Progress,()=>CultureIds.Text("母株保留 ","Mother reserve ")+CultureModel.Reserve(State.Policy).ToString("0")+" kg · "+CultureIds.Text("当前生物量 ","Live biomass ")+State.LiveKg.ToString("0.00")+" kg");
            carbonHandle=Status("CARBON",CarbonSummary,ProblemDetail);
            RefreshDetails();
            UpdateInputs();
        }
        private void RestoreLimits()
        {
            for(int n=0;n<stores.Length&&n<CultureConfig.StorageLimits.Length;n++)
            {
                // Do not trim existing overfull native inventory.
                stores[n].capacityKg=CultureConfig.StorageLimits[n];
                stores[n].showInUI=n==0||n==2||n==4||n==7||showDetails;
                // This native property is not a serialized prefab field, so it
                // must be restored on spawned instances, not only in Config.
                stores[n].allowUIItemRemoval=n==0||n==1||n==2||n==4||n==7||n==8;
            }
            // Old fertilizer gets its own temporary allowance; it must not
            // prevent a 0.1.0 save from receiving the new carbon-bearing medium.
            stores[2].capacityKg=20+(float)Mass(2,"Fertilizer");
        }
        private Guid Status(string id,Func<string> name,Func<string> detail,int severity=0)
        {
            var status=new StatusItem("BAIYE_CULTURE_"+id,"{NAME}","{DETAIL}","",severity>0?StatusItem.IconType.Exclamation:StatusItem.IconType.Info,severity>1?NotificationType.Bad:severity>0?NotificationType.BadMinor:NotificationType.Neutral,false,OverlayModes.None.ID,showWorldIcon:severity>0);
            status.resolveStringCallback=(text,data)=>text.Replace("{NAME}",name()).Replace("{DETAIL}",detail());
            return GetComponent<KSelectable>().AddStatusItem(status,this);
        }
        private void SyncLive()=>State.LiveKg=stores[3].GetMassAvailable(new Tag(CultureIds.Live(State.Species)));
        private CultureInput Snapshot(float watts=960)
        {
            SyncLive();
            return new CultureInput{Water=Mass(0,"Water"),SaltWater=Mass(0,"SaltWater"),CO2=Mass(1,"CarbonDioxide"),Fertilizer=Mass(2,"Fertilizer"),Organic=Mass(2,"SlimeMold")+Mass(2,"ToxicSand"),OxygenRoom=Room(5),ProductRoom=Room(4),SampleRoom=Room(7),DrainRoom=Room(6),WaterRoom=Room(0),LegacyWater=Mass(6,"Water"),LegacySaltWater=Mass(6,"SaltWater"),ImportSample=Mass(8,CultureIds.Sample(State.TargetSpecies,State.TargetTrait)),TemperatureC=GetComponent<PrimaryElement>().Temperature-273.15,MaxWatts=watts};
        }
        private double Mass(int store,string id)=>stores[store].GetMassAvailable(new Tag(id));
        private double Room(int store)=>Math.Max(0,CultureConfig.StorageLimits[store]+(store==2?Mass(2,"Fertilizer"):0)-stores[store].ExactMassStored());
        public void Sim200ms(float dt)
        {
            if(!spawned)return;
            RestoreLimits();UpdateInputs();var input=Snapshot();
            bool maintained=Operational&&GameClock.Instance.GetTime()-lastWork<=.4f;
            State=CultureModel.Observe(State,input,dt,maintained,Activity.Maintaining);
            var plan=CultureModel.Step(State,input,.2,Operational);
            energy.BaseWattageRating=(float)plan.Watts;
            block=plan.Block;operational.SetActive(Operational&&plan.Worked);
            bucketTime+=dt;
            if(bucketTime>=5){averageOxygen=bucketOxygen/bucketTime;averageProduct=bucketProduct/bucketTime;bucketTime=bucketOxygen=bucketProduct=0;}
            RefreshAlerts();
        }
        private void RefreshAlerts()
        {
            int severity=Inactive&&!State.Switching?2:block!=CultureBlock.None||output.Blocked||State.Stage==CultureStage.Grow&&State.Health<.8?1:0;
            if(severity!=alertSeverity)
            {
                alertSeverity=severity;var selectable=GetComponent<KSelectable>();
                selectable.RemoveStatusItem(statusHandle);statusHandle=Status("STATE",Summary,ProblemDetail,severity);
            }
            int episode=State.Stage==CultureStage.Grow?(Inactive?2:State.Health<.8?1:0):0;
            if(episode==0){if(State.Health>=.9||State.Switching){healthEpisode=0;GetComponent<Notifier>().Remove(healthNotification);healthNotification=null;}return;}
            if(episode<=healthEpisode)return;healthEpisode=episode;
            var notifier=GetComponent<Notifier>();notifier.Remove(healthNotification);
            healthNotification=new Notification(CultureIds.Text(episode==2?"生态培养物已失活":"生态培养物健康下降",episode==2?"Culture is inactive":"Culture health is declining"),episode==2?NotificationType.Bad:NotificationType.BadMinor,(_,data)=>ProblemDetail(),expires:false);
            notifier.Add(healthNotification);
        }
        internal void Settle(float watts,bool paid)
        {
            if(!spawned || !paid || !Operational)return;
            var before=State.Stage;
            var delta=CultureModel.Step(State,Snapshot(watts),.2,true);block=delta.Block;
            if(delta.Worked && Commit(delta))
            {
                lastWork=GameClock.Instance.GetTime();bucketOxygen+=delta.Oxygen;bucketProduct+=delta.Product;
                Activity.Committed(before,delta,lastWork);
                output.Committed(before,delta);
                // Electrical dissipation only; biological chemical energy is a
                // game balance abstraction, not a claim of real LED efficiency.
                var temperatures=GameComps.StructureTemperatures;
                temperatures?.ProduceEnergy(temperatures.GetHandle(gameObject),watts*.2f/1000,"Culture lighting",.2f);
                operational.SetActive(Operational);
            }
            else operational.SetActive(false);
        }
        private void UpdateInputs()
        {
            bool accept=Room(0)>1e-7;
            consumers[0].consumptionRate=accept?10:0;
            // Any limits the entire vessel, including incorrectly supplied fluid;
            // all material stays in native storage instead of leaking on the floor.
            consumers[0].capacityTag=GameTags.Any;
            bool culture=State.Stage==CultureStage.Grow||State.Stage==CultureStage.Inoculate;
            bool recentlyPaid=GameClock.Instance!=null&&GameClock.Instance.GetTime()-lastWork<=.4f;
            atmosphere.EnableConsumption(Operational&&recentlyPaid&&!Activity.Maintaining&&culture&&Room(1)>.05&&Room(4)>0&&Room(5)>0);
            Tag medium=usePollutedDirt?SimHashes.ToxicSand.CreateTag():SimHashes.SlimeMold.CreateTag();
            if(mediumDelivery.RequestedItemTag!=medium)mediumDelivery.RequestedItemTag=medium;
            // Legacy fertilizer and alternate medium count toward the same
            // sealed input vessel. Fetching never discards those old supplies.
            mediumDelivery.capacity=(float)Math.Max(0,stores[2].capacityKg-stores[2].ExactMassStored()+stores[2].GetMassAvailable(medium));
            bool feed=culture&&Room(2)>=5;
            if(mediumDelivery.IsPaused==feed)mediumDelivery.Pause(!feed,"Culture medium request");
            bool seed=State.Stage==CultureStage.Inoculate&&State.LiveKg<CultureModel.SeedKg-1e-7&&State.TargetTrait!=CultureTrait.Base;
            Tag wanted=new Tag(CultureIds.Sample(State.TargetSpecies,State.TargetTrait));
            if(sampleDelivery.RequestedItemTag!=wanted)sampleDelivery.RequestedItemTag=wanted;
            if(sampleDelivery.IsPaused==seed)sampleDelivery.Pause(!seed,"Culture sample request");
        }

        // This is the only native material commit. Availability and destination
        // capacity are checked before the first debit; all prefab IDs exist.
        private bool Commit(CultureDelta d)
        {
            string liveId=CultureIds.Live(State.Species),sampleId=CultureIds.Sample(State.TargetSpecies,State.TargetTrait);
            if(Mass(0,"Water")+1e-7<d.Water||Mass(0,"SaltWater")+1e-7<d.SaltWater||Mass(1,"CarbonDioxide")+1e-7<d.CO2||Mass(2,"Fertilizer")+1e-7<d.Fertilizer||Mass(2,"SlimeMold")+Mass(2,"ToxicSand")+1e-7<d.Organic||Mass(3,liveId)+1e-7<d.LiveTaken||Mass(8,sampleId)+1e-7<d.SampleTaken||Mass(6,"Water")+1e-7<d.ReclaimWater||Mass(6,"SaltWater")+1e-7<d.ReclaimSaltWater)return false;
            if(Room(4)+1e-7<d.Product+d.Residue+d.Salt||Room(5)+1e-7<d.Oxygen||Room(6)+1e-7<d.DrainWater+d.DrainSaltWater||Room(7)+1e-7<d.SampleMade)return false;
            if(Room(0)+d.Water+d.SaltWater+1e-7<d.ReturnedWater)return false;
            // Drain transfers preserve liquid temperature and disease unchanged.
            if(d.DrainWater>0)stores[0].TransferMass(stores[6],new Tag("Water"),(float)d.DrainWater);
            if(d.DrainSaltWater>0)stores[0].TransferMass(stores[6],new Tag("SaltWater"),(float)d.DrainSaltWater);
            if(d.ReclaimWater>0)stores[6].TransferMass(stores[0],new Tag("Water"),(float)d.ReclaimWater,flatten:true);
            var disease=SimUtil.DiseaseInfo.Invalid;double consumed=0,weightedTemp=0;
            Debit(0,"Water",d.Water-d.DrainWater,ref disease,ref consumed,ref weightedTemp);
            Debit(0,"SaltWater",d.SaltWater-d.DrainSaltWater,ref disease,ref consumed,ref weightedTemp);
            Debit(1,"CarbonDioxide",d.CO2,ref disease,ref consumed,ref weightedTemp);
            Debit(2,"Fertilizer",d.Fertilizer,ref disease,ref consumed,ref weightedTemp);
            double slime=Math.Min(d.Organic,Mass(2,"SlimeMold"));
            Debit(2,"SlimeMold",slime,ref disease,ref consumed,ref weightedTemp);
            Debit(2,"ToxicSand",Math.Max(0,d.Organic-slime),ref disease,ref consumed,ref weightedTemp);
            Debit(6,"SaltWater",d.ReclaimSaltWater,ref disease,ref consumed,ref weightedTemp);
            Debit(3,liveId,d.LiveTaken,ref disease,ref consumed,ref weightedTemp);
            Debit(8,sampleId,d.SampleTaken,ref disease,ref consumed,ref weightedTemp);
            float temp=consumed>0?(float)(weightedTemp/consumed):GetComponent<PrimaryElement>().Temperature;
            double returned=Math.Max(0,d.ReturnedWater-d.ReclaimWater);
            double total=d.Oxygen+d.LiveAdded+d.Product+d.Salt+d.Residue+d.SampleMade+returned;
            int remaining=disease.count;
            Add(3,CultureIds.Live(d.Next.Species),d.LiveAdded,temp,disease,total,ref remaining);
            Add(4,CultureIds.Product(d.ProductSpecies),d.Product,temp,disease,total,ref remaining);
            Add(4,"Salt",d.Salt,temp,disease,total,ref remaining);
            Add(4,CultureIds.Residue,d.Residue,temp,disease,total,ref remaining);
            Add(7,CultureIds.Sample(d.SampleSpecies,d.SampleTrait),d.SampleMade,temp,disease,total,ref remaining);
            if(returned>0)
            {
                int germs=Math.Min(remaining,(int)Math.Round(disease.count*returned/Math.Max(total,1e-20)));remaining-=germs;
                stores[0].AddLiquid(SimHashes.Water,(float)returned,temp,disease.idx,germs,false);
            }
            if(d.Oxygen>0)stores[5].AddGasChunk(SimHashes.Oxygen,(float)d.Oxygen,temp,disease.idx,remaining,false);
            else if(remaining>0)
            {
                int index=returned>0?0:d.SampleMade>0?7:d.Product+d.Salt+d.Residue>0?4:3;
                string id=returned>0?"Water":d.SampleMade>0?CultureIds.Sample(d.SampleSpecies,d.SampleTrait):d.Residue>0?CultureIds.Residue:d.Salt>0?"Salt":d.Product>0?CultureIds.Product(d.ProductSpecies):CultureIds.Live(d.Next.Species);
                stores[index].FindFirst(new Tag(id))?.GetComponent<PrimaryElement>().AddDisease(disease.idx,remaining,"Culture rounding remainder");
            }
            State=d.Next;SyncLive();UpdateInputs();return true;
        }
        private void Debit(int index,string id,double kg,ref SimUtil.DiseaseInfo disease,ref double consumed,ref double weightedTemp)
        {
            if(kg<=0)return;
            stores[index].ConsumeAndGetDisease(new Tag(id),(float)kg,out float took,out var germs,out float temp);
            disease=SimUtil.CalculateFinalDiseaseInfo(disease,germs);consumed+=took;weightedTemp+=took*temp;
        }
        private void Add(int index,string id,double kg,float temp,SimUtil.DiseaseInfo disease,double total,ref int remaining)
        {
            if(kg<=0)return;
            int germs=Math.Min(remaining,(int)Math.Round(disease.count*kg/Math.Max(total,1e-20)));remaining-=germs;
            GameObject item=stores[index].FindFirst(new Tag(id));
            if(item==null)
            {
                item=Util.KInstantiate(Assets.GetPrefab(new Tag(id)),transform.position);
                var element=item.GetComponent<PrimaryElement>();element.Mass=(float)kg;element.Temperature=temp;element.AddDisease(disease.idx,germs,"Culture transaction");
                item.SetActive(true);stores[index].Store(item,true,false,false);return;
            }
            var pe=item.GetComponent<PrimaryElement>();pe.Temperature=SimUtil.CalculateFinalTemperature(pe.Mass,pe.Temperature,(float)kg,temp);pe.Mass+=(float)kg;
            pe.AddDisease(disease.idx,germs,"Culture transaction");stores[index].Trigger((int)GameHashes.OnStorageChange,item);
        }
        internal bool FinishSample(CultureTrait trait)
        {
            if(!Operational)return false;SyncLive();var d=CultureModel.SampleCulture(State,trait,Room(7));bool success=d.Worked&&Commit(d);if(success)output.Committed(State.Stage,d);return success;
        }
        internal bool CanSample(CultureTrait trait){SyncLive();return Operational&&Room(7)>=CultureModel.SeedKg&&CultureModel.CanSample(State,trait);}
        public void SelectPolicy(CulturePolicy policy)
        {
            if(!Enum.IsDefined(typeof(CulturePolicy),policy))return;
            State.Policy=policy;Refresh();
        }
        public void SelectSpecies(CultureSpecies species)
        {
            if(State.Switching||!Enum.IsDefined(typeof(CultureSpecies),species))return;
            if(selectedSpecies!=species)selectedTrait=CultureTrait.Base;
            selectedSpecies=species;Refresh();
        }
        public void SelectTrait(CultureTrait trait){if(!State.Switching&&Enum.IsDefined(typeof(CultureTrait),trait)){selectedTrait=trait;Refresh();}}
        public void SelectSampleTrait(CultureTrait trait){if(!SampleRequested&&Enum.IsDefined(typeof(CultureTrait),trait)){sampleTrait=trait;Refresh();}}
        public void ToggleKeepSample(){if(!State.Switching){keepSample=!keepSample;Refresh();}}
        public void ToggleMedium(){usePollutedDirt=!usePollutedDirt;Refresh();}
        public void SelectMedium(bool polluted){usePollutedDirt=polluted;Refresh();}
        public void ToggleAdvanced(){showAdvanced=!showAdvanced;}
        public void RestartBase()
        {
            GetComponent<CultureSampling>().Cancel();State=CultureModel.RequestBaseRestart(State);
            selectedSpecies=State.TargetSpecies;selectedTrait=CultureTrait.Base;output.Request();Refresh();
        }
        public void ChangeCulture()
        {
            if(State.Switching)return;
            GetComponent<CultureSampling>().Cancel();
            State=CultureModel.RequestSwitch(State,selectedSpecies,selectedTrait,keepSample);Refresh();
        }
        public bool SampleRequested=>GetComponent<CultureSampling>().Requested;
        public void RequestSample(){if(CanSample(sampleTrait)&&!SampleRequested)GetComponent<CultureSampling>().Request(sampleTrait);}
        public void CancelSample()=>GetComponent<CultureSampling>().Cancel();
        public string SampleReason()
        {
            if(Inactive)return CultureIds.Text("培养物失活，无法采样。请先清洗并重新接种。","Inactive culture cannot be sampled; clean and restart first.");
            if(State.Stage!=CultureStage.Grow)return CultureIds.Text("接种、清洗或换种期间不能采样。","Sampling waits until cultivation begins.");
            if(!Operational)return CultureIds.Text("缺电或建筑已禁用。","No power or disabled.");
            if(State.Health<.5)return CultureIds.Text("健康低于 50%，暂不能采样。","Health below 50%; sampling paused.");
            if(State.LiveKg<CultureModel.Reserve(State.Policy)+CultureModel.SeedKg)return CultureIds.Text("母株不足，需保留 ","Insufficient biomass; reserve ")+(CultureModel.Reserve(State.Policy)+CultureModel.SeedKg).ToString("0.00")+" kg";
            if(Room(7)<CultureModel.SeedKg)return CultureIds.Text("样本仓无空位，请排出暂存产物。","Sample buffer full; eject stored output.");
            if(!CultureModel.CanSample(State,SampleTrait))return CultureIds.Trait(SampleTrait)+CultureIds.Text("实际培养经历 "," paid cultivation exposure ")+CultureModel.Exposure(State,SampleTrait).ToString("0")+" / 1800 s\n"+CultureIds.SampleSource(SampleTrait);
            if(SampleRequested)return CultureIds.Text("采样已安排；需要可达的操作位置及有研究工作的复制人。","Sampling requested; needs a reachable work position and a duplicant permitted to research.");
            return CultureIds.Text("可安排 30 秒采样，消耗 50 g 真实母株。","Ready for 30 s sampling; consumes 50 g living culture.");
        }
        public string InoculumHelp()
        {
            var species=State.Switching?State.TargetSpecies:selectedSpecies;var trait=State.Switching?State.TargetTrait:selectedTrait;
            if(trait==CultureTrait.Base)return CultureIds.Text("基础株来自仓内种质库，无需外部样本；仍消耗水、营养料和电力培养。","Base strains come from the internal library; no sample needed. Cultivation still consumes water, medium and power.");
            double kg=Mass(8,CultureIds.Sample(species,trait));
            return CultureIds.Species(species)+" · "+CultureIds.Trait(trait)+CultureIds.Text("样本：仓内 "," sample: stored ")+(kg*1000).ToString("0")+" / 50 g\n"+CultureIds.SampleSource(trait)+"\n"+CultureIds.Text("从同种培养物安排复制人采样取得。不会天然生成；缺样本可退回基础株。","Request sampling from the same species. Not naturally generated; return to base if no sample is available.");
        }
        public void ToggleDetails(){showDetails=!showDetails;RestoreLimits();RefreshDetails();}
        private void Refresh(){UpdateInputs();Game.Instance.userMenu.Refresh(gameObject);}
        private void RefreshDetails()
        {
            var selectable=GetComponent<KSelectable>();
            foreach(var handle in new[]{inventoryHandle,ratesHandle,exposureHandle})if(handle!=Guid.Empty)selectable.RemoveStatusItem(handle);
            inventoryHandle=ratesHandle=exposureHandle=Guid.Empty;
            if(!showDetails)return;
            inventoryHandle=Status("INVENTORY",()=>CultureIds.Text("库存与回收","Inventory and recovery"),Inventory);
            ratesHandle=Status("RATES",()=>CultureIds.Text("实际产率（近 5 秒）","Actual output (last 5 s)"),()=>CultureIds.Text("氧气 ","Oxygen ")+(averageOxygen*1000).ToString("0.0")+" g/s · "+CultureIds.Text("采收 ","Harvest ")+(averageProduct*1000).ToString("0.0")+" g/s");
            exposureHandle=Status("EXPOSURE",()=>CultureIds.Text("性状筛选经历","Strain selection exposure"),()=>CultureIds.Text("耐热 / 低耗 / 速生 / 高营养：\n","Heat / dim / fast / nutritious:\n")+State.HeatExposure.ToString("0")+" / "+State.DimExposure.ToString("0")+" / "+State.FastExposure.ToString("0")+" / "+State.NutritionExposure.ToString("0")+" s\n"+CultureIds.Text("新性状取样需对应经历达到 1800 秒。","New traits require 1800 s of matching exposure."));
        }
        public static string PolicyName(CulturePolicy policy)=>CultureIds.Text(new[]{"保种","供氧优先","食物优先"}[(int)policy],policy.ToString());
        private string Summary()
        {
            if(block!=CultureBlock.None)return BlockName(block)+" · "+CultureIds.Species(State.Species);
            string phase=CultureIds.Text(new[]{"正在接种","正在培养","保留旧株样本","回收旧培养物","仓内过滤","循环清洗"}[(int)State.Stage],State.Stage.ToString());
            if(State.Stage==CultureStage.Grow&&State.Policy==CulturePolicy.Preserve&&State.LiveKg>=16)phase=CultureIds.Text("正在保种","Preserving culture");
            return phase+CultureIds.Species(State.Species)+" · "+PolicyName(State.Policy);
        }
        private string Progress()
        {
            if(Inactive&&!State.Switching)return CultureIds.Text("健康 ","Health ")+(State.Health*100).ToString("0")+CultureIds.Text("% · 已失活，需重新接种","% · inactive; restart required");
            string health=CultureIds.Text("健康 ","Health ")+(State.Health*100).ToString("0")+"%";
            if(State.Stage==CultureStage.Inoculate)return health+" · "+CultureIds.Text("接种 ","Inoculation ")+State.LiveKg.ToString("0.00")+" / 2 kg";
            if(State.Stage==CultureStage.Clean)return CultureIds.Text("循环清洗 ","Recirculating wash ")+State.Seconds.ToString("0.0")+" / 30 s";
            if(State.Switching)return CultureIds.Text("完成回收和清洗后接种：","Next inoculation after recovery: ")+CultureIds.Species(State.TargetSpecies);
            if(State.Policy==CulturePolicy.Preserve)return health+" · "+CultureIds.Text("母株 ","Mother culture ")+State.LiveKg.ToString("0.00")+" / 16 kg";
            return health+" · "+CultureIds.Text("距采收还需增长 ","Growth until harvest ")+Math.Max(0,CultureModel.Threshold(State.Policy)-State.LiveKg).ToString("0.00")+" kg";
        }
        private string CarbonSummary()
        {
            if(block!=CultureBlock.None||output.Blocked)return ProblemDetail();
            switch(Activity.Mode(State,GameClock.Instance.GetTime(),Operational,Room(5)<=1e-7))
            {
                case CultureMode.Preserve:return CultureIds.Text("保种维持 · 不自动采收","Preservation · no automatic harvest");
                case CultureMode.BufferMaintenance:return CultureIds.Text("产物暂存仓满 · 120 W 保种维护","Output buffer full · 120 W maintenance");
                case CultureMode.Photosynthetic:return CultureIds.Text("基础有机培养 + 光合增产中","Organic culture + photosynthetic bonus active");
                case CultureMode.OxygenBlocked:return CultureIds.Text("基础有机培养 · 氧缓存满，光合增产暂停","Organic culture · oxygen buffer full, bonus paused");
                case CultureMode.Organic:return CultureIds.Text("基础有机培养 · 气体净输出为零","Organic culture · zero net gas output");
                default:return CultureIds.Text("接种/换种使用有机培养基与水","Inoculation/switching uses organic medium and water");
            }
        }
        private string Inventory()=>CultureIds.Text("产物 ","Products ")+stores[4].ExactMassStored().ToString("0.00")+" / 50 kg\n"+CultureIds.Text("水 / 盐水 ","Water / salt water ")+Mass(0,"Water").ToString("0.00")+" / "+Mass(0,"SaltWater").ToString("0.00")+" kg（40 kg）\n"+CultureIds.Text("有机营养料 ","Organic medium ")+(Mass(2,"SlimeMold")+Mass(2,"ToxicSand")).ToString("0.00")+" / 20 kg\n"+CultureIds.Text("旧版肥料 ","Legacy fertilizer ")+Mass(2,"Fertilizer").ToString("0.00")+" kg\n"+CultureIds.Text("待回收旧液 ","Legacy liquid awaiting recovery ")+stores[6].ExactMassStored().ToString("0.00")+" kg\n"+CultureIds.Text("CO2 / O2 缓存 ","CO2 / O2 buffer ")+Mass(1,"CarbonDioxide").ToString("0.00")+" / "+stores[5].ExactMassStored().ToString("0.00")+" kg";
        private string BlockName(CultureBlock b)
        {
            if(b==CultureBlock.Temperature)
            {
                double c=GetComponent<PrimaryElement>().Temperature-273.15;
                return CultureIds.Text(c>CultureModel.MaxTemperature(State.Species,State.Trait)?"温度过高":"温度过低",c>CultureModel.MaxTemperature(State.Species,State.Trait)?"Too hot":"Too cold");
            }
            return CultureIds.Text(new[]{"运行条件正常","缺电或建筑已禁用","缺少水或盐水","CO2 增速暂停","缺少有机营养料","氧出口满，增速暂停","产物仓满，请搬走产物","样本仓满，请搬走样本","旧液暂存仓满","温度不适","培养物失活，请重新接种","等待对应特殊株样本"}[(int)b],b.ToString());
        }
        public string ProblemDetail()
        {
            if(Inactive&&!State.Switching)return CultureIds.Text("点击“清洗并重新接种基础株”。失活前记录：","Click Clean and restart base culture. Last recorded stress: ")+(State.LastStress==CultureBlock.None?CultureIds.Text("旧存档未记录，无法还原。","not recorded in this old save."):BlockName(State.LastStress));
            if(output.Blocked)return CultureIds.Text("出料位置被实心格挡住；腾出建筑底部/两侧空间。库存保留。","Solid cells block the output; clear space at the base/sides. Stock is retained.");
            if(block==CultureBlock.ProductFull)return CultureIds.Text("产物 ","Products ")+stores[4].ExactMassStored().ToString("0.00")+" / 50 kg · "+CultureIds.Text("下批需 4 kg 空位。点击排出暂存产物；维护条件正常时按 120 W 保种。","Next harvest needs 4 kg free. Eject stored output; valid conditions allow 120 W maintenance.");
            if(block==CultureBlock.Water)return CultureIds.Text("接入水/盐水；清洗需仓内至少 0.6 kg 水。","Supply water/salt water; cleaning needs 0.6 kg stored water.");
            if(block==CultureBlock.Nutrient)return CultureIds.Text("等待 ","Waiting for ")+CultureIds.Text(usePollutedDirt?"污染土":"菌泥",usePollutedDirt?"polluted dirt":"slime")+CultureIds.Text("配送；检查资源、路径和复制人配送权限。"," delivery; check stock, reachability and delivery permissions.");
            if(block==CultureBlock.Temperature)return BlockName(block)+" · "+CultureIds.Text("适温 ","Range ")+CultureModel.MinTemperature(State.Species).ToString("0")+"–"+CultureModel.MaxTemperature(State.Species,State.Trait).ToString("0")+"°C";
            if(block==CultureBlock.ImportSample)return InoculumHelp()+"\n"+CultureIds.Text("点击“回收清洗并退回基础株”可撤回此次接种。","Click Recover, clean and return to base to withdraw this inoculation.");
            if(block==CultureBlock.SampleFull)return CultureIds.Text("样本暂存仓满；点击排出暂存产物，并腾出出料位置。","Sample buffer full; eject output and clear the outlet.");
            if(block==CultureBlock.Power)return CultureIds.Text("检查供电、自动化信号与建筑启用状态。","Check power, automation signal and enabled state.");
            if(State.Stage==CultureStage.Grow&&State.Health<.8)return CultureIds.Text("培养物受损，最近记录：","Culture stressed; last cause: ")+BlockName(State.LastStress);
            return CultureIds.Text("自动批量出料；搬运到储物箱或冰箱。环境 CO2 可选增产。","Automatic batch output; transport to storage/fridge. Ambient CO2 is optional.");
        }
        protected override void OnCleanUp(){spawned=false;GetComponent<Notifier>()?.Remove(healthNotification);foreach(var handle in new[]{statusHandle,healthHandle,carbonHandle,inventoryHandle,ratesHandle,exposureHandle})if(handle!=Guid.Empty)GetComponent<KSelectable>()?.RemoveStatusItem(handle);base.OnCleanUp();}
    }
}
