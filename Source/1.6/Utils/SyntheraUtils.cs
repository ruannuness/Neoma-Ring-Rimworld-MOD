using RimWorld;
using System.Linq;
using Verse;

namespace SyntheraCore
{
    public static class SyntheraUtils
    {
        // Shared setup for altar Syntheras (SyntheraRace* pawns).
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

        // Setup for the Neoma ring companion (Human race, sustained by BioLock — no Cache need).
        public static void SetupNeomaCompanion(Pawn pawn)
        {
            if (pawn == null) return;

            // Strip all hediffs from generation.
            var hediffs = pawn.health.hediffSet.hediffs.ToList();
            foreach (Hediff h in hediffs)
                pawn.health.RemoveHediff(h);

            // SyntheraConsciousness marks her as a synthetic avatar.
            var conDef = DefDatabase<HediffDef>.GetNamed("SyntheraConsciousness", false);
            if (conDef != null)
                pawn.health.AddHediff(conDef);

            // Strip all needs — Neoma is sustained by the BioLock, not food or sleep.
            if (pawn.needs == null)
                pawn.needs = new Pawn_NeedsTracker(pawn);
            var allNeeds = pawn.needs.AllNeeds.ToList();
            foreach (Need need in allNeeds)
                pawn.needs.AllNeeds.Remove(need);

            // Strip any generated apparel and weapons — Neoma has no equipment from generation.
            if (pawn.apparel != null)
                foreach (var a in pawn.apparel.WornApparel.ToList())
                    pawn.apparel.Remove(a);

            if (pawn.equipment != null)
                foreach (var eq in pawn.equipment.AllEquipmentListForReading.ToList())
                    pawn.equipment.Remove(eq);

            // Apply Neoma-specific backstories.
            if (pawn.story != null)
            {
                var childhood = DefDatabase<BackstoryDef>.GetNamed("Neoma_Origin", false);
                var adulthood = DefDatabase<BackstoryDef>.GetNamed("Neoma_Awakening", false);
                if (childhood != null) pawn.story.Childhood = childhood;
                if (adulthood != null) pawn.story.Adulthood = adulthood;
            }
        }
    }
}
