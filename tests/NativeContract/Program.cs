using System;
using System.IO;
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
            var recipes=ComplexRecipeManager.Get().preProcessRecipes;Check(recipes.Count==5,"recipes missing or duplicated");
            var kitchens=new System.Collections.Generic.Dictionary<string,int>();
            foreach(var recipe in recipes)
            {
                float inputs=0,outputs=0;foreach(var e in recipe.ingredients)inputs+=e.amount;foreach(var e in recipe.results)outputs+=e.amount;
                Check(Math.Abs(inputs-outputs)<1e-6,"kitchen deleted or created batch mass");
                string kitchen=recipe.fabricators[0].Name;kitchens[kitchen]=kitchens.ContainsKey(kitchen)?kitchens[kitchen]+1:1;
            }
            Check(kitchens["MicrobeMusher"]==1&&kitchens["CookingStation"]==2&&kitchens["GourmetCookingStation"]==2,"wrong kitchen dispatch");
        });
        Console.WriteLine(passed+" native managed contract scenarios passed");
    }
}
