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

        // Transient — rebuilt in PostSpawnSetup / CompTick
        private CompSyntheraSpawner linkedSpawner;
        private CompPowerTrader compPower;
        private int lastScanTick;
        private bool capRejected;
        private bool typeRejected;

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
            lastScanTick = parent.thingIDNumber % Props.scanIntervalTicks;

            if (linkedBuilding != null)
                linkedSpawner = linkedBuilding.TryGetComp<CompSyntheraSpawner>();

            // Repopulate the transient RegisteredAuxTypes set after a save/load.
            // effectsApplied is saved; without this, the slot key would be missing from
            // the set until the next ApplyEffects() call, breaking the duplicate/cap checks.
            if (respawningAfterLoad && effectsApplied && linkedSpawner != null)
                linkedSpawner.RegisteredAuxTypes.Add(SlotKey);
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
            if (tick - lastScanTick < Props.scanIntervalTicks) return;
            lastScanTick = tick;

            RefreshLinkedSpawner();

            if (linkedSpawner == null) return;

            // If previously rejected as duplicate, re-check whether the conflict was removed.
            if (typeRejected && !linkedSpawner.RegisteredAuxTypes.Contains(SlotKey))
                typeRejected = false;

            if (IsActive && AvatarReady && !effectsApplied && !typeRejected)
                ApplyEffects();
            else if ((!IsActive || !AvatarReady) && effectsApplied)
                RemoveEffects();
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
            string status = effectsApplied  ? "active"
                          : capRejected     ? "module limit reached"
                          : !IsActive       ? "no power"
                          :                   "avatar not deployed";
            string detail = BuildDetailLine();
            string suffix = detail.NullOrEmpty() ? "" : $"\n{detail}";
            return $"Aux module: linked to {linkedBuilding.def.label} [{status}]{suffix}";
        }

        private string BuildDetailLine()
        {
            switch (Props.moduleType)
            {
                case AuxModuleType.CoreOptimizer:
                {
                    var parts = new List<string>();
                    if (Props.respawnTicksMultiplier != 1f)
                        parts.Add($"Respawn ×{Props.respawnTicksMultiplier:0.##}");
                    if (Props.recreateCooldownMultiplier != 1f)
                        parts.Add($"Recreate ×{Props.recreateCooldownMultiplier:0.##}");
                    return parts.Count > 0 ? string.Join(" | ", parts) : "";
                }
                case AuxModuleType.WorkUnlocker:
                {
                    if (unlockedByUs.Count == 0) return "";
                    var names = unlockedByUs
                        .Where(w => w != null)
                        .Select(w => w.label?.CapitalizeFirst())
                        .Where(s => !s.NullOrEmpty())
                        .ToList();
                    return names.Count > 0 ? "Unlocks: " + string.Join(", ", names) : "";
                }
                case AuxModuleType.RoleSpecializer:
                {
                    if (Props.pawnBuffHediff.NullOrEmpty()) return "";
                    var def = DefDatabase<HediffDef>.GetNamed(Props.pawnBuffHediff, false);
                    return def != null ? $"Role: {def.label.CapitalizeFirst()}" : "";
                }
                default:
                {
                    if (Props.pawnBuffHediff.NullOrEmpty()) return "";
                    var def = DefDatabase<HediffDef>.GetNamed(Props.pawnBuffHediff, false);
                    return def != null ? def.label.CapitalizeFirst() : "";
                }
            }
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
            float radius = Props.detectionRadius;
            if (linkedSpawner != null && linkedSpawner.SignalBurstActive)
                radius *= 2f;

            CompSyntheraSpawner found = null;
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(
                         parent.Position, parent.Map, radius, useCenter: true))
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
            typeRejected   = false;
            capRejected    = false;
        }

        // ── Effect dispatch ──────────────────────────────────────────────────

        private void ApplyEffects()
        {
            if (linkedSpawner == null) return;

            string key = SlotKey;

            if (linkedSpawner.RegisteredAuxTypes.Count >= linkedSpawner.Props.maxAuxModules + linkedSpawner.BonusAuxSlots
                && !linkedSpawner.RegisteredAuxTypes.Contains(key))
            {
                capRejected = true;
                return;
            }
            capRejected = false;

            if (linkedSpawner.RegisteredAuxTypes.Contains(key))
            {
                if (!typeRejected)
                {
                    string msg = Props.moduleType == AuxModuleType.RoleSpecializer
                        ? $"This role specialization is already active on {linkedBuilding.def.label}."
                        : $"A {Props.moduleType} module is already active on {linkedBuilding.def.label}. Only one of each type is allowed per altar.";
                    Messages.Message(msg, parent, MessageTypeDefOf.RejectInput, false);
                }
                typeRejected = true;
                capRejected  = true;
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
