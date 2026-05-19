using RimWorld;
using Verse;

namespace SyntheraCore
{
    // Placed adjacent to an altar; modifies the altar's structural properties rather
    // than the Synthera pawn. Scans for CompSyntheraSpawner in detectionRadius,
    // registers itself, and applies its bonus to transient fields on the spawner.
    public class CompAltarUpgrade : ThingComp
    {
        private Thing linkedBuilding;
        private CompSyntheraSpawner linkedSpawner;
        private CompPowerTrader compPower;
        private bool effectsApplied;
        private int lastScanTick;

        public CompProperties_AltarUpgrade Props => (CompProperties_AltarUpgrade)props;

        private bool IsActive => compPower == null || compPower.PowerOn;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            compPower   = parent.TryGetComp<CompPowerTrader>();
            lastScanTick = parent.thingIDNumber % 250;

            if (linkedBuilding != null)
            {
                linkedSpawner = linkedBuilding.TryGetComp<CompSyntheraSpawner>();
                if (respawningAfterLoad && effectsApplied && linkedSpawner != null)
                    ApplyBonus();
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref linkedBuilding, "linkedBuilding");
            Scribe_Values.Look(ref effectsApplied, "effectsApplied", false);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned) return;

            int tick = Find.TickManager.TicksGame;
            if (tick - lastScanTick >= 250)
            {
                lastScanTick = tick;
                RefreshLinkedSpawner();
            }

            if (linkedSpawner != null)
            {
                if (IsActive && !effectsApplied)   ApplyBonus();
                else if (!IsActive && effectsApplied) RemoveBonus();
            }
        }

        public override void ReceiveCompSignal(string signal)
        {
            if (signal == "PowerTurnedOff" || signal == "FlickedOff")
                RemoveBonus();
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            RemoveBonus();
        }

        private void RefreshLinkedSpawner()
        {
            CompSyntheraSpawner found = null;
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(
                         parent.Position, parent.Map, Props.detectionRadius, useCenter: true))
            {
                var spawner = t.TryGetComp<CompSyntheraSpawner>();
                if (spawner != null) { found = spawner; break; }
            }

            if (found == linkedSpawner) return;

            RemoveBonus();
            linkedSpawner  = found;
            linkedBuilding = found?.parent;
        }

        private void ApplyBonus()
        {
            if (linkedSpawner == null) return;
            effectsApplied = true;

            switch (Props.upgradeType)
            {
                case AltarUpgradeType.SlotExpander:
                    linkedSpawner.BonusAuxSlots += Props.bonusAuxSlots;
                    break;
                case AltarUpgradeType.RespawnBooster:
                    linkedSpawner.SpawnIntervalMultiplier *= Props.respawnMultiplier;
                    break;
                case AltarUpgradeType.CoolingArray:
                    linkedSpawner.HeatOutputMultiplier = System.Math.Max(0f, linkedSpawner.HeatOutputMultiplier - Props.heatReduction);
                    break;
                case AltarUpgradeType.PowerCondenser:
                    linkedSpawner.PowerMultiplier = System.Math.Max(0f, linkedSpawner.PowerMultiplier - Props.powerReduction);
                    break;
            }
        }

        private void RemoveBonus()
        {
            if (!effectsApplied || linkedSpawner == null) { effectsApplied = false; return; }
            effectsApplied = false;

            switch (Props.upgradeType)
            {
                case AltarUpgradeType.SlotExpander:
                    linkedSpawner.BonusAuxSlots -= Props.bonusAuxSlots;
                    if (linkedSpawner.BonusAuxSlots < 0) linkedSpawner.BonusAuxSlots = 0;
                    break;
                case AltarUpgradeType.RespawnBooster:
                    if (Props.respawnMultiplier > 0f)
                        linkedSpawner.SpawnIntervalMultiplier /= Props.respawnMultiplier;
                    break;
                case AltarUpgradeType.CoolingArray:
                    linkedSpawner.HeatOutputMultiplier = System.Math.Min(1f, linkedSpawner.HeatOutputMultiplier + Props.heatReduction);
                    break;
                case AltarUpgradeType.PowerCondenser:
                    linkedSpawner.PowerMultiplier = System.Math.Min(1f, linkedSpawner.PowerMultiplier + Props.powerReduction);
                    break;
            }
        }

        public override string CompInspectStringExtra()
        {
            if (linkedSpawner == null)
                return $"Altar upgrade: no altar in range";

            string status = effectsApplied ? "active" : IsActive ? "avatar not deployed" : "no power";
            return $"Altar upgrade [{Props.upgradeType}]: linked to {linkedBuilding.def.label} [{status}]";
        }
    }
}
