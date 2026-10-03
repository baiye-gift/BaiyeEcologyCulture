namespace Baiye.EcologyCulture
{
    // Output requests survive blocked outlets and save/load. Tiny salt/residue
    // transactions stay sealed until a real batch or a completed stage exists.
    public static class CultureOutputPolicy
    {
        public const double ByproductBatchKg=5;
        public static bool ProductsDue(double kg,bool requested)=>kg>1e-7&&(requested||kg+1e-7>=ByproductBatchKg);
        public static bool SamplesDue(double kg)=>kg>1e-7;
        public static bool FlushAfter(CultureStage before,CultureDelta d)=>d.Worked&&!d.Maintaining&&
            (before==CultureStage.Grow&&d.LiveTaken>=CultureModel.BatchKg-1e-7||
             before==CultureStage.Recover&&d.Next.Stage!=CultureStage.Recover||
             before==CultureStage.Drain&&d.Next.Stage!=CultureStage.Drain||
             before==CultureStage.Clean&&d.Next.Stage!=CultureStage.Clean);
    }
}
