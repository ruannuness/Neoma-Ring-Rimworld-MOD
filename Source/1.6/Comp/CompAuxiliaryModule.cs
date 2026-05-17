using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace SyntheraCore
{
    public class CompAuxiliaryModule : ThingComp
    {
        // Saved
        private Thing linkedBuilding;
        private bool effectsApplied;
        private List<WorkTypeDef> unlockedByUs = new List<WorkTypeDef>();

        // Transient — rebuilt in PostSpawnSetup
        private CompSyntheraSpawner linkedSpawner;
        private CompPowerTrader compPower;
        private int lastScanTick;
        private int lastStressTick;

        public CompProperties_AuxiliaryModule Props => (CompProperties_AuxiliaryModule)props;

        private bool IsActive => compPower == null || compPower.PowerOn;

        private bool AvatarReady =>
            linkedSpawner?.Consciousness != null &&
            linkedSpawner.Consciousness.Spawned &&
            !linkedSpawner.Consciousness.Dead;

        // ── ThingComp lifecycle ──────────────────────────────────────────────

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            compPower = parent.TryGetComp<CompPowerTrader>();
            lastScanTick  = parent.thingIDNumber % Props.scanIntervalTicks;
            lastStressTick = parent.thingIDNumber % 2500;

            if (linkedBuilding != null)
                linkedSpawner = linkedBuilding.TryGetComp<CompSyntheraSpawner>();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref linkedBuilding, "linkedBuilding");
            Scribe_Values.Look(ref effectsApplied, "effectsApplied", false);
            Scribe_Collections.Look(ref unlockedByUs, "unlockedByUs", LookMode.Def);
            if (unlockedByUs == null) unlockedByUs = new List<WorkTypeDef>();
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned) return;

            int tick = Find.TickManager.TicksGame;
            if (tick - lastScanTick >= Props.scanIntervalTicks)
            {
                lastScanTick = tick;
                RefreshLinkedSpawner();
            }

            if (linkedSpawner != null)
            {
                if (IsActive && AvatarReady && !effectsApplied)
                    ApplyEffects();
                else if ((!IsActive || !AvatarReady) && effectsApplied)
                    RemoveEffects();
            }

            // Stress modification — relief (Optimizer) or addition (Overclock) every 2500 ticks
            if (effectsApplied && (Props.stressReliefPerDay > 0f || Props.stressAddedPerDay > 0f))
            {
                if (tick - lastStressTick >= 2500)
                {
                    lastStressTick = tick;
                    if (Props.stressReliefPerDay > 0f) TickStressRelief();
                    if (Props.stressAddedPerDay  > 0f) TickStressAdd();
                }
            }
        }

        public override void ReceiveCompSignal(string signal)
        {
            if (signal == "PowerTurnedOff" || signal == "FlickedOff")
                RemoveEffects();
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            RemoveEffects();
            if (linkedSpawner != null)
            {
                linkedSpawner.RegisteredAuxTypes.Remove(SlotKey);
                linkedSpawner = null;
            }
            linkedBuilding = null;
        }

        public override string CompInspectStringExtra()
        {
            if (linkedSpawner == null)
                return "Aux module: no altar in range";
            string status = effectsApplied ? "active" : (IsActive ? "avatar not deployed" : "no power");
            string extra = "";
            if (effectsApplied && Props.stressAddedPerDay > 0f)
                extra = $" [+{Props.stressAddedPerDay:0.###} stress/day]";
            return $"Aux module: linked to {linkedBuilding.def.label} [{status}]{extra}";
        }

        // ── Slot key ─────────────────────────────────────────────────────────
        // Role modules are keyed by their specific hediff name (allows multiple roles).
        // All other module types are keyed by their type name (one per type per altar).

        private string SlotKey =>
            Props.moduleType == AuxModuleType.RoleSpecializer
                ? Props.pawnBuffHediff
                : Props.moduleType.ToString();

        // ── Proximity scan ───────────────────────────────────────────────────

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

            RemoveEffects();
            if (linkedSpawner != null)
                linkedSpawner.RegisteredAuxTypes.Remove(SlotKey);

            linkedSpawner  = found;
            linkedBuilding = found?.parent;
        }

        // ── Effect dispatch ──────────────────────────────────────────────────

        private void ApplyEffects()
        {
            if (linkedSpawner == null) return;

            string key = SlotKey;

            if (Props.moduleType == AuxModuleType.RoleSpecializer)
            {
                if (linkedSpawner.RegisteredAuxTypes.Contains(key))
                {
                    Messages.Message(
                        $"This role specialization is already active on {linkedBuilding.def.label}.",
                        parent, MessageTypeDefOf.RejectInput, false);
                    return;
                }
                int activeRoles = linkedSpawner.RegisteredAuxTypes.Count(k => k.StartsWith("SyntheraRole"));
                if (activeRoles >= linkedSpawner.Props.maxRoleModules)
                {
                    Messages.Message(
                        $"{linkedBuilding.def.label} can support at most {linkedSpawner.Props.maxRoleModules} role module(s). Upgrade the altar to unlock more slots.",
                        parent, MessageTypeDefOf.RejectInput, false);
                    return;
                }
            }
            else if (linkedSpawner.RegisteredAuxTypes.Contains(key))
            {
                Messages.Message(
                    $"A {Props.moduleType} module is already active on {linkedBuilding.def.label}. Only one of each type is allowed per altar.",
                    parent, MessageTypeDefOf.RejectInput, false);
                return;
            }

            linkedSpawner.RegisteredAuxTypes.Add(key);
            effectsApplied = true;

            switch (Props.moduleType)
            {
                case AuxModuleType.CoreOptimizer:   ApplyCoreOptimizer();   break;
                case AuxModuleType.RoleSpecializer: ApplyRoleSpecializer(); break;
                case AuxModuleType.WorkUnlocker:    ApplyWorkUnlocker();    break;
                default:                            ApplyHediff(Props.pawnBuffHediff); break;
            }
        }

        private void RemoveEffects()
        {
            if (!effectsApplied) return;
            effectsApplied = false;

            if (linkedSpawner != null)
                linkedSpawner.RegisteredAuxTypes.Remove(SlotKey);

            switch (Props.moduleType)
            {
                case AuxModuleType.CoreOptimizer:   RemoveCoreOptimizer();   break;
                case AuxModuleType.RoleSpecializer: RemoveRoleSpecializer(); break;
                case AuxModuleType.WorkUnlocker:    RemoveWorkUnlocker();    break;
                default:                            RemoveHediff(Props.pawnBuffHediff); break;
            }
        }

        // ── Generic hediff helpers ───────────────────────────────────────────

        private void ApplyHediff(string hediffName)
        {
            if (hediffName.NullOrEmpty()) return;
            var def  = DefDatabase<HediffDef>.GetNamed(hediffName, false);
            var pawn = linkedSpawner?.Consciousness;
            if (def == null || pawn == null) return;
            if (pawn.health.hediffSet.GetFirstHediffOfDef(def) == null)
                pawn.health.AddHediff(def).Severity = 1.0f;
        }

        private void RemoveHediff(string hediffName)
        {
            if (hediffName.NullOrEmpty()) return;
            var def  = DefDatabase<HediffDef>.GetNamed(hediffName, false);
            var pawn = linkedSpawner?.Consciousness;
            if (def == null || pawn == null) return;
            var h = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (h != null) pawn.health.RemoveHediff(h);
        }

        // ── CoreOptimizer ────────────────────────────────────────────────────

        private void ApplyCoreOptimizer()
        {
            linkedSpawner.AuxRespawnMultiplier  = Props.respawnTicksMultiplier;
            linkedSpawner.AuxRecreateMultiplier = Props.recreateCooldownMultiplier;
        }

        private void RemoveCoreOptimizer()
        {
            if (linkedSpawner != null)
            {
                linkedSpawner.AuxRespawnMultiplier  = 1f;
                linkedSpawner.AuxRecreateMultiplier = 1f;
            }
        }

        private void TickStressRelief()
        {
            var pawn = linkedSpawner?.Consciousness;
            if (pawn == null || !pawn.Spawned || pawn.Dead) return;
            var stressDef = DefDatabase<HediffDef>.GetNamed("SyntheraSystemStress", false);
            if (stressDef == null) return;
            var stress = pawn.health.hediffSet.GetFirstHediffOfDef(stressDef);
            if (stress == null) return;
            float relief = Props.stressReliefPerDay * 2500f / 60000f;
            stress.Severity = Mathf.Max(0f, stress.Severity - relief);
        }

        private void TickStressAdd()
        {
            var pawn = linkedSpawner?.Consciousness;
            if (pawn == null || !pawn.Spawned || pawn.Dead) return;
            var stressDef = DefDatabase<HediffDef>.GetNamed("SyntheraSystemStress", false);
            if (stressDef == null) return;
            var stress = pawn.health.hediffSet.GetFirstHediffOfDef(stressDef);
            if (stress == null) return;
            float addition = Props.stressAddedPerDay * 2500f / 60000f;
            stress.Severity = Mathf.Min(1f, stress.Severity + addition);
        }

        // ── RoleSpecializer ──────────────────────────────────────────────────

        private void ApplyRoleSpecializer()
        {
            if (Props.pawnBuffHediff.NullOrEmpty()) return;
            var pawn = linkedSpawner?.Consciousness;
            if (pawn == null) return;
            StripAllRoleHediffs(pawn);
            ApplyHediff(Props.pawnBuffHediff);
        }

        private void RemoveRoleSpecializer()
        {
            var pawn = linkedSpawner?.Consciousness;
            if (pawn != null) StripAllRoleHediffs(pawn);
        }

        private static void StripAllRoleHediffs(Pawn pawn)
        {
            var toRemove = pawn.health.hediffSet.hediffs
                .Where(h => h.def.defName.StartsWith("SyntheraRole"))
                .ToList();
            foreach (var h in toRemove)
                pawn.health.RemoveHediff(h);
        }

        // ── WorkUnlocker ─────────────────────────────────────────────────────

        private void ApplyWorkUnlocker()
        {
            if (Props.unlockedWorkTypes == null) return;
            var pawn = linkedSpawner?.Consciousness;
            if (pawn?.workSettings == null) return;

            unlockedByUs.Clear();
            foreach (string wtName in Props.unlockedWorkTypes)
            {
                var wt = DefDatabase<WorkTypeDef>.GetNamed(wtName, false);
                if (wt == null)
                {
                    Log.Warning($"SyntheraCore: WorkUnlocker — WorkTypeDef '{wtName}' not found.");
                    continue;
                }
                if (pawn.workSettings.GetPriority(wt) == 0)
                {
                    pawn.workSettings.SetPriority(wt, 3);
                    unlockedByUs.Add(wt);
                }
            }
        }

        private void RemoveWorkUnlocker()
        {
            var pawn = linkedSpawner?.Consciousness;
            if (pawn?.workSettings == null) { unlockedByUs.Clear(); return; }

            foreach (WorkTypeDef wt in unlockedByUs)
                pawn.workSettings.SetPriority(wt, 0);

            unlockedByUs.Clear();
        }
    }
}
