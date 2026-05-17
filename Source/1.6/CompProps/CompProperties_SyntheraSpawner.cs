using Verse;

namespace SyntheraCore
{
    public class CompProperties_SyntheraSpawner : CompProperties
    {
        public string pawnKind;
        public float spawnIntervalDays = 1f;
        public int maxPawnsToSpawn = 1;
        public SoundDef spawnSound;
        public int respawnTicks = 60000;
        public float explosionRadius = 4.9f;
        public int recreateCooldownTicks = 180000; // 3 in-game days (72 in-game hours)
        public int maxRoleModules = 1;            // how many role specializer nodes can link simultaneously

        public CompProperties_SyntheraSpawner()
        {
            this.compClass = typeof(CompSyntheraSpawner);
        }
    }
}
