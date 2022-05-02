using System;
using RimWorld.Planet;
using RimWorld;
using System.Text;

namespace StargatesMod
{
	public class SitePartWorker_Stargate : SitePartWorker
	{
		public override string GetPostProcessedThreatLabel(Site site, SitePart sitePart)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append($"Gate address: {CompStargate.GetStargateDesignation(site.Tile)}");
			return sb.ToString();
		}
	}
}
