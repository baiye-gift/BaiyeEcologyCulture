using System.Collections.Generic;
using UnityEngine;

namespace Baiye.EcologyCulture
{
    public static class CultureIds
    {
        public const string Building = "BaiyeCultureChamber", Residue = "BaiyeCultureResidue";
        public static readonly string[] Food = { "BaiyeSpirulinaPaste", "BaiyeSalineLeaves", "BaiyeAlgaePorridge", "BaiyeAlgaeCake", "BaiyeSeaweedChips", "BaiyeAlgaeWrap", "BaiyeAlgaeTofu" };
        public static readonly string[] FoodZh = { "螺旋藻泥", "盐生藻叶", "藻泥粥", "藻麦饼", "盐藻脆片", "菌菇藻卷", "藻酱豆腐" };
        public static readonly string[] FoodEn = { "Spirulina Paste", "Saline Algae Leaves", "Algae Porridge", "Algae Wheat Cakes", "Seaweed Chips", "Mushroom Algae Wrap", "Algae Tofu" };
        public static readonly float[] Calories = { 4000, 2000, 4000f/1.5f, 4100f/1.25f, 2000, 4800f/1.75f, 6100f/1.75f };
        public static readonly int[] Quality = { -1, 0, 0, 1, 1, 3, 4 };
        public static string Live(CultureSpecies s) => "BaiyeLiveCulture" + s;
        public static string Sample(CultureSpecies s,CultureTrait t) => "BaiyeCultureSample" + s + t;
        public static string Product(CultureSpecies s) => s == CultureSpecies.Green ? "Algae" : Food[s == CultureSpecies.Spirulina ? 0 : 1];
        public static bool Chinese => Localization.GetLocale() == null || Localization.GetLocale().Code.StartsWith("zh");
        public static string Text(string zh,string en) => Chinese ? zh : en;
        public static string Species(CultureSpecies s) => Text(new[]{"基础绿藻","螺旋藻","盐生藻"}[(int)s],s.ToString());
        public static string Trait(CultureTrait t) => Text(new[]{"基础株","耐热株","低耗株","速生株","高营养株"}[(int)t],t.ToString());
        public static string SampleSource(CultureTrait t)=>Text(new[]{
            "基础株可直接采样，不需筛选经历。",
            "温度至少 34°C，累计 1800 秒实际培养后采样。",
            "保种策略下累计 1800 秒实际生长后采样；满仓维护不计。",
            "食物优先策略下累计 1800 秒实际培养后采样。",
            "有机营养料库存至少 10 kg，累计 1800 秒实际培养后采样。"
        }[(int)t],new[]{
            "Base sampling needs no selection exposure.",
            "Cultivate at 34 C or above for 1800 paid growth seconds, then sample.",
            "Grow under Preserve for 1800 paid seconds, then sample; maintenance does not count.",
            "Cultivate under Food for 1800 paid growth seconds, then sample.",
            "Keep at least 10 kg organic medium during 1800 paid growth seconds, then sample."
        }[(int)t]);
        public static string FoodDescription(int n)=>Text(new[]{
            "培养仓自动采收螺旋藻所得的可食藻泥。可直接食用，或与水制粥、与冰霜小麦制饼，并用于燃气灶的藻卷与豆腐。应及时取走冷藏。",
            "培养仓自动采收盐生藻所得的可食藻叶。可直接食用、电动烤炉烤成脆片，或用于燃气灶的藻卷与豆腐。盐与不可食残渣另行收集。",
            "食物压制器把 1 kg 螺旋藻泥与 0.5 kg 水制成 1.5 kg 藻泥粥。含水增加重量，配方总热量为 4000 千卡；冷藏保存。",
            "电动烤炉把 1 kg 螺旋藻泥与 0.25 kg 冰霜小麦制成 1.25 kg 藻麦饼，整批 4100 千卡；冷藏保存。",
            "电动烤炉把 1 kg 盐生藻叶烤成 1 kg 盐藻脆片，整批 2000 千卡，品质高于生藻叶；仍需冷藏。",
            "燃气灶将 1 kg 煎蘑菇、0.5 kg 盐生藻叶与 0.25 kg 螺旋藻泥制成 1.75 kg 菌菇藻卷，整批 4800 千卡；冷藏保存。",
            "燃气灶将 1 kg 豆腐、0.5 kg 螺旋藻泥与 0.25 kg 盐生藻叶制成 1.75 kg 藻酱豆腐，整批 6100 千卡；冷藏保存。"
        }[n],new[]{
            "Edible spirulina harvest. Eat directly or use in porridge, wheat cakes, wraps and tofu. Remove promptly and refrigerate.",
            "Edible saline-algae harvest. Eat directly, grill into chips or use in wraps and tofu. Salt and inedible residue are separate products.",
            "Musher recipe: 1 kg spirulina paste + 0.5 kg water yields 1.5 kg porridge, 4000 kcal per batch. Refrigerate.",
            "Grill recipe: 1 kg spirulina paste + 0.25 kg sleet wheat yields 1.25 kg cakes, 4100 kcal per batch. Refrigerate.",
            "Grill recipe: 1 kg saline leaves yields 1 kg chips, 2000 kcal per batch with improved quality. Refrigerate.",
            "Gas range: 1 kg fried mushroom + 0.5 kg saline leaves + 0.25 kg spirulina paste yields 1.75 kg wraps, 4800 kcal per batch. Refrigerate.",
            "Gas range: 1 kg tofu + 0.5 kg spirulina paste + 0.25 kg saline leaves yields 1.75 kg algae tofu, 6100 kcal per batch. Refrigerate."
        }[n]);
    }

