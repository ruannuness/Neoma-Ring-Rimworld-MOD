using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace SyntheraCore
{
    // Runs after GenStep_OrbitalPlatform generates the map structure.
    // The station appears deserted — no living enemies.
    // Places a NeomaCorrupted_Core corpse (with NeomaRingImplanted) near a cryo casket.
    // Enemies only appear when the player extracts the ring (see Recipe_ExtractNeomaRing).
    public class SitePartWorker_NeomaOrbitalStation : SitePartWorker
    {
        private const int LandingClearRadius = 4;
        private const int CorpseOffsetFromCenter = 25;

        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);

            // Clear a landing zone at the map center so shuttles can always land.
            ClearLandingZone(map, map.Center, LandingClearRadius);

            var coreDef = DefDatabase<PawnKindDef>.GetNamed("NeomaCorrupted_Core", errorOnFail: false);
            var implant = DefDatabase<HediffDef>.GetNamed("NeomaRingImplanted",    errorOnFail: false);

            if (coreDef == null || implant == null)
            {
                Log.Warning("[SyntheraCore] SitePartWorker_NeomaOrbitalStation: missing defs.");
                return;
            }

            var faction = Find.FactionManager.FirstFactionOfDef(
                DefDatabase<FactionDef>.GetNamed("NeomaCorruptedAI", errorOnFail: false));

            Pawn boss = PawnGenerator.GeneratePawn(coreDef, faction);

            boss.health.AddHediff(implant);
            boss.Kill(dinfo: null);

            // Attach a golden glow to the corpse before spawning.
            // The glow is removed by Recipe_ExtractNeomaRing when the ring is taken.
            if (boss.Corpse != null)
            {
                var glowProps = new CompProperties_Glower { glowRadius = 7f, glowColor = new ColorInt(255, 185, 0, 0) };
                var glower    = new CompGlower { props = glowProps, parent = boss.Corpse };
                boss.Corpse.AllComps.Add(glower);
            }

            // Place corpse offset from center so it doesn't block the landing zone.
            IntVec3 spawnAnchor = map.Center + new IntVec3(CorpseOffsetFromCenter, 0, 0);
            IntVec3 corpseLoc   = CellFinder.RandomSpawnCellForPawnNear(spawnAnchor, map, 12);
            if (boss.Corpse != null)
            {
                GenSpawn.Spawn(boss.Corpse, corpseLoc, map);
                // Ancient relic — show skeleton stage immediately.
                var rot = boss.Corpse.GetComp<CompRottable>();
                if (rot != null) rot.RotProgress = 2_000_000f;
            }

            ThingDef cryoDef = DefDatabase<ThingDef>.GetNamed("AncientCryptosleepCasket", errorOnFail: false);
            if (cryoDef != null)
            {
                IntVec3 cryoLoc = corpseLoc + new IntVec3(0, 0, 2);
                if (cryoLoc.InBounds(map) && cryoLoc.Standable(map))
                    GenSpawn.Spawn(ThingMaker.MakeThing(cryoDef), cryoLoc, map, WipeMode.Vanish);
            }
        }

        // Removes blocking buildings and items from a radius around the landing center.
        // Leaves terrain, pawns, and corpses intact.
        private static void ClearLandingZone(Map map, IntVec3 center, int radius)
        {
            foreach (IntVec3 cell in CellRect.CenteredOn(center, radius).Cells)
            {
                if (!cell.InBounds(map)) continue;
                List<Thing> things = cell.GetThingList(map).ToList();
                foreach (Thing t in things)
                {
                    if (t is Pawn || t is Corpse) continue;
                    if (t.def.category == ThingCategory.Building ||
                        t.def.category == ThingCategory.Item)
                        t.Destroy(DestroyMode.Vanish);
                }
            }
        }
    }
}
