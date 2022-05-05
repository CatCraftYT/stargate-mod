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
            //i was setting individual values like the points in the slate which was causing NullReferenceExceptions, it took me so long to debug and i just got lucky ;(
            Slate slate = new Slate();
            QuestScriptDef questDef = DefDatabase<QuestScriptDef>.GetNamed("StargatesMod_StargateSiteScript");
            Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(questDef, slate);
            QuestUtility.SendLetterQuestAvailable(quest);
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
            yield return new Toil { initAction = () =>
            {
                GenerateStargateQuest();
                this.job.GetTarget(glyphScrapItem).Thing.Destroy();
            } };
        }
    }
}
