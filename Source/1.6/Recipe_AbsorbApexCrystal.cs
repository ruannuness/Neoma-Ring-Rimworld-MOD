using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.Sound;

namespace SyntheraCore
{
    public class ApexCrystalRecipeExtension : DefModExtension
    {
        public int apexLevelTarget = 1; // 1, 2, or 3
    }

    public class Recipe_AbsorbApexCrystal : Recipe_Surgery
    {
        private static readonly string[] ApexHediffNames =
        {
            "SyntheraApex_I",
            "SyntheraApex_II",
            "SyntheraApex_III"
        };

        private int TargetLevel => recipe.GetModExtension<ApexCrystalRecipeExtension>()?.apexLevelTarget ?? 1;

        private static int GetCurrentApexLevel(Pawn pawn)
        {
            var hediffs = pawn.health.hediffSet;
            for (int i = ApexHediffNames.Length - 1; i >= 0; i--)
            {
                var def = DefDatabase<HediffDef>.GetNamed(ApexHediffNames[i], false);
                if (def != null && hediffs.HasHediff(def)) return i + 1;
            }
            return 0;
        }

        private static bool IsValidTarget(Pawn pawn)
        {
            return pawn?.def?.defName?.StartsWith("SyntheraRace") == true
                || pawn?.kindDef?.defName == "NeomaPawn";
        }

        // Walk all maps to find the ring worn by a pawn that has neomaPawn == target.
        private static CompNeomaRing FindRingForNeoma(Pawn neoma)
        {
            foreach (Map map in Find.Maps)
            {
                foreach (Pawn wearer in map.mapPawns.AllPawnsSpawned)
                {
                    if (wearer.apparel == null) continue;
                    foreach (Apparel a in wearer.apparel.WornApparel)
                    {
                        var comp = a.TryGetComp<CompNeomaRing>();
                        if (comp != null && comp.NeomaPawn == neoma) return comp;
                    }
                }
            }
            return null;
        }

        public override bool AvailableOnNow(Thing thing, BodyPartRecord bp)
        {
            if (!(thing is Pawn pawn)) return false;
            if (!IsValidTarget(pawn)) return false;
            return GetCurrentApexLevel(pawn) == TargetLevel - 1;
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            int target = TargetLevel;

            // Remove all previous apex hediffs before adding the new tier.
            foreach (string name in ApexHediffNames)
            {
                var def = DefDatabase<HediffDef>.GetNamed(name, false);
                if (def == null) continue;
                var existing = pawn.health.hediffSet.GetFirstHediffOfDef(def);
                if (existing != null) pawn.health.RemoveHediff(existing);
            }

            var newDef = HediffDef.Named(ApexHediffNames[target - 1]);
            pawn.health.AddHediff(newDef);

            // For the Neoma companion, also advance the ring's level and BioLock.
            if (pawn.kindDef?.defName == "NeomaPawn")
                FindRingForNeoma(pawn)?.AdvanceLevel();

            if (pawn.Map != null)
            {
                SoundDefOf.PsychicPulseGlobal.PlayOneShotOnCamera(pawn.Map);
                FleckMaker.Static(pawn.Position, pawn.Map, FleckDefOf.PsycastAreaEffect, 5f);
            }

            string levelName = target == 1 ? "Apex I"
                             : target == 2 ? "Apex II"
                                           : "Apex III — Transcendent";
            Messages.Message(
                $"{pawn.LabelShort} has absorbed the synthetic apex crystal and reached {levelName}.",
                pawn, MessageTypeDefOf.PositiveEvent);
        }
    }
}
