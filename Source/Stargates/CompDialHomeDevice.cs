using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace StargatesMod;

public class CompDialHomeDevice : ThingComp
{
    private CompFacility _compFacility;
    public PlanetTile QueuedAddress = -1;
    public DialMode DialMode;

    public CompProperties_DialHomeDevice Props => (CompProperties_DialHomeDevice)props;

    public CompStargate GetLinkedStargateComp()
    {
        if (Props.selfDialler) return parent.TryGetComp<CompStargate>();
        
        return _compFacility.LinkedBuildings.Count switch
        {
            1 when !_compFacility.LinkedBuildings[0].TryGetComp<CompStargate>().IsHibernating => _compFacility.LinkedBuildings[0].TryGetComp<CompStargate>(),
            > 1 => _compFacility.LinkedBuildings.Where(t => !t.TryGetComp<CompStargate>().IsHibernating).FirstOrFallback().TryGetComp<CompStargate>(),
            _ => null
        };
    }

    public bool IsConnectedToStargate
    {
        get
        {
            if (Props.selfDialler) return true;
            return _compFacility.LinkedBuildings.Count != 0;
        }
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        _compFacility = parent.GetComp<CompFacility>();
    }
        
    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (Gizmo gizmo in base.CompGetGizmosExtra()) 
            yield return gizmo;

        CompStargate compStargate = GetLinkedStargateComp();
            
        if (compStargate == null) yield break;
            
        Command_Action commandCloseGate = new()
        {
            defaultLabel = "SGM.CloseStargate".Translate(),
            defaultDesc = "SGM.CloseStargateDesc".Translate(),
            icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel"),
            action = delegate
            {
                compStargate.CloseStargate(true);
            }
        };
        if (!compStargate.StargateIsActive) commandCloseGate.Disable("SGM.GateIsNotActive".Translate());
        else if (compStargate.IsReceivingGate) commandCloseGate.Disable("SGM.CannotCloseIncoming".Translate());
        yield return commandCloseGate;
    }
}
public class CompProperties_DialHomeDevice : CompProperties
{
    public CompProperties_DialHomeDevice()
    {
        compClass = typeof(CompDialHomeDevice);
    }
    public bool selfDialler = false;
    public bool requiresPower = false;
}