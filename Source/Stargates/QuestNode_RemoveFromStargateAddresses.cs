using RimWorld.QuestGen;
using Verse;

namespace StargatesMod
{
    public class QuestNode_AddStargateAddresses : QuestNode
    {
        public SlateRef<int> address;
        public SlateRef<bool> remove;

        protected override bool TestRunInt(Slate slate)
        {
            return true;
        }
        protected override void RunInt()
        {
            Slate slate = QuestGen.slate;
            int tile = slate.Get<int>("tile");

            WorldComp_StargateAddresses sgWorldComp = Find.World.GetComponent<WorldComp_StargateAddresses>();
            if (sgWorldComp != null)
            {
                if (remove.GetValue(slate)) { sgWorldComp.addressList.Remove(tile); }
                else { sgWorldComp.addressList.Add(tile); }
            }
            else { Log.Error("QuestNode_AddStargateAddresses tried to get WorldComp_StargateAddresses but it was null."); }
        }
    }
}
