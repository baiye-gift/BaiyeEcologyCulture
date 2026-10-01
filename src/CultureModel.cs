using System;

namespace Baiye.EcologyCulture
{
    public enum CultureSpecies { Green, Spirulina, Saline }
    public enum CulturePolicy { Preserve, Oxygen, Food }
    public enum CultureTrait { Base, Heat, Dim, Fast, Nutritious }
    public enum CultureStage { Inoculate, Grow, SaveSample, Recover, Drain, Clean }
    public enum CultureBlock { None, Power, Water, CarbonDioxide, Nutrient, OxygenFull, ProductFull, SampleFull, DrainFull, Temperature, Dead, ImportSample }

#if GAME_HOST
    [KSerialization.SerializationConfig(KSerialization.MemberSerialization.OptOut)]
#endif
    public sealed class CultureState
    {
        public CultureSpecies Species, TargetSpecies;
        public CultureTrait Trait, TargetTrait;
        public CulturePolicy Policy;
        public CultureStage Stage;
        public bool Switching, KeepSample;
        public bool RecoveryEdible = true;
        public double Health = 1, LiveKg, Seconds, HeatExposure, DimExposure, FastExposure, NutritionExposure;
        public CultureState Copy() => (CultureState)MemberwiseClone();
    }

    public sealed class CultureInput
    {
        public double Water, SaltWater, CO2, Fertilizer, Organic, OxygenRoom, ProductRoom, SampleRoom, DrainRoom, ImportSample;
        public double LegacyWater, LegacySaltWater, WaterRoom;
        public double TemperatureC = 28;
        public double MaxWatts = 960;
    }

    public sealed class CultureDelta
    {
        public CultureState Next;
        public CultureBlock Block;
        public double Water, SaltWater, CO2, Fertilizer, Organic, Oxygen, LiveAdded, LiveTaken, Product, Salt, Residue, SampleMade, SampleTaken, DrainWater, DrainSaltWater;
        public double ReclaimWater, ReclaimSaltWater, ReturnedWater, PhotoGain;
        public CultureSpecies ProductSpecies, SampleSpecies;
        public CultureTrait SampleTrait;
        public bool Worked;
        public double Watts;
        public double Inputs => Water + SaltWater + CO2 + Fertilizer + Organic + LiveTaken + SampleTaken + ReclaimWater + ReclaimSaltWater;
        public double Outputs => Oxygen + LiveAdded + Product + Salt + Residue + SampleMade + DrainWater + DrainSaltWater + ReturnedWater;
    }

    public static class CultureModel
    {
        public const double Capacity = 20, SeedKg = .05, BatchKg = 4;
        public const double InoculationKg = 2, InoculationSeconds = 120;
        public const double CarbonPerKg = .98 * 44 / 30, WaterPerKg = .98 * 18 / 30, NutrientPerKg = .02, OxygenPerKg = .98 * 32 / 30;
        // Game medium abstraction: baseline hydrated biomass incorporates 80%
        // organic medium and 20% water, with no net oxygen. For extra CH2O-like
        // photosynthesis the medium supplies 20% minerals; its other 80% exits
        // as recoverable residue. The old fertilizer buffer can supply minerals.
        public const double OrganicPerKg = .8, BasicWaterPerKg = .2;
        public static double Reserve(CulturePolicy p) => p == CulturePolicy.Food ? 8 : p == CulturePolicy.Oxygen ? 14 : 12;
        public static double Threshold(CulturePolicy p) => Reserve(p) + BatchKg;
        public static double MaxRate(CultureSpecies species) => species == CultureSpecies.Green ? .16 : species == CultureSpecies.Spirulina ? .003 : .004;
        public static double MinTemperature(CultureSpecies species) => species == CultureSpecies.Green ? 10 : species == CultureSpecies.Spirulina ? 20 : 15;
        public static double MaxTemperature(CultureSpecies species, CultureTrait trait) =>
            (species == CultureSpecies.Green ? 40 : species == CultureSpecies.Spirulina ? 38 : 42) + (trait == CultureTrait.Heat ? 12 : 0);
        public static double EdibleFraction(CultureSpecies species,CultureTrait trait) => species==CultureSpecies.Green?1:Math.Min(1,(species==CultureSpecies.Spirulina?.8:.65)+(trait==CultureTrait.Nutritious?.15:0));
        public static double OperatingWatts(CultureState s) => s.Stage != CultureStage.Grow ? 480 : (s.Policy == CulturePolicy.Preserve ? 120 : s.Policy == CulturePolicy.Oxygen ? 960 : 600)*(s.Trait==CultureTrait.Dim?.65:1);

