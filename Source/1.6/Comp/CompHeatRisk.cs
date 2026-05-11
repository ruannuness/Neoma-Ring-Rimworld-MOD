using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FormgelCore
{
    public class CompHeatRisk : ThingComp
    {
        public CompProperties_HeatRisk Props => (CompProperties_HeatRisk)props;

        private int lastHeatCheckTick = -1;
        private const int CHECK_INTERVAL = 2500; // Check every hour (2500 ticks)

        public override void CompTick()
        {
            base.CompTick();

            if (!parent.Spawned || Find.TickManager.TicksGame - lastHeatCheckTick < CHECK_INTERVAL)
                return;

            lastHeatCheckTick = Find.TickManager.TicksGame;

            // Check ambient temperature
            float ambientTemp = parent.Position.GetTemperature(parent.Map);

            if (ambientTemp >= Props.heatThreshold)
            {
                // Calculate explosion chance based on temperature difference
                float tempDifference = ambientTemp - Props.heatThreshold;
                float explosionChance = Props.explosionChancePerHour * (1f + tempDifference / 10f); // Higher temp = higher chance

                if (Rand.Value < explosionChance)
                {
                    TriggerExplosion();
                }
            }
        }

        private void TriggerExplosion()
        {
            // Find the spawner component
            CompFormgelSpawner spawner = parent.GetComp<CompFormgelSpawner>();
            if (spawner != null && spawner.Consciousness != null)
            {
                // Despawn the pawn with explosion
                spawner.DespawnFormgel(true, false); // explode but not gone for good
            }

            // Create explosion
            DamageDef explosionDamage = DefDatabase<DamageDef>.GetNamed("Flame", false) ?? DefDatabase<DamageDef>.GetNamed("Bomb");
            GenExplosion.DoExplosion(
                parent.Position,
                parent.Map,
                Props.explosionRadius,
                explosionDamage,
                null,
                (int)Props.explosionDamage,
                postExplosionSpawnThingDef: ThingDefOf.Filth_Ash,
                postExplosionSpawnChance: 0.5f,
                postExplosionSpawnThingCount: 1
            );

            // Damage the building itself
            parent.TakeDamage(new DamageInfo(DamageDefOf.Flame, Props.explosionDamage * 0.5f, 0f));

            // Create fire
            // FireUtility.TryStartFireIn(parent.Position, parent.Map, 1f);
        }

        public override string CompInspectStringExtra()
        {
            float ambientTemp = parent.Spawned ? parent.Position.GetTemperature(parent.Map) : 0f;

            if (ambientTemp >= Props.heatThreshold)
            {
                float tempDifference = ambientTemp - Props.heatThreshold;
                float explosionChance = Props.explosionChancePerHour * (1f + tempDifference / 10f);
                return $"Heat risk: {explosionChance.ToStringPercent()} chance/hour";
            }

            return null;
        }
    }
}