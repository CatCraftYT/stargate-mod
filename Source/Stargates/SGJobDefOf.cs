using RimWorld;
using Verse;

namespace StargatesMod;

[DefOf]
public static class SGJobDefOf
{
    static SGJobDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(JobDefOf));
    }
    
    public static JobDef StargatesMod_WatchStargate;

    public static JobDef StargatesMod_EnterStargate;

    public static JobDef StargatesMod_DialStargate;

    public static JobDef StargatesMod_BringToStargate;

    public static JobDef StargatesMod_InstallIris;

    public static JobDef StargatesMod_DecodeGlyphs;
}