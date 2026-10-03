using HarmonyLib;
using System.Collections.Generic;

namespace Baiye.EcologyCulture
{
    public sealed class CultureMod : KMod.UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            string key="STRINGS.BUILDINGS.PREFABS."+CultureIds.Building.ToUpperInvariant();
            Strings.Add(key+".NAME",CultureIds.Text("生态培养仓","Ecological Culture Chamber"));
            Strings.Add(key+".DESC",CultureIds.Text("自动培养、保种和采收；一台仓同时培养一种藻类。","Automated cultivation, preservation and harvesting of one algae culture."));
            Strings.Add(key+".EFFECT",CultureIds.Text("基础有机培养：水或盐水、有机营养料（菌泥或污染土）和电力转为生物量，气体净输出为零。\n光合增产：实际吸收周围 CO2 后额外增加生物量并输出氧气；无 CO2 或氧缓存满仍可基础培养。内部代谢合并到效率、耗电和健康中，不逐步模拟呼吸。\n右侧三个下拉框选择策略、藻类和营养料，选新种类后执行换种。高级采样默认折叠。成品自动批量排出，堵仓可按 120 W 保种维护；失活可一键清洗重接种基础株。换种使用仓内过滤和循环清洗，无需 CO2 输入管或清洗排液管。\n基础株内置于封闭种质库，特殊株必须投送真实样本。产物可由复制人或清扫器取走。","Baseline organic cultivation uses water, organic medium (slime or polluted dirt) and power with zero net gas output. Actual consumed ambient CO2 enables additional photosynthetic biomass and oxygen. Internal metabolism is abstracted into efficiency, power and health. No CO2 inlet or liquid drain is needed. Use three dropdowns for strategy, species and medium, with advanced sampling folded away. Harvest ejects in batches; a full buffer allows paid 120 W maintenance. Clean and restart inactive base culture with one action. Advanced strains require physical samples."));
            base.OnLoad(harmony); // UserMod2 installs Harmony patches once.
        }
    }
    [HarmonyPatch(typeof(Db),nameof(Db.Initialize))]
    internal static class CultureRegistration
    {
        private static void Postfix()
        {
            ModUtil.AddBuildingToPlanScreen((HashedString)"Food",CultureIds.Building);
            var tech=Db.Get().Techs.Get("FoodRepurposing") ?? Db.Get().Techs.Get("Agriculture");
            if(tech!=null && !tech.unlockedItemIDs.Contains(CultureIds.Building))tech.unlockedItemIDs.Add(CultureIds.Building);
        }
    }
    // Recipe registration follows native prefab configuration so the kitchen's
    // ingredient discovery includes the new foods. IDs and amounts are stable.
    [HarmonyPatch(typeof(CookingStationConfig),nameof(CookingStationConfig.ConfigureBuildingTemplate))]
    internal static class CultureRecipes
    {
        private static void Postfix()=>Register();
        internal static void Register()
        {
            Add("MicrobeMusher",2,new[]{CultureIds.Food[0],"Water"},new[]{1f,.5f},1.5f);
            Add("CookingStation",3,new[]{CultureIds.Food[0],"ColdWheatSeed"},new[]{1f,.25f},1.25f);
            Add("CookingStation",4,new[]{CultureIds.Food[1]},new[]{1f},1f);
            Add("GourmetCookingStation",5,new[]{"FriedMushroom",CultureIds.Food[1],CultureIds.Food[0]},new[]{1f,.5f,.25f},1.75f);
            Add("GourmetCookingStation",6,new[]{"Tofu",CultureIds.Food[0],CultureIds.Food[1]},new[]{1f,.5f,.25f},1.75f);
        }
        private static void Add(string kitchen,int food,string[] names,float[] masses,float output)
        {
            var inputs=new ComplexRecipe.RecipeElement[names.Length];
            for(int n=0;n<inputs.Length;n++)inputs[n]=new ComplexRecipe.RecipeElement(new Tag(names[n]),masses[n]);
            var results=new[]{new ComplexRecipe.RecipeElement(new Tag(CultureIds.Food[food]),output,ComplexRecipe.RecipeElement.TemperatureOperation.Heated)};
            string id=ComplexRecipeManager.MakeRecipeID(kitchen,inputs,results);
            if(ComplexRecipeManager.Get().GetRecipe(id)!=null)return;
            new ComplexRecipe(id,inputs,results){time=40,nameDisplay=ComplexRecipe.RecipeNameDisplay.Result,description=CultureIds.Text("将培养仓的藻类烹制成食物。","Cook cultivated algae."),fabricators=new List<Tag>{new Tag(kitchen)}};
        }
    }
    [HarmonyPatch(typeof(EnergyConsumer),nameof(EnergyConsumer.SetConnectionStatus))]
    internal static class CulturePaidPower
    {
        private static void Prefix(EnergyConsumer __instance,out float __state)=>__state=__instance.GetComponent<CultureProcess>()!=null?__instance.WattsUsed:0;
        private static void Postfix(EnergyConsumer __instance,CircuitManager.ConnectionStatus connection_status,float __state)
        {
            if(__state>0)__instance.GetComponent<CultureProcess>()?.Settle(__state,connection_status==CircuitManager.ConnectionStatus.Powered);
        }
    }
}
