using System.Collections.Generic;
using TUNING;
using UnityEngine;

namespace Baiye.EcologyCulture
{
    public sealed class CultureConfig : IBuildingConfig
    {
        public static readonly float[] StorageLimits={40,5,20,20,50,5,40,.5f,.1f};
        public override string[] GetRequiredDlcIds()=>DlcManager.EXPANSION1;
        public override BuildingDef CreateBuildingDef()
        {
            var def=BuildingTemplates.CreateBuildingDef(CultureIds.Building,4,4,"baiye_culture_kanim",100,60,new[]{800f},MATERIALS.REFINED_METALS,373.15f,BuildLocationRule.OnFloor,BUILDINGS.DECOR.PENALTY.TIER1,NOISE_POLLUTION.NOISY.TIER1);
            def.RequiresPowerInput=true;def.EnergyConsumptionWhenActive=960;
            def.PowerInputOffset=new CellOffset(0,1);def.Overheatable=false;
            def.InputConduitType=ConduitType.Liquid;def.UtilityInputOffset=new CellOffset(-1,0);
            def.OutputConduitType=ConduitType.Gas;def.UtilityOutputOffset=new CellOffset(1,0);
            def.LogicInputPorts=LogicOperationalController.CreateSingleInputPortList(new CellOffset(0,1));
            def.AudioCategory="HollowMetal";
            CultureCodex.Attach(def);
            return def;
        }
        public override void ConfigureBuildingTemplate(GameObject go,Tag prefabTag)
        {
            go.AddOrGet<Operational>();
            go.GetComponent<KPrefabID>().AddTag(RoomConstraints.ConstraintTags.IndustrialMachinery);
            // Stable order is part of the save contract.
            Storage water=Store(go,40,false),carbon=Store(go,5,false),nutrient=Store(go,20,false),live=Store(go,20,false),products=Store(go,50,true),oxygen=Store(go,5,false),drain=Store(go,40,false),samples=Store(go,.5f,true),import=Store(go,.1f,false);
            water.allowUIItemRemoval=carbon.allowUIItemRemoval=nutrient.allowUIItemRemoval=import.allowUIItemRemoval=true;
            foreach(CultureSpecies s in System.Enum.GetValues(typeof(CultureSpecies)))
            {
                products.storageFilters.Add(new Tag(CultureIds.Product(s)));
                foreach(CultureTrait t in System.Enum.GetValues(typeof(CultureTrait)))samples.storageFilters.Add(new Tag(CultureIds.Sample(s,t)));
            }
            products.storageFilters.Add(new Tag(CultureIds.Residue));products.storageFilters.Add(SimHashes.Salt.CreateTag());
            var liquid=Consumer(go,water,ConduitType.Liquid,false,SimHashes.Water.CreateTag(),40,10);
            var atmosphere=go.AddComponent<ElementConsumer>();atmosphere.elementToConsume=SimHashes.CarbonDioxide;
            atmosphere.storage=carbon;atmosphere.storeOnConsume=true;atmosphere.capacityKG=5;
            atmosphere.consumptionRate=.25f;atmosphere.consumptionRadius=3;atmosphere.sampleCellOffset=new Vector3(0,1,0);
            atmosphere.isRequired=false;atmosphere.showInStatusPanel=false;atmosphere.showDescriptor=false;atmosphere.ignoreActiveChanged=true;
            var outGas=go.AddComponent<ConduitDispenser>();outGas.conduitType=ConduitType.Gas;outGas.elementFilter=new[]{SimHashes.Oxygen};outGas.storage=oxygen;outGas.alwaysDispense=true;
            Deliver(go,nutrient,SimHashes.SlimeMold.CreateTag(),20,5);
            Deliver(go,import,new Tag(CultureIds.Sample(CultureSpecies.Green,CultureTrait.Base)),.05f,.05f);
            // Keep all nine storage positions and the old endpoint component for
            // save compatibility. It now has no secondary ports to register.
            go.AddOrGet<CultureSecondaryEndpoints>();go.AddOrGet<CultureProcess>();go.AddOrGet<CultureSampling>();go.AddOrGet<CultureVisuals>();
            for(int n=0;n<CultureSettingButton.Count;n++)go.AddComponent<CultureSettingButton>().Choice=n;
            Prioritizable.AddRef(go);
        }
        private static Storage Store(GameObject go,float kg,bool removable)
        {
            var store=go.AddComponent<Storage>();store.capacityKg=kg;store.showInUI=true;store.allowItemRemoval=removable;store.allowUIItemRemoval=removable;store.storageFilters=new List<Tag>();store.fetchCategory=Storage.FetchCategory.Building;
            store.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);
            return store;
        }
        private static ConduitConsumer Consumer(GameObject go,Storage store,ConduitType type,bool secondary,Tag tag,float kg,float rate)
        {
            var c=go.AddComponent<ConduitConsumer>();c.storage=store;c.conduitType=type;c.useSecondaryInput=secondary;c.capacityTag=tag;c.capacityKG=kg;c.consumptionRate=rate;c.alwaysConsume=true;c.forceAlwaysSatisfied=true;c.wrongElementResult=ConduitConsumer.WrongElementResult.Store;return c;
        }
        private static void Deliver(GameObject go,Storage storage,Tag tag,float kg,float refill)
        {
            var delivery=go.AddComponent<ManualDeliveryKG>();delivery.SetStorage(storage);delivery.RequestedItemTag=tag;delivery.capacity=kg;delivery.refillMass=refill;delivery.MinimumMass=.01f;delivery.choreTypeIDHash=Db.Get().ChoreTypes.MachineFetch.IdHash;
        }
        public override void DoPostConfigureComplete(GameObject go)
        {
            go.AddOrGet<LogicOperationalController>();go.AddOrGet<KBatchedAnimHeatPostProcessingEffect>();
        }
    }
}
