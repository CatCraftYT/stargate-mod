using RimWorld.Planet;
using RimWorld.QuestGen;

namespace StargatesMod;

public class QuestNode_CorrectSiteName : QuestNode
{
    public SlateRef<Site> site;
    public SlateRef<string> inSignal;

    protected override bool TestRunInt(Slate slate)
    {
        return true;
    }
        
    protected override void RunInt()
    {
        Slate slate = QuestGen.slate;
            
        QuestPart_CorrectSiteName questPartCorrectSiteName = new()
        {
            inSignal = QuestGenUtility.HardcodedSignalWithQuestID(inSignal.GetValue(slate)) ?? QuestGen.slate.Get<string>("inSignal"),
            site = site.GetValue(slate),
            quest = QuestGen.quest
        };
            
        QuestGen.quest.AddPart(questPartCorrectSiteName);
    }
}