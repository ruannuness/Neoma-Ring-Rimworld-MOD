using RimWorld;
using UnityEngine;
using Verse;

namespace SyntheraCore
{
    public class Need_SyntheraCoherence : Need
    {
        public Need_SyntheraCoherence(Pawn pawn) : base(pawn) { }

        public override float MaxLevel => 1f;

        // Only show the bar for Synthera pawns — colonists silently carry this at 0.
        public override bool ShowOnNeedList => IsSyntheraPawn;

        private bool IsSyntheraPawn => pawn.kindDef?.defName.StartsWith("NeomaPawn") == true;

        // Set each CompTick by CompSyntheraSpawner based on altar tier and pawn/altar mismatch.
        // Higher altar tier = lower multiplier = cache fills slower = avatar lasts longer.
        public float AltarMultiplier = 1f;

        // Drain requested by Cache Purgers this interval; applied and capped in NeedInterval.
        // Transient — not saved; at most 150 ticks of drift on load.
        private float pendingDrain = 0f;

        public override void NeedInterval()
        {
            if (!IsSyntheraPawn) return;
            if (!pawn.Spawned) return;

            float effectiveMultiplier = Mathf.Max(AltarMultiplier, 0.3f);
            float rawFill = BaseFillPerInterval() * effectiveMultiplier;

            // Purgers can remove at most 80% of this interval's fill.
            // This guarantees cache always creeps upward — Purgers extend deployment
            // time significantly (up to ~50 in-game days on TierIV) without making it infinite.
            float cappedDrain = Mathf.Min(pendingDrain, rawFill * 0.8f);
            pendingDrain = 0f;

            CurLevel = Mathf.Clamp01(CurLevel + rawFill - cappedDrain);
            UpdateStrainHediff();
        }

        // Base fill rate per 150-tick interval (no altar bonus applied here).
        // Higher tier pawns are more efficient — their cache fills slower.
        public float BaseFillPerInterval()
        {
            string kind = pawn.kindDef?.defName ?? "";
            if (kind.Contains("TierIV") || kind.Contains("Miku")) return 0.00067f; // ~4 days to fill
            if (kind.Contains("TierIII"))                          return 0.001f;   // ~2.5 days
            if (kind.Contains("TierII"))                           return 0.00167f; // ~1.5 days
            return 0.0025f;                                                          // ~1 day (TierI)
        }

        // Called by CompSyntheraRecharger (Cache Purger) to request drain this interval.
        // Drain is accumulated and capped in NeedInterval — multiple Purgers don't stack unbounded.
        public void Purge(float amount)
        {
            pendingDrain += amount;
        }

        private void UpdateStrainHediff()
        {
            var strainDef = DefDatabase<HediffDef>.GetNamed("SyntheraCoherenceStrain", false);
            if (strainDef == null) return;

            if (CurLevel <= 0.75f)
            {
                var existing = pawn.health.hediffSet.GetFirstHediffOfDef(strainDef);
                if (existing != null) pawn.health.RemoveHediff(existing);
                return;
            }

            // Severity 0.25 = unstable (75–90%), 0.75 = critical (> 90%)
            float targetSev = CurLevel > 0.9f ? 0.75f : 0.25f;
            var strain = pawn.health.hediffSet.GetFirstHediffOfDef(strainDef)
                         ?? pawn.health.AddHediff(strainDef);
            strain.Severity = targetSev;
        }

        public void ClearStrainHediff()
        {
            var strainDef = DefDatabase<HediffDef>.GetNamed("SyntheraCoherenceStrain", false);
            if (strainDef == null) return;
            var h = pawn.health.hediffSet.GetFirstHediffOfDef(strainDef);
            if (h != null) pawn.health.RemoveHediff(h);
        }
    }
}
