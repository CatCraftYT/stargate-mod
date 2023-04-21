using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using RimWorld;
using RimWorld.Planet;

namespace StargatesMod
{
    public class WorldObject_PermSGSite : WorldObject, IStargate
    {
        public string siteName;
        public ThingDef gateDef;
        public ThingDef dhdDef;
        bool stargateIsActive;
        int dialledAddress;

        List<Thing> sendBuffer = new List<Thing>();
        List<Thing> recvBuffer = new List<Thing>();

        #region IStargate implementations
        public bool IsActive
        {
            get => this.stargateIsActive;
            set => this.stargateIsActive = value;
        }

        public void AddToRecieveBuffer(Thing thing)
        {
            sendBuffer.Add(thing);
        }

        public void AddToSendBuffer(Thing thing)
        {
            recvBuffer.Add(thing);
        }

        public void OpenStargate(int address, bool isRecieving)
        {
            if (sendBuffer.Any() || recvBuffer.Any()) {
                Log.Warning($"A world stargate ({this.Tile}) was opened, but there are still pawns inside the buffers. This shouldn't have happened. Clearing buffers.");
                sendBuffer.Clear();
                recvBuffer.Clear();
            }

            dialledAddress = address;
            stargateIsActive = true;

            // if we're recieving, then just wait for the gate to be closed
            if (isRecieving) { return; }

            if (SGUtils.GetStargate(address).TryRecieveConnection(this.Tile))
            {
                Log.Error($"Stargate at {address} refused to recieve connection from world stargate {this.Tile}.");
            };
        }

        public void CloseStargate()
        {
            this.stargateIsActive = false;
            List<Thing> things = recvBuffer.Concat(sendBuffer).ToList();
            IEnumerable<Pawn> pawns = things.Where((Thing thing) => thing as Pawn != null).Select((Thing thing) => thing as Pawn);

            // things would be destroyed since there are no pawns to carry them, so destroy them now
            if (pawns.Count() == 0)
            {
                foreach (Thing thing in things) { thing.Destroy(); }
            }
            else
            {
                Caravan caravan = CaravanMaker.MakeCaravan(pawns, Faction.OfPlayer, this.Tile, true);
                foreach (Thing thing in things.Where((Thing thing) => thing as Pawn == null))
                {
                    CaravanInventoryUtility.GiveThing(caravan, thing);
                }
            }
        }

        public bool TryCloseConnection()
        {
            if (!stargateIsActive) { return false; }
            CloseStargate();
            return true;
        }

        public bool TryRecieveConnection(int address)
        {
            if (stargateIsActive) { return false; }
            OpenStargate(address, true);
            return true;
        }
        #endregion

        public override string Label
        {
            get
            {
                if (siteName == null) { return base.Label; }
                else { return siteName; }
            }
        }

        public override string GetInspectString()
        {
            return "GateAddress".Translate(SGUtils.GetStargateDesignation(this.Tile));
        }

        public override void SpawnSetup()
        {
            base.SpawnSetup();
            Find.World.GetComponent<WorldComp_StargateAddresses>().AddAddress(this.Tile);
        }

        public override void Destroy()
        {
            base.Destroy();
            Find.World.GetComponent<WorldComp_StargateAddresses>().RemoveAddress(this.Tile);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
            yield return new Command_Action
            {
                icon = ContentFinder<Texture2D>.Get("UI/Buttons/Rename"),
                action = () => { Find.WindowStack.Add(new Dialog_RenameSGSite(this)); },
                defaultLabel = "RenameGateSite".Translate(),
                defaultDesc = "RenameGateSiteDesc".Translate()
            };
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan)
        {
            if (this.IsActive)
            {
                yield return new FloatMenuOption("CannotDialGateIsActive".Translate(), null);
                yield break;
            }
            WorldComp_StargateAddresses addressComp = Find.World.GetComponent<WorldComp_StargateAddresses>();
            addressComp.CleanupAddresses();
            if (addressComp.addressList.Count < 2)
            {
                yield return new FloatMenuOption("CannotDialNoDestinations".Translate(), null);
                yield break;
            }

            foreach (int i in addressComp.addressList)
            {
                if (i != this.Tile)
                {
                    WorldObject sgObject = Find.WorldObjects.MapParentAt(i);
                    yield return CaravanArrivalActionUtility.GetFloatMenuOptions(() => { return true; }, () => { return new CaravanArrivalAction_PermSGSite(this, i); }, "GoToGate".Translate(SGUtils.GetStargateDesignation(i), sgObject.Label), caravan, this.Tile, this).First();
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref this.siteName, "siteName");
            Scribe_Defs.Look(ref this.dhdDef, "dhdDef");
            Scribe_Defs.Look(ref this.gateDef, "gateDef");
        }
    }
}
