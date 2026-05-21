using Verse;

namespace SyntheraCore
{
    public class CompProperties_SyntheraRecharger : CompProperties
    {
        // Coherence restored per 150-tick interval to each nearby spawned Synthera.
        // At 0.001f: +0.4/day, enough to sustain TierIII indefinitely and extend TierI/II.
        public float rechargePerInterval = 0.001f;
        public float radius = 8f;

        public CompProperties_SyntheraRecharger()
        {
            this.compClass = typeof(CompSyntheraRecharger);
        }
    }
}