        // These transactions are pure plans. The host commits withdrawals and
        // native inventory changes before accepting Next; readiness checks never
        // advance timers or manufacture a second copy of a product.
        public static CultureDelta Step(CultureState state, CultureInput input, double seconds, bool funded)
        {
            var d = new CultureDelta { Next = state.Copy(), ProductSpecies = state.Species, SampleSpecies = state.Species, SampleTrait = state.Trait, Watts = OperatingWatts(state) };
            if (!funded || seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) { d.Block = CultureBlock.Power; return d; }
            if (input.MaxWatts + 1e-6 < d.Watts) { d.Block = CultureBlock.Power; return d; }
            // Recover old-version liquids into the input vessel, in paid bounded
            // batches. A full vessel defers recovery without deleting old stock
            // or preventing the current culture from using the input water.
            if (input.WaterRoom > 1e-7 && input.LegacyWater > 1e-7)
            {
                d.ReclaimWater = d.ReturnedWater = Math.Min(10*seconds,Math.Min(input.WaterRoom,input.LegacyWater));
                d.Worked = true; return d;
            }
            if (input.WaterRoom > 1e-7 && input.LegacySaltWater > 1e-7 && input.ProductRoom > 1e-7)
            {
                d.ReclaimSaltWater = Math.Min(10*seconds,Math.Min(input.LegacySaltWater,Math.Min(input.WaterRoom/.93,input.ProductRoom/.07)));
                d.ReturnedWater = d.ReclaimSaltWater*.93; d.Salt=d.ReclaimSaltWater*.07;
                d.Worked = true; return d;
            }
            if (state.Stage == CultureStage.SaveSample)
            {
                if (state.LiveKg < SeedKg || state.Health <= .1)
                { d.Next.Stage = CultureStage.Recover; d.Worked = true; return d; }
                if (input.SampleRoom < SeedKg) return Block(d, CultureBlock.SampleFull);
                d.LiveTaken = d.SampleMade = SeedKg;
                d.Next.LiveKg -= SeedKg; d.Next.Stage = CultureStage.Recover; d.Worked = true; return d;
            }
            if (state.Stage == CultureStage.Recover)
            {
                double mass = Math.Min(state.LiveKg, Math.Max(0, input.ProductRoom));
                if (state.LiveKg > 1e-7 && mass <= 1e-7) return Block(d, CultureBlock.ProductFull);
                d.LiveTaken = mass;
                if (state.Health <= .1 || !state.RecoveryEdible) d.Residue = mass; else { d.Product = mass*EdibleFraction(state.Species,state.Trait)*state.Health; d.Residue=mass-d.Product; }
                d.Next.LiveKg -= mass;
                if (d.Next.LiveKg <= 1e-7) { d.Next.LiveKg = 0; d.Next.Stage = CultureStage.Drain; }
                d.Worked = true; return d;
            }
            if (state.Stage == CultureStage.Drain)
            {
                // Keep liquid inside the vessel. Numeric stage IDs stay stable
                // for old saves; Drain now means internal filtering.
                if (input.SaltWater > 1e-7)
                {
                    if (input.ProductRoom <= 1e-7) return Block(d,CultureBlock.ProductFull);
                    d.SaltWater=Math.Min(10*seconds,Math.Min(input.SaltWater,input.ProductRoom/.07));
                    d.ReturnedWater=d.SaltWater*.93;d.Salt=d.SaltWater*.07;
                    d.Worked=true;return d;
                }
                d.Next.Stage = CultureStage.Clean; d.Next.Seconds = 0; d.Worked = true; return d;
            }
            if (state.Stage == CultureStage.Clean)
            {
                double time = Math.Min(seconds, Math.Max(0,30-state.Seconds));
                if (input.Water + 1e-8 < .6) return Block(d,CultureBlock.Water);
                d.Next.Seconds += time; d.Worked = true; // 0.6 kg recirculates; no discharge.
                if (d.Next.Seconds >= 30-1e-7)
                {
                    d.Next.Species = state.TargetSpecies; d.Next.Trait = state.TargetTrait;
                    d.Next.Stage = CultureStage.Inoculate; d.Next.Seconds = 0; d.Next.Health = 1;
                    d.Next.HeatExposure = d.Next.DimExposure = d.Next.FastExposure = d.Next.NutritionExposure = 0;
                }
                return d;
            }
            if (state.Stage == CultureStage.Inoculate && state.LiveKg < SeedKg-1e-7 && (state.TargetTrait != CultureTrait.Base || input.ImportSample >= SeedKg))
            {
                if (input.ImportSample + 1e-8 < SeedKg) return Block(d,CultureBlock.ImportSample);
                d.SampleTaken = d.LiveAdded = SeedKg;
                d.Next.LiveKg += SeedKg;
                d.Next.Species = state.TargetSpecies; d.Next.Trait = state.TargetTrait;
                d.Next.Health = 1; d.Worked = true; return d;
            }
            if (state.Health <= .1) return Block(d,CultureBlock.Dead);
            if (input.TemperatureC < MinTemperature(state.Species) || input.TemperatureC > MaxTemperature(state.Species,state.Trait))
                return Block(d,CultureBlock.Temperature);
            if (state.Stage == CultureStage.Grow && state.Policy != CulturePolicy.Preserve && state.LiveKg + 1e-7 >= Threshold(state.Policy))
            {
                if (input.ProductRoom + 1e-7 < BatchKg) return Block(d,CultureBlock.ProductFull);
                d.LiveTaken = Math.Min(BatchKg, state.LiveKg-Reserve(state.Policy));
                d.Product = d.LiveTaken*EdibleFraction(state.Species,state.Trait)*state.Health;d.Residue=d.LiveTaken-d.Product;
                d.Next.LiveKg -= d.LiveTaken; d.Worked = true; return d;
            }
            if (state.Stage == CultureStage.Grow && state.Policy == CulturePolicy.Preserve && state.LiveKg >= 16)
            { d.Worked = true; return d; } // Paid maintenance; no free net oxygen.
            if (state.LiveKg >= Capacity-1e-7 || input.ProductRoom <= 1e-7) return Block(d,CultureBlock.ProductFull);
            double gain;
            if (state.Stage == CultureStage.Inoculate)
                gain = Math.Min(InoculationKg-state.LiveKg, InoculationKg/InoculationSeconds*seconds);
            else
            {
                double density = Math.Min(1, Math.Max(.001,state.LiveKg)/12);
                double crowding = state.LiveKg > 16 ? Math.Max(.15,(Capacity-state.LiveKg)/4) : 1;
                double mode = state.Policy == CulturePolicy.Preserve ? .35 : state.Policy == CulturePolicy.Oxygen ? 1 : .9;
                double trait = state.Trait == CultureTrait.Fast ? 1.25 : state.Trait == CultureTrait.Dim ? .65 : state.Trait == CultureTrait.Heat ? .85 : state.Trait == CultureTrait.Nutritious ? .95 : 1;
                gain = MaxRate(state.Species)*density*crowding*mode*trait*state.Health*seconds;
            }
            gain = Math.Max(0,Math.Min(gain,Capacity-state.LiveKg));
            if (gain <= 1e-10) return Block(d,CultureBlock.ProductFull);
            if(input.Organic+1e-8 < gain*OrganicPerKg) return Block(d,CultureBlock.Nutrient);
            if(input.Water+input.SaltWater*.93+1e-8 < gain*BasicWaterPerKg) return Block(d,CultureBlock.Water);
            // Bonus is bounded by every real reagent and outlet. Absence of CO2,
            // fertilizer or oxygen space never cancels the baseline transaction.
            double factor=state.Policy==CulturePolicy.Oxygen?1:state.Policy==CulturePolicy.Food?.5:.25;
            double photo=state.Stage==CultureStage.Grow?Math.Min(gain*factor,Capacity-state.LiveKg-gain):0;
            photo=Math.Max(0,Math.Min(photo,Math.Min(input.CO2/CarbonPerKg,input.OxygenRoom/OxygenPerKg)));
            photo=Math.Min(photo,Math.Max(0,input.Water+input.SaltWater*.93-gain*BasicWaterPerKg)/WaterPerKg);
            double extraNutrient=state.Trait==CultureTrait.Fast||state.Trait==CultureTrait.Nutritious?1.25:1;
            double mineralPerKg=NutrientPerKg*extraNutrient;
            photo=Math.Min(photo,Math.Max(0,input.Fertilizer+(input.Organic-gain*OrganicPerKg)*.2)/mineralPerKg);
            // Find the maximal feasible optional bonus, including salt residue.
            double lo=0,hi=photo;
            GrowthLedger(d,state,input,gain,photo,mineralPerKg);
            if(d.Salt+d.Residue<=input.ProductRoom+1e-10)lo=photo;
            else for(int n=0;n<32;n++)
            {
                double trial=(lo+hi)/2;
                GrowthLedger(d,state,input,gain,trial,mineralPerKg);
                if(d.Salt+d.Residue<=input.ProductRoom+1e-10)lo=trial;else hi=trial;
            }
            GrowthLedger(d,state,input,gain,lo,mineralPerKg);
            if(d.Salt+d.Residue>input.ProductRoom+1e-8)return Block(new CultureDelta{Next=state.Copy(),Watts=d.Watts},CultureBlock.ProductFull);
            d.LiveAdded=gain+lo;d.PhotoGain=lo;d.Next.LiveKg+=d.LiveAdded;d.Worked=true;
            if(state.Stage==CultureStage.Inoculate && d.Next.LiveKg>=InoculationKg-1e-7)
            {d.Next.Stage=CultureStage.Grow;d.Next.Switching=false;d.Next.Seconds=0;}
            if(input.TemperatureC>=34)d.Next.HeatExposure+=seconds;
            if(state.Policy==CulturePolicy.Preserve)d.Next.DimExposure+=seconds;
            if(state.Policy==CulturePolicy.Food)d.Next.FastExposure+=seconds;
            if(input.Organic>=10)d.Next.NutritionExposure+=seconds;
            return d;
        }

