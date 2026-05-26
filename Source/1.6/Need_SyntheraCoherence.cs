using RimWorld;
using UnityEngine;
using Verse;

namespace SyntheraCore
{
    public class Need_SyntheraCoherence : Need
    {
        private static readonly Color BarColorLow  = new Color(0.20f, 0.60f, 1.00f); // blue
        private static readonly Color BarColorMid  = new Color(1.00f, 0.80f, 0.00f); // yellow
        private static readonly Color BarColorHigh = new Color(1.00f, 0.18f, 0.10f); // red
        private static readonly Color BarBg        = new Color(0.10f, 0.10f, 0.12f);
        private static readonly Color BarBorder    = new Color(0.25f, 0.25f, 0.28f);

        public Need_SyntheraCoherence(Pawn pawn) : base(pawn) { }

        public override void DrawOnGUI(Rect rect, int maxThresholdMarkers = 8, float customMargin = -1f,
            bool drawArrows = true, bool doTooltip = true, Rect? rectForTooltip = null, bool drawLabel = true)
        {
            // Let vanilla handle the label, background and tooltip
            base.DrawOnGUI(rect, maxThresholdMarkers, customMargin, drawArrows, doTooltip, rectForTooltip, drawLabel);

            // Recalculate the fill rect using the same layout vanilla uses
            if (rect.height != 28f)
            {
                int trim = Mathf.RoundToInt((rect.height - 28f) / 2f);
                rect.yMin += trim;
                rect.yMax -= trim;
            }
            float margin = customMargin >= 0f ? customMargin : 6f;
            Rect barRect = new Rect(rect.xMax - rect.width * 0.55f, rect.y + margin, rect.width * 0.55f - 4f, rect.height - margin * 2f);

            // Overdraw the fill area with our gradient colour
            float fill = Mathf.Clamp01(CurLevel / MaxLevel);
            if (fill > 0f)
            {
                Rect fillRect = new Rect(barRect.x + 1f, barRect.y + 1f, (barRect.width - 2f) * fill, barRect.height - 2f);
                Color c = fill < 0.5f
                    ? Color.Lerp(BarColorLow, BarColorMid, fill * 2f)
                    : Color.Lerp(BarColorMid, BarColorHigh, (fill - 0.5f) * 2f);
                Widgets.DrawBoxSolid(fillRect, c);
            }
        }

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
