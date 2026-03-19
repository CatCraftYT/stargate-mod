using RimWorld;
using RimWorld.Planet;

namespace StargatesMod;

public class QuestPart_CorrectSiteName : QuestPart
{
    public string inSignal;
    public Site site;
    public new Quest quest;
        
    public override void Notify_QuestSignalReceived(Signal signal)
    {
        base.Notify_QuestSignalReceived(signal);
        
        if (signal.tag == inSignal)
            site.customLabel = quest.name;
    }
}