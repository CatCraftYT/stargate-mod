using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace StargatesMod.Mod_Settings
{
    public class StargatesMod_Mod : Mod
    {
        /*Reference to settings*/
        StargatesMod_Settings _settings;
        
        /*Resolve settings reference*/
        public StargatesMod_Mod(ModContentPack content) : base(content)
        {
            _settings = GetSettings<StargatesMod_Settings>();
        }


        /*Settings GUI*/
        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            
            /*Section 1*/
            Listing_Standard sec1 = listingStandard.BeginSection(150);
            sec1.Label("SGM.SettingsCat.Options".Translate());
            sec1.GapLine();
            sec1.CheckboxLabeled("SGM.ShortenDialSeq.Label".Translate(), ref _settings.ShortenGateDialSeq, "SGM.ShortenDialSeq.TT".Translate());
            /*sec1.Label("Setting2");
            sec1.Label("Setting3");
            sec1.Label("Setting4");*/
            listingStandard.EndSection(sec1);

            listingStandard.Gap();
            
            /*Section 2*/
            Listing_Standard sec2 = listingStandard.BeginSection(75);
            sec2.Label("SGM.SettingsCat.Debug".Translate());
            sec2.GapLine();
            sec2.CheckboxLabeled("SGM.DebugMode.Label".Translate(), ref _settings.DebugMode, "SGM.DebugMode.TT".Translate());
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
}