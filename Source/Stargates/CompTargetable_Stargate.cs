using RimWorld;
using Verse;
using System.Collections.Generic;

namespace StargatesMod
{
    public class CompTargetable_Stargate : CompTargetable
    {
		protected override bool PlayerChoosesTarget => true;

		protected override TargetingParameters GetTargetingParameters()
		{
			return new TargetingParameters
			{
				validator = x =>
				{
					CompStargate sgComp = x.Thing.TryGetComp<CompStargate>();
					return x.Thing != null && sgComp != null && sgComp.Props.canHaveIris && !sgComp.HasIris;
				}
			};
		}

		public override IEnumerable<Thing> GetTargets(Thing targetChosenByPlayer = null)
		{
			yield return targetChosenByPlayer;
		}
	}
}
