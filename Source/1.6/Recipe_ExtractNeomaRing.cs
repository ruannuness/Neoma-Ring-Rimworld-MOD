using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.Sound;

namespace SyntheraCore
{
    public class Recipe_ExtractNeomaRing : RecipeWorker
    {
        private const int WaveSize = 6;

        private static HediffDef ImplantedDef =>
            DefDatabase<HediffDef>.GetNamed("NeomaRingImplanted", false);

        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            if (thing is not Pawn pawn || !pawn.Dead) return false;
            var def = ImplantedDef;
            return def != null && pawn.health.hediffSet.HasHediff(def);
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer,
            List<Thing> ingredients, Bill bill)
        {
            // Surgery path: pawn is InnerPawn; use its corpse position.
            IntVec3 pos = pawn.Corpse?.Position ?? IntVec3.Invalid;
            ExecuteExtraction(pawn, pawn.Map ?? pawn.Corpse?.Map, pos);
        }

        // Called by the surgery bill and the right-click job.
        // position must be the Corpse.Position — InnerPawn.Position is always Invalid.
        public static void ExecuteExtraction(Pawn pawn, Map map, IntVec3 position)
        {
            if (map == null || !position.IsValid) return;

            var implantDef = ImplantedDef;
            if (implantDef != null)
            {
                var h = pawn.health.hediffSet.GetFirstHediffOfDef(implantDef);
                if (h != null) pawn.health.RemoveHediff(h);
            }

            // Extinguish the golden glow that marked this corpse.
            var corpse = pawn.Corpse;
            if (corpse != null && corpse.Spawned)
            {
                var glower = corpse.GetComp<CompGlower>();
                if (glower != null)
                {
                    map.glowGrid.DeRegisterGlower(glower);
                    corpse.AllComps.Remove(glower);
                }
            }

            var ringDef = DefDatabase<ThingDef>.GetNamed("NeomaRing", false);
            if (ringDef == null)
            {
                Log.Error("[SyntheraCore] ExtractNeomaRing: NeomaRing ThingDef not found.");
                return;
            }
            GenPlace.TryPlaceThing(ThingMaker.MakeThing(ringDef), position, map, ThingPlaceMode.Near);

            SoundDef.Named("PsychicAnimalPulserCast")
                ?.PlayOneShot(SoundInfo.InMap(new TargetInfo(position, map)));

            SpawnDefensiveWave(map, position);

            Messages.Message(
                "NeomaRing_RingAlarm".Translate(),
                new LookTargets(position, map),
                MessageTypeDefOf.ThreatBig);
        }

        private static void SpawnDefensiveWave(Map map, IntVec3 nearPos)
        {
            var sentryDef  = DefDatabase<PawnKindDef>.GetNamed("NeomaCorrupted_Sentry", false);
            var coreDef    = DefDatabase<PawnKindDef>.GetNamed("NeomaCorrupted_Core",   false);
            var factionDef = DefDatabase<FactionDef>.GetNamed("NeomaCorruptedAI",        false);
            if (sentryDef == null || factionDef == null) return;

            Faction faction = Find.FactionManager.FirstFactionOfDef(factionDef);
            if (faction == null)
            {
                faction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(factionDef));
                Find.FactionManager.Add(faction);
            }

            for (int i = 0; i < WaveSize; i++)
            {
                if (!TryFindWaveCell(map, nearPos, out IntVec3 loc)) continue;
                Pawn sentry = PawnGenerator.GeneratePawn(sentryDef, faction);
                GenSpawn.Spawn(sentry, loc, map);
            }

            if (coreDef != null && TryFindWaveCell(map, nearPos, out IntVec3 coreLoc))
            {
                Pawn core = PawnGenerator.GeneratePawn(coreDef, faction);
                GenSpawn.Spawn(core, coreLoc, map);
            }
        }

        // Space maps have no walkable edge — pick any standable interior cell away from the player.
        private static bool TryFindWaveCell(Map map, IntVec3 nearPos, out IntVec3 result)
        {
            for (int attempt = 0; attempt < 50; attempt++)
            {
                IntVec3 c = CellFinder.RandomCell(map);
                if (c.Standable(map) && c.DistanceTo(nearPos) > 15f)
                {
                    result = c;
                    return true;
                }
            }
            result = IntVec3.Invalid;
            return false;
        }
    }
}
