using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld;

namespace StargatesMod
{
    public class Building_Stargate : Building
    {
        public override IEnumerable<Gizmo> GetGizmos()
        {
            CompStargate sgComp = this.GetComp<CompStargate>();
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                if (gizmo is Command_LoadToTransporter)
                {
                    if (sgComp.stargateIsActive) { yield return gizmo; }
                    continue;
                }
                yield return gizmo;
            }
        }

        public override string GetInspectString()
        {
            StringBuilder sb = new StringBuilder();

            CompStargate sgComp = this.GetComp<CompStargate>();
            sb.AppendLine(sgComp.GetInspectString());

            CompPowerTrader power = this.TryGetComp<CompPowerTrader>();
            if (power != null) { sb.AppendLine(power.CompInspectStringExtra()); }
            
            return sb.ToString().TrimEndNewlines();
        }
    }
}
