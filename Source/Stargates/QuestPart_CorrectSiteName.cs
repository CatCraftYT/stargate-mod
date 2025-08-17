using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace StargatesMod
{
    public class QuestPart_CorrectSiteName : QuestPart
    {
        public string inSignal;
        public Site site;
        public new Quest quest;

        /*When receiving the correct signal, set the site's custom label to the quest name*/
        /*AKA match the site's name to the associated quest's name*/
        /*Values passed by the associated QuestNode*/
        public override void Notify_QuestSignalReceived(Signal signal)
        {
            base.Notify_QuestSignalReceived(signal);
            if (signal.tag == inSignal)
            {
                site.customLabel = quest.name;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref inSignal, "inSignal");
            Scribe_Values.Look(ref site, "site");
            Scribe_Values.Look(ref quest, "quest");
        }
    }
}