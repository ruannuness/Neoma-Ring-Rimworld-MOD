using Verse;

namespace SyntheraCore
{
    public class CompProperties_NeomaRing : CompProperties
    {
        public string pawnKind = "NeomaPawn";

        // Hibernation duration per ring level (0=no backup, 1=2d, 2=12h, 3=2h).
        public int[] respawnTicksByLevel = { 0, 120000, 30000, 5000 };

        public CompProperties_NeomaRing()
        {
            compClass = typeof(CompNeomaRing);
        }

        public int GetRespawnTicks(int level)
        {
            if (level <= 0 || level >= respawnTicksByLevel.Length) return 0;
            return respawnTicksByLevel[level];
        }
    }
}
