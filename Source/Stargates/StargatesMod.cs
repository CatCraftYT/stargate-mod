using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace StargatesMod;

public class StargatesMod : Mod
{
    /*Reference to settings*/
    private StargatesModSettings _modSettings;
        
    /*Resolve settings reference*/
    public StargatesMod(ModContentPack content) : base(content)
    {
        _modSettings = GetSettings<StargatesModSettings>();
    }


    /*Settings GUI*/
    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listingStandard = new();
        listingStandard.Begin(inRect);
            
        /*Section 1*/
        Listing_Standard sec1 = listingStandard.BeginSection(150);
        sec1.Label("SGM.SettingsCat.Options".Translate());
        sec1.GapLine();
        sec1.CheckboxLabeled("SGM.ShortenDialSeq.Label".Translate(), ref _modSettings.ShortenGateDialSeq, "SGM.ShortenDialSeq.TT".Translate());
        /*sec1.Label("Setting2");
        sec1.Label("Setting3");
        sec1.Label("Setting4");*/
        listingStandard.EndSection(sec1);

        listingStandard.Gap();
            
        /*Section 2*/
        Listing_Standard sec2 = listingStandard.BeginSection(75);
        sec2.Label("SGM.SettingsCat.Debug".Translate());
        sec2.GapLine();
        sec2.CheckboxLabeled("SGM.DebugMode.Label".Translate(), ref _modSettings.DebugMode, "SGM.DebugMode.TT".Translate());
        listingStandard.EndSection(sec2);
            
        listingStandard.Gap();
            
        /*Toggleable patches notice*/
        Listing_Standard secTp = listingStandard.BeginSection(75);
        secTp.Label("SGM.ToggleablePatches.Header".Translate());
        secTp.GapLine();
        secTp.Label("SGM.ToggleablePatches.Text".Translate());
        listingStandard.EndSection(secTp);
            
        listingStandard.End();
        base.DoSettingsWindowContents(inRect);
    }

    /*Settings mod label*/
    public override string SettingsCategory()
    {
        return "StargatesMod".Translate();
    }
}