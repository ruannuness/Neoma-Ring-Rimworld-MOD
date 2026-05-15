using RimWorld;
using System.Linq;
using Verse;

namespace SyntheraCore
{
    public static class SyntheraUtils
    {
        public static void SetupPawn(Pawn pawn)
        {
            if (pawn == null) return;

            var hediffs = pawn.health.hediffSet.hediffs.ToList();
            foreach (Hediff hediff in hediffs)
                pawn.health.RemoveHediff(hediff);

            var hediffDef = DefDatabase<HediffDef>.GetNamed("SyntheraConsciousness", false);
            if (hediffDef != null)
                pawn.health.AddHediff(hediffDef);

            if (pawn.needs == null)
                pawn.needs = new Pawn_NeedsTracker(pawn);

            pawn.needs.AddOrRemoveNeedsAsAppropriate();

            var allNeeds = pawn.needs.AllNeeds.ToList();
            foreach (Need need in allNeeds)
                pawn.needs.AllNeeds.Remove(need);
        }
    }
}
