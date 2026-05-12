using RimWorld;
using System.Linq;
using Verse;

namespace FormgelCore
{
    public static class FormgelUtils
    {
        public static void SetupPawn(Pawn pawn)
        {
            if (pawn == null) return;

            // Remove all existing hediffs
            var hediffs = pawn.health.hediffSet.hediffs.ToList();
            foreach (Hediff hediff in hediffs)
            {
                pawn.health.RemoveHediff(hediff);
            }
            
            // Add the FormgelConsciousness hediff
            var hediffDef = DefDatabase<HediffDef>.GetNamed("FormgelConsciousness", false);
            if (hediffDef != null)
            {
                pawn.health.AddHediff(hediffDef);
            }
            
            if (pawn.needs == null)
                pawn.needs = new Pawn_NeedsTracker(pawn);
            
            pawn.needs.AddOrRemoveNeedsAsAppropriate();

            // Remove all needs
            var allNeeds = pawn.needs.AllNeeds.ToList();
            foreach (Need need in allNeeds)
            {
                pawn.needs.AllNeeds.Remove(need);
            }
        }
    }
}
