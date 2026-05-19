using Verse;

namespace SyntheraCore
{
    public enum AltarUpgradeType
    {
        SlotExpander,    // +N aux slots on linked altar
        RespawnBooster,  // ×multiplier on respawn/recreate cooldowns
        CoolingArray,    // reduces heat output of CompHeatPusher on linked altar
        PowerCondenser,  // reduces base power draw of linked altar
    }

    public class CompProperties_AltarUpgrade : CompProperties
    {
        public AltarUpgradeType upgradeType  = AltarUpgradeType.SlotExpander;
        public float detectionRadius         = 6f;
        public int   bonusAuxSlots           = 2;       // SlotExpander
        public float respawnMultiplier       = 0.7f;    // RespawnBooster  (< 1 = faster)
        public float heatReduction           = 0.6f;    // CoolingArray    (fraction subtracted)
        public float powerReduction          = 0.3f;    // PowerCondenser  (fraction subtracted)

        public CompProperties_AltarUpgrade()
        {
            compClass = typeof(CompAltarUpgrade);
        }
    }
}
