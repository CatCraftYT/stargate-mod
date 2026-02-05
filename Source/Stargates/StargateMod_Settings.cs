using System.Collections.Generic;
using Verse;

namespace StargatesMod;

public class StargatesModSettings : ModSettings
{
    /*Mod Settings*/
    public bool ShortenGateDialSeq = false;
    public bool DebugMode = false;

    /*Write Settings to file*/
    public override void ExposeData()
    {
        Scribe_Values.Look(ref ShortenGateDialSeq, "ShortenGateDialSeq");
        Scribe_Values.Look(ref DebugMode, "DebugMode");
        base.ExposeData();
    }
}