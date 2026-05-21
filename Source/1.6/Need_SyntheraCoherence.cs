using RimWorld;
using UnityEngine;
using Verse;

namespace SyntheraCore
{
    public class Need_SyntheraCoherence : Need
    {
        public Need_SyntheraCoherence(Pawn pawn) : base(pawn) { }

        public override float MaxLevel => 1f;

        // Only show the bar for Synthera pawns — colonists silently carry this at 1.0.
        public override bool ShowOnNeedList => IsSyntheraPawn;

        private bool IsSyntheraPawn => pawn.kindDef?.defName.StartsWith("NeomaPawn") == true;

        public override void NeedInterval()
        {
            if (!IsSyntheraPawn) return;
            if (!pawn.Spawned) return;
            CurLevel = Mathf.Max(0f, CurLevel - DrainPerInterval());
        }

        // Rates chosen so each tier lasts a distinct duration without recharger.
        // TierI ~1 day, TierII ~1.5 days, TierIII ~2.5 days, TierIV ~4 days.
        public float DrainPerInterval()
        {
            string kind = pawn.kindDef?.defName ?? "";
            if (kind.Contains("TierIV") || kind.Contains("Miku")) return 0.000625f;
            if (kind.Contains("TierIII"))                          return 0.001f;
            if (kind.Contains("TierII"))                           return 0.00167f;
            return 0.0025f;
        }

        public void Recharge(float amount)
        {
            CurLevel = Mathf.Min(1f, CurLevel + amount);
        }
    }
}
