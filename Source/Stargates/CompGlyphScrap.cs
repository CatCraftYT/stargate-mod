using System;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.Sound;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StargatesMod
{
    public class CompGlyphScrap : ThingComp
    {
        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            if (!selPawn.CanReach(this.parent, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn))
            {
                yield break;
            }

            yield return new FloatMenuOption("Decode symbols", () =>
            {
                Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("StargateMod_EnterStargate"), this.parent);
                selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            });
        }
    }

    public class CompProperties_GlyphScrap : CompProperties
    {
        public CompProperties_GlyphScrap()
        {
            this.compClass = typeof(CompGlyphScrap);
        }
    }
}