        private static void GrowthLedger(CultureDelta d,CultureState state,CultureInput input,double basic,double photo,double minerals)
        {
            double required=basic*BasicWaterPerKg+photo*WaterPerKg;
            d.Water=d.SaltWater=0;
            if(state.Species==CultureSpecies.Saline){d.SaltWater=Math.Min(input.SaltWater,required/.93);d.Water=Math.Max(0,required-d.SaltWater*.93);}
            else {d.Water=Math.Min(input.Water,required);d.SaltWater=Math.Max(0,required-d.Water)/.93;}
            d.Salt=d.SaltWater*.07;
            d.Fertilizer=Math.Min(input.Fertilizer,photo*minerals);
            double medium=Math.Max(0,photo*minerals-d.Fertilizer)/.2;
            d.Organic=basic*OrganicPerKg+medium;
            d.Residue=medium*.8+photo*(minerals-NutrientPerKg);
            d.CO2=photo*CarbonPerKg;d.Oxygen=photo*OxygenPerKg;
        }

        private static CultureDelta Block(CultureDelta delta,CultureBlock block){delta.Block=block;return delta;}
        public static CultureState RequestSwitch(CultureState state, CultureSpecies species, CultureTrait trait, bool keep)
        {
            var next=state.Copy();
            if(state.Switching || (state.Species==species && state.Trait==trait && state.Stage==CultureStage.Grow && state.Health>.1))return next;
            next.TargetSpecies=species;next.TargetTrait=trait;next.Switching=true;next.KeepSample=keep;
            next.RecoveryEdible=state.Stage==CultureStage.Grow&&state.Health>.1;
            next.Stage=keep?CultureStage.SaveSample:CultureStage.Recover;next.Seconds=0;return next;
        }

