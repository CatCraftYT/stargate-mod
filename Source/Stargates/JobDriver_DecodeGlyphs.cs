using System;
using RimWorld;
using RimWorld.QuestGen;
using Verse;
using Verse.AI;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace StargatesMod
{
    public class JobDriver_DecodeGlyphs : JobDriver
    {
        private const TargetIndex glyphScrapItem = TargetIndex.A;
        private const int useDuration = 500;

        private void GenerateStargateQuest()
        {
            Slate slate = new Slate();
            float threatPoints = StorytellerUtility.DefaultThreatPointsNow(pawn.Map);
            slate.Set("points", threatPoints, false);
            slate.Set("population", PawnsFinder.AllMaps_FreeColonists.Count, false);
            slate.Set("colonistsSingularOrPlural", 1, false);
            slate.Set("passengersSingularOrPlural", 1, false);
            slate.Set("enemyFaction", Find.FactionManager.RandomEnemyFaction(true), false);

            QuestScriptDef questDef = DefDatabase<QuestScriptDef>.GetNamed("StargatesMod_StargateSiteScript");
            Quest quest = QuestGen.Generate(questDef, slate);
            Find.SignalManager.RegisterReceiver(quest);
            quest.Initiate();
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return this.pawn.Reserve(this.job.GetTarget(glyphScrapItem), this.job);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(glyphScrapItem, PathEndMode.Touch);

            Toil toil = Toils_General.Wait(useDuration);
            toil.WithProgressBarToilDelay(glyphScrapItem);
            yield return toil;
            yield return new Toil { initAction = () => { GenerateStargateQuest(); } };
        }
    }
}
