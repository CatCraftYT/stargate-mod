using System;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace StargatesMod
{
    public class CompTargetable_Stargate : CompTargetable
    {
		protected override bool PlayerChoosesTarget { get { return true; } }

		protected override TargetingParameters GetTargetingParameters()
		{
			return new TargetingParameters
			{
				validator = (TargetInfo x) =>
				{
					CompStargate sgComp = x.Thing.TryGetComp<CompStargate>();
					if (x.Thing != null && sgComp != null && sgComp.Props.canHaveIris && !sgComp.hasIris)
                    {
						return true;
                    }
					return false;
				}
			};
		}

		public override IEnumerable<Thing> GetTargets(Thing targetChosenByPlayer = null)
		{
			yield return targetChosenByPlayer;
			yield break;
		}
	}
}
