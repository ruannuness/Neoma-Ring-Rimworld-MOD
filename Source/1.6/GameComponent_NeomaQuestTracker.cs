using RimWorld;
using RimWorld.Planet;
using Verse;

namespace SyntheraCore
{
    // Auto-discovered by RimWorld reflection — no GameDef XML required.
    public class WorldComponent_NeomaQuestTracker : WorldComponent
    {
        public static WorldComponent_NeomaQuestTracker Instance { get; private set; }

        private bool tier3Done;
        private bool questFired;
        private int nextCheckTick;

        // 72 in-game hours = 3 days = 180 000 ticks
        private const int MinTicks = 180_000;

        public WorldComponent_NeomaQuestTracker(World world) : base(world) => Instance = this;

        public void OnTierIIIResearched()
        {
            if (questFired) return;
            tier3Done = true;
            TryFireQuest();
        }

        public override void WorldComponentTick()
        {
            if (questFired || !tier3Done) return;
            int tick = Find.TickManager.TicksGame;
            if (tick < nextCheckTick) return;
            nextCheckTick = tick + 500;
            TryFireQuest();
        }

        private void TryFireQuest()
        {
            if (questFired) return;
            if (Find.TickManager.TicksGame < MinTicks) return;

            var questDef = DefDatabase<QuestScriptDef>.GetNamed("NeomaOrbitalStation", errorOnFail: false);
            if (questDef == null) return; // Odyssey not installed

            IIncidentTarget target = Find.CurrentMap ?? (IIncidentTarget)Find.World;
            float points = StorytellerUtility.DefaultThreatPointsNow(target);
            QuestUtility.GenerateQuestAndMakeAvailable(questDef, points);

            Find.LetterStack.ReceiveLetter(
                "NeomaOrbitalStation_LetterLabel".Translate(),
                "NeomaOrbitalStation_LetterText".Translate(),
                LetterDefOf.PositiveEvent);

            questFired = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref tier3Done,  "tier3Done");
            Scribe_Values.Look(ref questFired, "questFired");
        }
    }
}
