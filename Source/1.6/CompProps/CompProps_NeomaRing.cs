using Verse;

namespace SyntheraCore
{
    public class CompProperties_NeomaRing : CompProperties
    {
        public string pawnKind;
        public int respawnTicks     = 120000; // calibration I: 2-day hibernation
        public int fastRespawnTicks = 60000;  // calibration II: 1-day hibernation

        public CompProperties_NeomaRing()
        {
            compClass = typeof(CompNeomaRing);
        }
    }
}
