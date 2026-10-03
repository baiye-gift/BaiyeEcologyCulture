using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using KSerialization;
using Baiye.EcologyCulture;

// Runs the game's real managed serialization on the built production state.
// No Unity objects, rendering, native pipe network or game world are started.
static class Program
{
    static string managed;
    static int passed;
    static void Main(string[] args)
    {
        managed=args[0];AppDomain.CurrentDomain.AssemblyResolve+=(_,e)=>
        {string p=Path.Combine(managed,new AssemblyName(e.Name).Name+".dll");return File.Exists(p)?Assembly.LoadFrom(p):null;};
        try{Run();}catch(Exception e){Console.Error.WriteLine(e);Environment.ExitCode=1;}
    }
    static void Check(bool v,string m){if(!v)throw new Exception(m);}
    static void Test(string n,System.Action a){a();passed++;Console.WriteLine("PASS "+n);}
    [SerializationConfig(MemberSerialization.OptIn)]
    public sealed class Envelope
    {
        [Serialize] public CultureState State;
        [Serialize] public bool Requested;
        [Serialize] public float SamplingRemaining;
    }
    static T RoundTrip<T>(T value) where T:new()
    {
        Manager.Initialize();byte[] data,directory;
        using(var s=new MemoryStream()){using(var w=new BinaryWriter(s)){Serializer.Serialize(value,w);data=s.ToArray();}}
        using(var s=new MemoryStream()){using(var w=new BinaryWriter(s)){Manager.SerializeDirectory(w);directory=s.ToArray();}}
        Manager.DeserializeDirectory(new FastReader(directory));var restored=new T();
        Check(Deserializer.Deserialize(restored,new FastReader(data)),"native deserialization failed");return restored;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Run()
    {
        Test("production process participates in native save system",()=>Check(typeof(ISaveLoadable).IsAssignableFrom(typeof(CultureProcess)),"process omitted ISaveLoadable"));
        Test("culture settings use a dedicated native screen and remove old repeated controls",()=>
        {
            Check(typeof(SideScreenContent).IsAssignableFrom(typeof(CultureSettingsSideScreen)),"native settings screen missing");
            Check(!typeof(ISidescreenButtonControl).IsAssignableFrom(typeof(CultureSettingButton)),"old twelve buttons still exposed");
            Check(CultureSettingsSideScreen.MainSelectorCount==3,"main controls not compact");
            Check(typeof(CultureSettingButton).GetField("Choice").GetCustomAttribute<UnityEngine.SerializeField>()!=null,"choice lost during prefab clone");
        });
        Test("native settings registration retains existing panels and uses one scene instance",()=>
        {
            var register=typeof(CultureMod).Assembly.GetType("Baiye.EcologyCulture.CultureSettingsRegistration").GetMethod("AddScreenReference",BindingFlags.NonPublic|BindingFlags.Static);
            Check(register!=null,"scene-owned native settings registration missing");
            var native=new DetailsScreen.SideScreenRef{name="Existing native panel"};
            var refs=new System.Collections.Generic.List<DetailsScreen.SideScreenRef>{native};
            var screen=(CultureSettingsSideScreen)RuntimeHelpers.GetUninitializedObject(typeof(CultureSettingsSideScreen));
            Check((bool)register.Invoke(null,new object[]{refs,screen}),"new screen not registered");
            Check(refs.Count==2&&ReferenceEquals(refs[0],native),"native screens changed or removed");
            Check(ReferenceEquals(refs[1].screenPrefab,screen)&&ReferenceEquals(refs[1].screenInstance,screen)&&refs[1].tab==DetailsScreen.SidescreenTabTypes.Config,"native host would clone an uninitialized global prefab");
            Check(!(bool)register.Invoke(null,new object[]{refs,screen})&&refs.Count==2,"repeated registration duplicates native content");
        });
        Test("all nine storage identities retain order and designed vessel sizes",()=>
        {
            var expected=new[]{40f,5,20,20,50,5,40,.5f,.1f};Check(CultureConfig.StorageLimits.Length==expected.Length,"saved vessel count changed");
            for(int n=0;n<expected.Length;n++)Check(CultureConfig.StorageLimits[n]==expected[n],"vessel contract changed");
            Check((int)CultureStage.Drain==4&&(int)CultureStage.Clean==5,"old save stage IDs changed");
        });
        Test("real native serialization preserves nested culture and sampling state",()=>
        {
            var e=new Envelope{State=new CultureState{Species=CultureSpecies.Saline,Trait=CultureTrait.Heat,TargetSpecies=CultureSpecies.Spirulina,TargetTrait=CultureTrait.Fast,Stage=CultureStage.Clean,Switching=true,KeepSample=true,RecoveryEdible=false,LastStress=CultureBlock.Nutrient,Health=.73,LiveKg=9.4,Seconds=13.2,HeatExposure=1820,NutritionExposure=40},Requested=true,SamplingRemaining=12.5f};
            var r=RoundTrip(e);Check(r.State!=null&&r.State.Stage==CultureStage.Clean&&r.State.Switching&&r.State.KeepSample&&!r.State.RecoveryEdible,"switch fields lost");
            Check(r.State.Species==CultureSpecies.Saline&&r.State.Trait==CultureTrait.Heat&&r.State.TargetTrait==CultureTrait.Fast,"genotype lost");
            Check(r.State.LiveKg==9.4&&r.State.Seconds==13.2&&r.State.Health==.73&&r.State.HeatExposure==1820&&r.Requested&&r.SamplingRemaining==12.5f,"progress lost");
            Check(r.State.LastStress==CultureBlock.Nutrient,"historical stress lost");
        });
        Test("native inventory accepts the sample classification and output state is saveable",()=>
        {
            Check(DiscoveredResources.GetCategoryForTags(new System.Collections.Generic.HashSet<Tag>{GameTags.MiscPickupable}).IsValid,"sample category not discoverable");
            Check(typeof(ISaveLoadable).IsAssignableFrom(typeof(CultureOutput)),"pending blocked output not saveable");
        });
        Test("unique sample prefab IDs distinguish all fifteen genotypes",()=>
        {
            var ids=new System.Collections.Generic.HashSet<string>();
            foreach(CultureSpecies s in Enum.GetValues(typeof(CultureSpecies)))foreach(CultureTrait t in Enum.GetValues(typeof(CultureTrait)))Check(ids.Add(CultureIds.Sample(s,t)),"sample identity collision");Check(ids.Count==15,"missing genotypes");
        });
        Test("production recipe registration is idempotent and preserves batch mass",()=>
        {
            ComplexRecipeManager.DestroyInstance();
            var register=typeof(CultureMod).Assembly.GetType("Baiye.EcologyCulture.CultureRecipes").GetMethod("Register",BindingFlags.NonPublic|BindingFlags.Static);
            register.Invoke(null,null);register.Invoke(null,null);
            var recipes=ComplexRecipeManager.Get().preProcessRecipes;Check(recipes.Count==8,"recipes missing or duplicated");
            var kitchens=new System.Collections.Generic.Dictionary<string,int>();
            foreach(var recipe in recipes)
            {
                float inputs=0,outputs=0;foreach(var e in recipe.ingredients)inputs+=e.amount;foreach(var e in recipe.results)outputs+=e.amount;
                Check(Math.Abs(inputs-outputs)<1e-6,"kitchen deleted or created batch mass");
                string kitchen=recipe.fabricators[0].Name;kitchens[kitchen]=kitchens.ContainsKey(kitchen)?kitchens[kitchen]+1:1;
            }
            Check(kitchens["MicrobeMusher"]==2&&kitchens["CookingStation"]==3&&kitchens["GourmetCookingStation"]==3,"wrong kitchen dispatch");
        });
        Test("all algae recipes use the native temperature contract of their kitchen",()=>
        {
            ComplexRecipeManager.DestroyInstance();
            var register=typeof(CultureMod).Assembly.GetType("Baiye.EcologyCulture.CultureRecipes").GetMethod("Register",BindingFlags.NonPublic|BindingFlags.Static);
            register.Invoke(null,null);
            var recipes=ComplexRecipeManager.Get().preProcessRecipes;
            int musher=0,heated=0;
            foreach(var recipe in recipes)
            {
                string kitchen=recipe.fabricators[0].Name;
                foreach(var result in recipe.results)
                {
                    var expected=kitchen=="MicrobeMusher"?ComplexRecipe.RecipeElement.TemperatureOperation.AverageTemperature:ComplexRecipe.RecipeElement.TemperatureOperation.Heated;
                    Check(result.temperatureOperation==expected,"unsafe temperature operation for "+result.material.Name+" in "+kitchen+": "+result.temperatureOperation);
                    if(kitchen=="MicrobeMusher"){Check(result.material.Name=="BaiyeAlgaePorridge"||result.material.Name=="BaiyeMixedAlgaeMash","unexpected musher product");musher++;}else heated++;
                }
            }
            Check(musher==2&&heated==6,"not all eight algae products were checked");
        });
        Test("new algae foods preserve identities artwork and approved recipe budgets",()=>
        {
            var ids=new System.Collections.Generic.HashSet<string>();
            string[] old={"BaiyeSpirulinaPaste","BaiyeSalineLeaves","BaiyeAlgaePorridge","BaiyeAlgaeCake","BaiyeSeaweedChips","BaiyeAlgaeWrap","BaiyeAlgaeTofu"};
            Check(CultureIds.Food.Length==10&&CultureIds.FoodZh.Length==10&&CultureIds.FoodEn.Length==10&&CultureIds.Calories.Length==10&&CultureIds.Quality.Length==10&&CultureIds.FoodAnimations.Length==10,"food metadata arrays differ");
            for(int n=0;n<CultureIds.Food.Length;n++)
            {
                Check(ids.Add(CultureIds.Food[n]),"duplicate food identity");
                Check(n>=7||CultureIds.Food[n]==old[n],"existing food identity changed");
                Check(CultureIds.FoodAnimations[n]!="baiye_culture_item7_kanim"&&CultureIds.FoodAnimations[n]!="baiye_culture_item8_kanim","food selected sample/residue art");
                string art=CultureIds.FoodAnimations[n].Replace("_kanim","");
                Check(File.Exists(Path.Combine(AppContext.BaseDirectory,"../../../../../anim/assets",art,art+"_anim.bytes")),"food animation missing: "+CultureIds.FoodAnimations[n]);
                Check(CultureIds.FoodDescription(n).Length>20,"food guide missing");
            }
            string[] products={"BaiyeAlgaePorridge","BaiyeAlgaeCake","BaiyeSeaweedChips","BaiyeAlgaeWrap","BaiyeAlgaeTofu","BaiyeMixedAlgaeMash","BaiyeSalineAlgaeCake","BaiyeMixedAlgaeStew"};
            string[][] inputs={new[]{"BaiyeSpirulinaPaste","Water"},new[]{"BaiyeSpirulinaPaste","ColdWheatSeed"},new[]{"BaiyeSalineLeaves"},new[]{"FriedMushroom","BaiyeSalineLeaves","BaiyeSpirulinaPaste"},new[]{"Tofu","BaiyeSpirulinaPaste","BaiyeSalineLeaves"},new[]{"BaiyeSpirulinaPaste","BaiyeSalineLeaves","Water"},new[]{"BaiyeSalineLeaves","ColdWheatSeed"},new[]{"FriedMushroom","BaiyeSpirulinaPaste","BaiyeSalineLeaves"}};
            float[][] amounts={new[]{1f,.5f},new[]{1f,.25f},new[]{1f},new[]{1f,.5f,.25f},new[]{1f,.5f,.25f},new[]{.5f,.5f,.5f},new[]{1f,.25f},new[]{1f,.5f,.5f}};
            float[] masses={1.5f,1.25f,1f,1.75f,1.75f,1.5f,1.25f,2f},calories={4000f,4100f,2000f,4800f,6100f,3000f,2100f,5800f};int[] quality={0,1,1,3,4,0,2,3};
            for(int n=0;n<products.Length;n++)
            {
                var recipe=ComplexRecipeManager.Get().preProcessRecipes.FirstOrDefault(r=>r.results[0].material.Name==products[n]);
                Check(recipe!=null&&recipe.time==40&&recipe.ingredients.Length==inputs[n].Length&&recipe.results.Length==1,"approved recipe missing or changed");
                for(int i=0;i<inputs[n].Length;i++)Check(recipe.ingredients[i].material.Name==inputs[n][i]&&recipe.ingredients[i].amount==amounts[n][i],"approved ingredient changed");
                Check(recipe.results[0].amount==masses[n]&&Math.Abs(CultureIds.Calories[n+2]*masses[n]-calories[n])<.01&&CultureIds.Quality[n+2]==quality[n],"approved mass calorie or quality budget changed");
            }
        });
        Console.WriteLine(passed+" native managed contract scenarios passed");
    }
}
