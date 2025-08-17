using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace StargatesMod
{
    public class QuestNode_CorrectSiteName : QuestNode
    {
        public SlateRef<Site> site;
        public SlateRef<string> inSignal;

        protected override bool TestRunInt(Slate slate)
        {
            return true;
        }

        /*Sets up a QuestPart_CorrectSiteName with the passed values that will correct the (stargate) site's name when the specified signal is sent*/
        /*Signal sent by a QuestNode_Delay*/
        protected override void RunInt()
        {
            Slate slate = QuestGen.slate;
            
            QuestPart_CorrectSiteName questPartCorrectSiteName = new QuestPart_CorrectSiteName
            {
                inSignal = QuestGenUtility.HardcodedSignalWithQuestID(inSignal.GetValue(slate)) ?? QuestGen.slate.Get<string>("inSignal"),
                site = site.GetValue(slate),
                quest = QuestGen.quest
            };
            QuestGen.quest.AddPart(questPartCorrectSiteName);
            
        }
    }
}