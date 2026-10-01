using System;
using Baiye.EcologyCulture;

static class Program
{
    static int passed;
    static CultureInput Supplies()=>new(){Water=20,SaltWater=20,CO2=5,Fertilizer=20,Organic=20,OxygenRoom=5,ProductRoom=50,SampleRoom=.5,DrainRoom=40,WaterRoom=20};
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Near(double a,double b,double tolerance=1e-8)=>Check(Math.Abs(a-b)<=tolerance,$"{a} != {b}");
    static void Test(string name,Action test){test();passed++;Console.WriteLine("PASS "+name);}
    static void Main()
    {
        Test("organic medium sustains paid growth without carbon dioxide or net oxygen",()=>
        {
            var s=new CultureState{Stage=CultureStage.Grow,LiveKg=10,Policy=CulturePolicy.Food};
            var i=Supplies();i.CO2=i.Fertilizer=0;var d=CultureModel.Step(s,i,.2,true);
            Check(d.Worked&&d.LiveAdded>0&&d.Organic>0,"CO2 still obligatory");Near(d.CO2,0);Near(d.Oxygen,0);Near(d.Inputs,d.Outputs);
        });
        Test("blocked oxygen outlet disables only optional photosynthetic growth",()=>
        {
            var s=new CultureState{Stage=CultureStage.Grow,LiveKg=10,Policy=CulturePolicy.Oxygen};
            var i=Supplies();i.OxygenRoom=0;var d=CultureModel.Step(s,i,.2,true);
            Check(d.Worked&&d.LiveAdded>0,"basic cultivation blocked by oxygen outlet");Near(d.CO2,0);Near(d.Oxygen,0);Near(d.Inputs,d.Outputs);
        });
        Test("growth consumes actual resources and conserves biomass plus oxygen",()=>
        {
            var s=new CultureState{Species=CultureSpecies.Spirulina,Stage=CultureStage.Grow,LiveKg=10,Policy=CulturePolicy.Food};
            var d=CultureModel.Step(s,Supplies(),.2,true);
            Check(d.Worked&&d.LiveAdded>0&&d.Oxygen>0,"no growth transaction");Near(d.Inputs,d.Outputs);
            Near(d.Next.LiveKg,s.LiveKg+d.LiveAdded-d.LiveTaken);Near(s.LiveKg,10);
        });
        Test("unpaid step cannot produce oxygen biomass or advance inoculation",()=>
        {
            var s=new CultureState();var d=CultureModel.Step(s,Supplies(),.2,false);
            Near(d.Inputs,0);Near(d.Outputs,0);Near(d.Next.Seconds,s.Seconds);Check(!d.Worked,"unpaid work");
        });
        Test("automatic harvest leaves protected culture and consumes the actual batch",()=>
        {
            var s=new CultureState{Stage=CultureStage.Grow,Species=CultureSpecies.Spirulina,Policy=CulturePolicy.Oxygen,LiveKg=18};
            var d=CultureModel.Step(s,Supplies(),.2,true);Near(d.Product,3.2);Near(d.Residue,.8);Near(d.LiveTaken,4);Near(d.Next.LiveKg,14);Near(d.Inputs,d.Outputs);
        });
        Test("saltwater nutrient and oxygen remain in the same conserved transaction",()=>
        {
            var s=new CultureState{Species=CultureSpecies.Saline,Stage=CultureStage.Grow,LiveKg=10,Policy=CulturePolicy.Food};
            var d=CultureModel.Step(s,Supplies(),.2,true);Check(d.Salt>0&&d.Water==0&&d.SaltWater>0,"salt deleted");Near(d.Inputs,d.Outputs);
        });
        Test("nutritious phenotype improves edible fraction without creating mass",()=>
        {
            var s=new CultureState{Species=CultureSpecies.Spirulina,Trait=CultureTrait.Nutritious,Stage=CultureStage.Grow,LiveKg=12,Policy=CulturePolicy.Food};
            var d=CultureModel.Step(s,Supplies(),.2,true);Near(d.Product,3.8);Near(d.Residue,.2);Near(d.Inputs,d.Outputs);
        });
        Test("switch does not turn old biomass into new species",()=>
        {
            var s=new CultureState{Species=CultureSpecies.Spirulina,Stage=CultureStage.Grow,LiveKg=10};
            s=CultureModel.RequestSwitch(s,CultureSpecies.Saline,CultureTrait.Base,false);
            var d=CultureModel.Step(s,Supplies(),.2,true);Check(d.ProductSpecies==CultureSpecies.Spirulina,"old stock renamed");Near(d.LiveTaken,10);Near(d.Inputs,d.Outputs);Check(d.Next.Stage==CultureStage.Drain,"recovery failed");
        });
        Test("saved sample uses real protected biomass and is not repeated",()=>
        {
            var s=new CultureState{Species=CultureSpecies.Spirulina,Trait=CultureTrait.Heat,Stage=CultureStage.Grow,LiveKg=10};
            s=CultureModel.RequestSwitch(s,CultureSpecies.Saline,CultureTrait.Base,true);
            var d=CultureModel.Step(s,Supplies(),.2,true);Near(d.SampleMade,.05);Near(d.LiveTaken,.05);Near(d.Inputs,d.Outputs);
            var repeat=CultureModel.RequestSwitch(d.Next,CultureSpecies.Green,CultureTrait.Dim,true);Check(repeat.Stage==CultureStage.Recover&&repeat.TargetSpecies==CultureSpecies.Saline,"repeat reset request");
        });
        Test("sample backlog pauses switch with all old biomass intact",()=>
        {
            var s=CultureModel.RequestSwitch(new(){Stage=CultureStage.Grow,LiveKg=10},CultureSpecies.Saline,CultureTrait.Base,true);
            var i=Supplies();i.SampleRoom=0;var d=CultureModel.Step(s,i,.2,true);Near(d.Inputs,0);Near(d.Outputs,0);Near(d.Next.LiveKg,10);Check(d.Next.Stage==CultureStage.SaveSample,"sample silently lost");
        });
        Test("cleaning needs full payment and recirculates its water without discharge",()=>
        {
            var s=new CultureState{Stage=CultureStage.Clean,Switching=true,TargetSpecies=CultureSpecies.Saline};
            var unpaid=CultureModel.Step(s,Supplies(),.2,false);Near(unpaid.Next.Seconds,0);
            double water=0;for(int x=0;x<150;x++){var d=CultureModel.Step(s,Supplies(),.2,true);Near(d.Inputs,d.Outputs);water+=d.Water;s=d.Next;}
            Near(water,0);Check(s.Stage==CultureStage.Inoculate&&s.Species==CultureSpecies.Saline,"cleaning did not choose new species");
        });
        Test("internal filtering preserves fresh water and separates real salt",()=>
        {
            var s=new CultureState{Stage=CultureStage.Drain,Switching=true};var i=Supplies();i.ProductRoom=0;
            var d=CultureModel.Step(s,i,.2,true);Near(d.Inputs,0);Check(d.Block==CultureBlock.ProductFull,"salt overflow");
            i.ProductRoom=50;d=CultureModel.Step(s,i,.2,true);Near(d.Water,0);Near(d.SaltWater,2);Near(d.ReturnedWater,1.86);Near(d.Salt,.14);Near(d.Inputs,d.Outputs);
        });
        Test("dead culture is recovered as residue and can restart the same base species",()=>
        {
            var s=new CultureState{Stage=CultureStage.Grow,LiveKg=10,Health=0};s=CultureModel.RequestSwitch(s,CultureSpecies.Green,CultureTrait.Base,false);
            var d=CultureModel.Step(s,Supplies(),.2,true);Near(d.Product,0);Near(d.Residue,10);Near(d.Inputs,d.Outputs);
        });
        Test("base inoculation has no free initial biomass",()=>
        {
            var s=new CultureState();double mass=0;
            for(int x=0;x<600;x++){var d=CultureModel.Step(s,Supplies(),.2,true);Near(d.Inputs,d.Outputs);Check(d.LiveAdded>0,"startup free or stalled");mass+=d.LiveAdded;s=d.Next;}
            Near(mass,2);Near(s.LiveKg,2);Check(s.Stage==CultureStage.Grow,"inoculation did not complete");
        });
        Test("imported genotype sample transfers once then feeds normal inoculation",()=>
        {
            var s=new CultureState{Stage=CultureStage.Inoculate,Species=CultureSpecies.Spirulina,TargetSpecies=CultureSpecies.Spirulina,Trait=CultureTrait.Heat,TargetTrait=CultureTrait.Heat};
            var i=Supplies();i.ImportSample=.05;var d=CultureModel.Step(s,i,.2,true);Near(d.SampleTaken,.05);Near(d.LiveAdded,.05);Near(d.Inputs,d.Outputs);
            i.ImportSample=0;var next=CultureModel.Step(d.Next,i,.2,true);Near(next.SampleTaken,0);Check(next.LiveAdded>0,"cannot continue without second sample");Near(next.Inputs,next.Outputs);
        });
        Test("healthy preservation culture holds target without emitting free oxygen",()=>
        {
            var s=new CultureState{Stage=CultureStage.Grow,LiveKg=16,Policy=CulturePolicy.Preserve};var d=CultureModel.Step(s,Supplies(),.2,true);
            Check(d.Worked,"no maintenance");Near(d.Inputs,0);Near(d.Outputs,0);Near(d.Next.LiveKg,16);
        });
        Test("three strategies cannot manufacture a different strain",()=>
        {
            foreach(CulturePolicy p in Enum.GetValues<CulturePolicy>())
            {var s=new CultureState{Species=CultureSpecies.Saline,Trait=CultureTrait.Fast,Stage=CultureStage.Grow,LiveKg=10,Policy=p};var d=CultureModel.Step(s,Supplies(),.2,true);Check(d.Next.Species==s.Species&&d.Next.Trait==s.Trait,"strategy changed genotype");Near(d.Inputs,d.Outputs);}
        });
        Test("input and output interruptions cause no partial resource withdrawal",()=>
        {
            for(int x=0;x<4;x++)
            {var s=new CultureState{Stage=CultureStage.Grow,LiveKg=10,Policy=CulturePolicy.Food};var i=Supplies();switch(x){case 0:i.Water=i.SaltWater=0;break;case 1:i.Organic=0;break;case 2:i.ProductRoom=0;break;case 3:i.TemperatureC=90;break;}var d=CultureModel.Step(s,i,.2,true);Near(d.Inputs,0);Near(d.Outputs,0);Near(d.Next.LiveKg,10);Check(!d.Worked,"interrupted partial production");}
        });
        Test("health observations do not create or delete dead material",()=>
        {
            var s=new CultureState{Stage=CultureStage.Grow,LiveKg=10};var i=Supplies();i.Water=0;
            s=CultureModel.Observe(s,i,2400,false);Near(s.Health,0);Near(s.LiveKg,10);
            var d=CultureModel.Step(s,Supplies(),.2,true);Near(d.Inputs,0);Near(d.Outputs,0);Check(d.Block==CultureBlock.Dead,"dead culture kept producing food");
        });
        Test("saved switch stage and remaining stock survive a state round trip",()=>
        {
            var s=CultureModel.RequestSwitch(new(){Stage=CultureStage.Grow,LiveKg=13,Trait=CultureTrait.Dim},CultureSpecies.Spirulina,CultureTrait.Heat,true);
            var options=new System.Text.Json.JsonSerializerOptions{IncludeFields=true};var restored=System.Text.Json.JsonSerializer.Deserialize<CultureState>(System.Text.Json.JsonSerializer.Serialize(s,options),options);
            var d=CultureModel.Step(restored,Supplies(),.2,true);Near(d.SampleMade,.05);Check(d.SampleTrait==CultureTrait.Dim&&d.Next.TargetTrait==CultureTrait.Heat,"genotype lost");Near(d.Inputs,d.Outputs);
        });
        Test("600 seconds cultivation retains mass through many automatic batches",()=>
        {
            foreach(CultureSpecies species in Enum.GetValues<CultureSpecies>())
            {var s=new CultureState{Stage=CultureStage.Grow,Species=species,LiveKg=14,Policy=CulturePolicy.Oxygen};double taken=0,added=0,food=0,residue=0;
            for(int x=0;x<3000;x++){var d=CultureModel.Step(s,Supplies(),.2,true);Near(d.Inputs,d.Outputs);Check(d.Next.LiveKg>=14-1e-7&&d.Next.LiveKg<=20,"protected stock violated");added+=d.LiveAdded;taken+=d.LiveTaken;food+=d.Product;residue+=d.Residue-d.Fertilizer+d.LiveAdded*CultureModel.NutrientPerKg;s=d.Next;}
            Near(14+added-taken,s.LiveKg,1e-7);Check(added>0,"no cycle growth");}
        });
        Test("product backlog cannot be deleted to make room for more oxygen",()=>
        {
            var s=new CultureState{Stage=CultureStage.Grow,LiveKg=18,Policy=CulturePolicy.Oxygen};var i=Supplies();i.ProductRoom=0;
            var d=CultureModel.Step(s,i,.2,true);Near(d.Inputs,0);Near(d.Outputs,0);Near(d.Next.LiveKg,18);Check(d.Block==CultureBlock.ProductFull,"wrong blockage");
        });
        Test("sample selection spends real mother culture and exposure",()=>
        {
            var s=new CultureState{Stage=CultureStage.Grow,LiveKg=16,HeatExposure=1800};
            var d=CultureModel.SampleCulture(s,CultureTrait.Heat,.5);Check(d.Worked,"eligible selection failed");Near(d.Inputs,d.Outputs);Near(d.Next.LiveKg,15.95);Near(d.Next.HeatExposure,0);Check(d.Next.Trait==CultureTrait.Base,"sampling rewrote mother genotype");
            Check(!CultureModel.SampleCulture(d.Next,CultureTrait.Heat,.5).Worked,"selection exposure duplicated");
        });
        Test("sampling refuses reserve violation unhealthy colony and output backlog",()=>
        {
            var s=new CultureState{Stage=CultureStage.Grow,Policy=CulturePolicy.Food,LiveKg=8.04};Check(!CultureModel.SampleCulture(s,CultureTrait.Base,.5).Worked,"sampling depleted reserve");
            s.LiveKg=10;s.Health=.1;Check(!CultureModel.SampleCulture(s,CultureTrait.Base,.5).Worked,"dead sample accepted");s.Health=1;Check(!CultureModel.SampleCulture(s,CultureTrait.Base,0).Worked,"sample warehouse overflow");
        });
        Test("interrupted inoculation cannot be farmed for fast food",()=>
        {
            var s=CultureModel.RequestSwitch(new(){Stage=CultureStage.Inoculate,Species=CultureSpecies.Spirulina,LiveKg=1},CultureSpecies.Green,CultureTrait.Base,false);
            var d=CultureModel.Step(s,Supplies(),.2,true);Near(d.Product,0);Near(d.Residue,1);Near(d.Inputs,d.Outputs);
        });
        Test("partially funded higher demand never grants an oxygen transaction",()=>
        {
            var s=new CultureState{Stage=CultureStage.Grow,Policy=CulturePolicy.Oxygen,LiveKg=14};var i=Supplies();i.MaxWatts=120;
            var d=CultureModel.Step(s,i,.2,true);Check(d.Block==CultureBlock.Power&&!d.Worked,"old maintenance payment funded high-demand culture");Near(d.Inputs,0);Near(d.Outputs,0);
        });
        Test("stressed harvest separates damaged material without deleting mass",()=>
        {
            var s=new CultureState{Stage=CultureStage.Grow,Species=CultureSpecies.Spirulina,Policy=CulturePolicy.Food,LiveKg=12,Health=.5};
            var d=CultureModel.Step(s,Supplies(),.2,true);Near(d.Product,1.6);Near(d.Residue,2.4);Near(d.Inputs,d.Outputs);Near(d.Next.LiveKg,8);
        });
        Test("unmaintained stored colony cannot heal for free",()=>
        {
            var s=new CultureState{Stage=CultureStage.Grow,LiveKg=10,Health=.5};
            var d=CultureModel.Observe(s,Supplies(),1,false);Check(d.Health<s.Health,"unpaid health recovery");Near(d.LiveKg,10);
        });
        Test("CO2 acceleration uses actual carbon and increases biomass",()=>
        {
            var s=new CultureState{Stage=CultureStage.Grow,LiveKg=10,Policy=CulturePolicy.Oxygen};
            var i=Supplies();i.CO2=0;var basic=CultureModel.Step(s,i,.2,true);
            i.CO2=5;i.Fertilizer=0;var photo=CultureModel.Step(s,i,.2,true);
            Check(photo.LiveAdded>basic.LiveAdded&&photo.CO2>0&&photo.Oxygen>0&&photo.Residue>0,"bonus not real");Near(photo.Inputs,photo.Outputs);Near(photo.CO2,photo.PhotoGain*CultureModel.CarbonPerKg);
        });
        Test("limited CO2 cannot cause a partial or oversized photosynthetic debit",()=>
        {
            var s=new CultureState{Stage=CultureStage.Grow,LiveKg=10,Policy=CulturePolicy.Oxygen};var i=Supplies();i.CO2=.00001;
            var d=CultureModel.Step(s,i,.2,true);Check(d.Worked&&d.CO2<=i.CO2+1e-10&&d.PhotoGain>0,"limited CO2 blocked baseline");Near(d.Inputs,d.Outputs);
        });
        Test("no CO2 cultivation and health remain sustainable through multiple batches",()=>
        {
            var s=new CultureState{Stage=CultureStage.Grow,LiveKg=12,Policy=CulturePolicy.Food};var i=Supplies();i.CO2=i.Fertilizer=0;
            double harvest=0;for(int n=0;n<3000;n++){var d=CultureModel.Step(s,i,.2,true);Near(d.Inputs,d.Outputs);Near(d.Oxygen,0);s=CultureModel.Observe(d.Next,i,.2,true);harvest+=d.Product;}
            Check(harvest>0&&s.Health==1,"CO2 absence harms baseline colony");
        });
        Test("legacy liquid recovery preserves mass and defers when vessel is full",()=>
        {
            var s=new CultureState{Stage=CultureStage.Clean,Seconds=10};var i=Supplies();i.LegacyWater=40;
            var d=CultureModel.Step(s,i,.2,true);Near(d.ReclaimWater,2);Near(d.ReturnedWater,2);Near(d.Inputs,d.Outputs);Near(d.Next.Seconds,10);
            i.WaterRoom=0;d=CultureModel.Step(s,i,.2,true);Near(d.ReclaimWater,0);Check(d.Next.Seconds>10,"old buffer prevents ongoing work");
            i.WaterRoom=20;i.LegacyWater=0;i.LegacySaltWater=40;d=CultureModel.Step(s,i,.2,true);Near(d.ReclaimSaltWater,2);Near(d.ReturnedWater,1.86);Near(d.Salt,.14);Near(d.Inputs,d.Outputs);
        });
        Test("internal washing waits for water and never needs a liquid outlet",()=>
        {
            var s=new CultureState{Stage=CultureStage.Clean};var i=Supplies();i.Water=.59;i.DrainRoom=0;
            var d=CultureModel.Step(s,i,.2,true);Check(!d.Worked&&d.Block==CultureBlock.Water,"waterless cleaning");
            i.Water=.6;d=CultureModel.Step(s,i,.2,true);Check(d.Worked,"external liquid outlet still mandatory");Near(d.Inputs,0);Near(d.Outputs,0);
        });
        Test("optional growth fits scarce water medium and residue capacity atomically",()=>
        {
            var s=new CultureState{Stage=CultureStage.Grow,Species=CultureSpecies.Saline,LiveKg=10,Policy=CulturePolicy.Food,Trait=CultureTrait.Fast};
            for(int n=0;n<4;n++)
            {
                var i=Supplies();i.Fertilizer=0;i.Water=0;
                if(n==0)i.SaltWater=.0003;
                if(n==1)i.Organic=.0009;
                if(n==2)i.ProductRoom=.00006;
                if(n==3)i.CO2=0;
                var d=CultureModel.Step(s,i,.2,true);Near(d.Inputs,d.Outputs);
                Check(d.Organic<=i.Organic+1e-8&&d.SaltWater<=i.SaltWater+1e-8&&d.CO2<=i.CO2+1e-8&&d.Salt+d.Residue<=i.ProductRoom+1e-8,"partial input or outlet overflow");
            }
        });
        Test("fresh water permits saline cultivation after internal strain cleaning",()=>
        {
            var s=new CultureState{Stage=CultureStage.Grow,Species=CultureSpecies.Saline,LiveKg=10};var i=Supplies();i.SaltWater=0;
            var d=CultureModel.Step(s,i,.2,true);Check(d.Worked&&d.LiveAdded>0,"recycled fresh water stalls saline culture");Near(d.Salt,0);Near(d.Inputs,d.Outputs);
        });
        Test("fertilizer alone never manufactures carbon-bearing baseline biomass",()=>
        {
            var s=new CultureState{Stage=CultureStage.Grow,LiveKg=10};var i=Supplies();i.Organic=i.CO2=0;
            var d=CultureModel.Step(s,i,.2,true);Check(!d.Worked&&d.Block==CultureBlock.Nutrient,"mineral fertilizer manufactured organic material");Near(d.Inputs,0);Near(d.Outputs,0);
        });
        Test("switch reaches new species through internal cleaning without discharge",()=>
        {
            var s=CultureModel.RequestSwitch(new(){Stage=CultureStage.Grow,Species=CultureSpecies.Spirulina,LiveKg=10},CultureSpecies.Saline,CultureTrait.Base,true);
            var i=Supplies();i.DrainRoom=0;i.CO2=0;double water=i.Water,saltwater=i.SaltWater,discharged=0;
            for(int n=0;n<900&&s.Stage!=CultureStage.Grow;n++)
            {
                i.Water=water;i.SaltWater=saltwater;var d=CultureModel.Step(s,i,.2,true);Near(d.Inputs,d.Outputs);
                Check(d.Worked,"switch stopped without external liquid outlet");
                water+=d.ReturnedWater-d.Water;saltwater-=d.SaltWater;discharged+=d.DrainWater+d.DrainSaltWater;s=d.Next;
            }
            Check(s.Stage==CultureStage.Grow&&s.Species==CultureSpecies.Saline&&!s.Switching,"switch failed");Near(s.LiveKg,2);Near(discharged,0);
        });
        Test("stored CO2 alone never reports photosynthesis",()=>
        {
            var s=new CultureState{Stage=CultureStage.Grow,LiveKg=10,Policy=CulturePolicy.Food};
            var a=new CultureActivity();var i=Supplies();i.CO2=0;var d=CultureModel.Step(s,i,.2,true);
            a.Committed(s.Stage,d,10);Check(a.Mode(d.Next,10.2,true,false)==CultureMode.Organic,"false photosynthesis signal");
        });
        Test("committed photo mode expires on pause and oxygen blockage remains baseline",()=>
        {
            var s=new CultureState{Stage=CultureStage.Grow,LiveKg=10,Policy=CulturePolicy.Oxygen};
            var a=new CultureActivity();var d=CultureModel.Step(s,Supplies(),.2,true);a.Committed(s.Stage,d,10);
            Check(a.Mode(d.Next,10.2,true,false)==CultureMode.Photosynthetic,"photo signal absent");
            Check(a.Mode(d.Next,10.2,true,true)==CultureMode.OxygenBlocked,"blocked bonus reported active");
            Check(a.Mode(d.Next,11,true,false)==CultureMode.Idle&&!a.Running(10.2,false),"stale or unpowered activity");
        });
        Test("only committed full automatic harvest advances harvest signal",()=>
        {
            var s=new CultureState{Stage=CultureStage.Grow,LiveKg=12,Policy=CulturePolicy.Food};
            var a=new CultureActivity();var d=CultureModel.Step(s,Supplies(),.2,true);
            Check(a.HarvestSequence==0,"preflight altered visual signal");a.Committed(s.Stage,d,10);Check(a.HarvestSequence==1,"harvest absent");
            a.Committed(CultureStage.Grow,new CultureDelta{Worked=true,LiveTaken=.05},11);
            a.Committed(CultureStage.Recover,d,12);a.Committed(CultureStage.Grow,new CultureDelta{LiveTaken=4},13);
            Check(a.HarvestSequence==1,"sampling, recovery or unpaid event counted as automatic harvest");
        });
        Test("native loop choice follows actual stage and preserving threshold",()=>
        {
            var s=new CultureState{Stage=CultureStage.Grow,Policy=CulturePolicy.Preserve,LiveKg=16};var a=new CultureActivity();
            a.Committed(s.Stage,new CultureDelta{Worked=true},10);
            Check(a.Mode(s,10,true,false)==CultureMode.Preserve&&CultureActivity.Loop(s)=="preserving_loop","preserving animation");
            s.Stage=CultureStage.Clean;Check(CultureActivity.Loop(s)=="cleaning_loop","wash animation");
            s.Stage=CultureStage.Inoculate;Check(CultureActivity.Loop(s)=="inoculating_loop","inoculation animation");
            s.Stage=CultureStage.Recover;Check(CultureActivity.Loop(s)=="recovery_loop","recovery animation");
            s.Stage=CultureStage.Grow;s.Policy=CulturePolicy.Food;Check(CultureActivity.Loop(s)=="working_loop","normal culture animation");
        });
        Console.WriteLine($"{passed} culture regression scenarios passed");
    }
}