        public static bool CanSample(CultureState state,CultureTrait trait)
        {
            if(state.Stage!=CultureStage.Grow || state.Health<.5 || state.LiveKg<Reserve(state.Policy)+SeedKg)return false;
            if(trait==state.Trait || trait==CultureTrait.Base)return true;
            double exposure=trait==CultureTrait.Heat?state.HeatExposure:trait==CultureTrait.Dim?state.DimExposure:trait==CultureTrait.Fast?state.FastExposure:state.NutritionExposure;
            return exposure>=1800;
        }
        public static CultureDelta SampleCulture(CultureState state,CultureTrait trait,double room)
        {
            var d=new CultureDelta{Next=state.Copy(),SampleSpecies=state.Species,SampleTrait=trait};
            if(!CanSample(state,trait) || room+1e-8<SeedKg)return Block(d,CultureBlock.SampleFull);
            d.SampleMade=d.LiveTaken=SeedKg;d.Next.LiveKg-=SeedKg;d.Worked=true;
            if(trait!=state.Trait && trait!=CultureTrait.Base)
            {
                if(trait==CultureTrait.Heat)d.Next.HeatExposure=0;
                else if(trait==CultureTrait.Dim)d.Next.DimExposure=0;
                else if(trait==CultureTrait.Fast)d.Next.FastExposure=0;
                else d.Next.NutritionExposure=0;
            }
            return d;
        }

        // Biological stress is observed once per simulation step, not once per
        // readiness probe or native power callback. It changes health only;
        // dead mass remains in the sealed vessel until paid recovery.
        public static CultureState Observe(CultureState state, CultureInput input, double seconds, bool operational)
        {
            var next=state.Copy();if(seconds<=0||state.Stage!=CultureStage.Grow)return next;
            bool water=input.Water+input.SaltWater*.93>.001;
            bool healthy=operational&&water&&input.Organic>.001&&input.TemperatureC>=MinTemperature(state.Species)&&input.TemperatureC<=MaxTemperature(state.Species,state.Trait);
            next.Health=Math.Max(0,Math.Min(1,state.Health+(healthy?seconds/600:-seconds/2400)));
            return next;
        }
    }
}