    // Native pickupables have distinct prefab IDs. Live culture is never edible;
    // demolition cannot bypass harvest or turn dead mother culture into food.
    public sealed class CultureItems : IMultiEntityConfig
    {
        public List<GameObject> CreatePrefabs()
        {
            var result = new List<GameObject>();
            for(int n=0;n<CultureIds.Food.Length;n++)
            {
                string id=CultureIds.Food[n],name=CultureIds.Text(CultureIds.FoodZh[n],CultureIds.FoodEn[n]);
                Strings.Add("STRINGS.ITEMS.FOOD."+id.ToUpperInvariant()+".NAME",name);
                Strings.Add("STRINGS.ITEMS.FOOD."+id.ToUpperInvariant()+".DESC",CultureIds.FoodDescription(n));
                var go=Loose(id,name,"baiye_culture_item"+n+"_kanim",SimHashes.Creature,CultureIds.FoodDescription(n));
                result.Add(EntityTemplates.ExtendEntityToFood(go,new EdiblesManager.FoodInfo(id,CultureIds.Calories[n]*1000,CultureIds.Quality[n],255.15f,277.15f,n<2?1200:3600,true)));
            }
            var residue=Loose(CultureIds.Residue,CultureIds.Text("培养残渣","Spent Culture Residue"),"baiye_culture_item8_kanim",SimHashes.Dirt,CultureIds.Text("来自采收的不可食部分、失活培养物或培养基剩余组分。不可食用或用来接种；可由复制人或清扫器取走并送入堆肥。","Inedible harvest, dead culture or spent medium. Not food or inoculum. Remove by duplicant or sweeper and compost."));
            residue.GetComponent<KPrefabID>().AddTag(GameTags.Compostable);result.Add(residue);
            foreach(CultureSpecies s in System.Enum.GetValues(typeof(CultureSpecies)))
            {
                var live=Loose(CultureIds.Live(s),CultureIds.Species(s)+CultureIds.Text("（活体）"," (live)"),"baiye_culture_item7_kanim",SimHashes.Algae,CultureIds.Species(s)+CultureIds.Text("的活体母株；在培养仓中参与实际生物量账目，不可直接食用。自动采收时才转换为该株产物和残渣；拆除不会绕过采收流程。"," mother culture. Counts as actual living biomass, not edible. Only harvest converts it into species products and residue; demolition cannot bypass harvest."));
                live.GetComponent<KPrefabID>().AddTag(GameTags.MiscPickupable);result.Add(live);
                foreach(CultureTrait t in System.Enum.GetValues(typeof(CultureTrait)))
                {
                    string id=CultureIds.Sample(s,t);
                    var go=Loose(id,CultureIds.Species(s)+" · "+CultureIds.Trait(t)+CultureIds.Text("样本"," sample"),"baiye_culture_item7_kanim",SimHashes.Algae,CultureIds.Species(s)+" · "+CultureIds.Trait(t)+CultureIds.Text("的真实种质样本。每次特殊株接种需要同种同型 50 g；选定目标并执行换种后配送。来自保样或复制人采样，不可食用。性状作用："," physical inoculum. Advanced inoculation needs 50 g of this exact species/trait after executing a culture switch. Obtained by preservation or duplicant sampling; not edible. Trait: ")+CultureIds.Text(new[]{"标准培养","温度上限 +12°C，生长 85%","培养功率和生长均为 65%","生长 125%，光合矿物需求 125%","可食比例 +15 个百分点（封顶 100%），生长 95%，光合矿物需求 125%"}[(int)t],new[]{"standard culture","maximum temperature +12 C, growth 85%","cultivation power and growth 65%","growth and photosynthetic mineral demand 125%","edible fraction +15 percentage points (cap 100%), growth 95%, photosynthetic mineral demand 125%"}[(int)t]));
                    go.GetComponent<KPrefabID>().AddTag(GameTags.MiscPickupable);result.Add(go);
                }
            }
            return result;
        }
        private static GameObject Loose(string id,string name,string anim,SimHashes element,string description)
        {
            Strings.Add("STRINGS.MISC.TAGS."+id.ToUpperInvariant(),name);
            return EntityTemplates.CreateLooseEntity(id,name,description,1,false,Assets.GetAnim(anim),"object",Grid.SceneLayer.Front,EntityTemplates.CollisionShape.RECTANGLE,.6f,.4f,true,element:element);
        }
        public void OnPrefabInit(GameObject inst) { }
        public void OnSpawn(GameObject inst)
        {
            // Also restore classification on old saved samples/live entities,
            // rather than relying solely on newly generated prefab tags.
            var id=inst.GetComponent<KPrefabID>();string name=id.PrefabTag.Name;
            if(name.StartsWith("BaiyeCultureSample",System.StringComparison.Ordinal)||name.StartsWith("BaiyeLiveCulture",System.StringComparison.Ordinal))id.AddTag(GameTags.MiscPickupable);
        }
    }
}
