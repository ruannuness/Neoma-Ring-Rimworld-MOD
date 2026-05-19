using RimWorld;
using Verse;

namespace SyntheraCore
{
    // Stores a Synthera pawn inside a physical item so it can be moved between altars.
    // The pawn is serialised deep into the comp; destroying the item by any means other
    // than Deconstruct kills the consciousness permanently.
    public class CompConsciousnessCore : ThingComp
    {
        public Pawn StoredConsciousness;

        public CompProperties_ConsciousnessCore Props => (CompProperties_ConsciousnessCore)props;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref StoredConsciousness, "StoredConsciousness");
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            if (mode != DestroyMode.Deconstruct && StoredConsciousness != null && !StoredConsciousness.Destroyed)
            {
                StoredConsciousness.Destroy(DestroyMode.KillFinalize);
                Messages.Message(
                    "A Synthera memory core was destroyed. The stored consciousness has been lost.",
                    MessageTypeDefOf.NegativeEvent, false);
            }
        }

        public override string CompInspectStringExtra()
        {
            if (StoredConsciousness == null) return "Memory core: empty";
            return $"Memory core: {StoredConsciousness.Name.ToStringShort} stored";
        }
    }
}
