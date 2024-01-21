using System;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Multiplayer.API;
using Verse.Sound;


namespace StargatesMod
{
    public class JobDriver_DialStargate : JobDriver
    {
        private const TargetIndex targetDHD = TargetIndex.A;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return this.pawn.Reserve(this.job.GetTarget(targetDHD), this.job);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            CompDialHomeDevice dhdComp = this.job.GetTarget(targetDHD).Thing.TryGetComp<CompDialHomeDevice>();
            this.FailOnDestroyedOrNull(targetDHD);
            this.FailOn(() => dhdComp.GetLinkedStargate().stargateIsActive);


      

            yield return Toils_Goto.GotoCell(job.GetTarget(targetDHD).Thing.InteractionCell, PathEndMode.OnCell);
            yield return new Toil
            {
                initAction = () =>
                {
    
              
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
                    Log.Message("TimeSpeed set to Normal");
                    CompStargate linkedStargate = dhdComp.GetLinkedStargate();

                    //re-added the dialing sequence because its cool AF and why not?
                    //lets just make sure the pawn doesnt walk into the vortex on accident...

                    StunPawnForDuration(pawn, 720);
            
               
                    SoundDef diallingSequence = SGSoundDefOf.StargateMod_SGDial;
                    SoundInfo soundInfo = SoundInfo.InMap(new TargetInfo(pawn.Position, pawn.Map));
                    diallingSequence.PlayOneShot(soundInfo);

                    linkedStargate.OpenStargateDelayed(dhdComp.lastDialledAddress, 660);


                }
            };
            yield break;
        }


        public void StunPawnForDuration(Pawn pawn, int durationTicks)
        {
          
                pawn.stances.stunner.StunFor(durationTicks, null);
            
        }

   

}
}
