using System.Collections.Generic;
using System.Text;
using Verse;
using RimWorld;

namespace StargatesMod
{
    public class Building_Stargate : Building
    {
        public override IEnumerable<Gizmo> GetGizmos()
        {
            CompStargate sgComp = GetComp<CompStargate>();
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                if (gizmo is Command_LoadToTransporter)
                {
                    if (sgComp.StargateIsActive) yield return gizmo;
                    continue;
                }
                yield return gizmo;
            }
        }

        public override string GetInspectString()
        {
            StringBuilder sb = new StringBuilder();

            CompStargate sgComp = GetComp<CompStargate>();
            sb.AppendLine(sgComp.GetInspectString());

            CompPowerTrader power = this.TryGetComp<CompPowerTrader>();
            if (power != null) sb.AppendLine(power.CompInspectStringExtra());
            
            return sb.ToString().TrimEndNewlines();
        }
    }
}
