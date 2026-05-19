using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SyntheraCore
{
    public class CompNeomaRing : ThingComp
    {
        public CompProperties_NeomaRing Props => (CompProperties_NeomaRing)props;

        private Pawn neomaPawn;
        private int  ringCalibration = 0; // 0–2; will become resonanceLevel 0–5 in Phase B
        private bool inHibernation   = false;
        private int  respawnTick     = 0;

        // Phase A: track wearer for permanent BioLock + death detection
        private Pawn _wearer;
        private bool isBound = false;

        // Severity table: maps ring calibration level → BioLock severity.
        // Phase B will extend this to 6 entries (0–5).
        private static readonly float[] BiolockSeverityByLevel = { 0.15f, 0.35f, 0.55f };

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref neomaPawn, "neomaPawn");
            Scribe_References.Look(ref _wearer,   "_wearer");
            Scribe_Values.Look(ref ringCalibration, "ringCalibration", 0);
            Scribe_Values.Look(ref inHibernation,   "inHibernation",   false);
            Scribe_Values.Look(ref respawnTick,      "respawnTick",     0);
            Scribe_Values.Look(ref isBound,          "isBound",         false);
        }

        public override void CompTick()
        {
            base.CompTick();

            // ── Detect wearer death → release BioLock ──────────────────────
            if (isBound && _wearer != null && _wearer.Dead)
                UnbindFromWearer();

            // ── Companion death → always hibernate (never permanent death) ──
            if (neomaPawn == null || !neomaPawn.Dead || inHibernation) return;

            Corpse  corpse   = neomaPawn.Corpse;
            IntVec3 deathPos = corpse?.Position
                            ?? (parent as Apparel)?.Wearer?.Position
                            ?? IntVec3.Invalid;

            inHibernation = true;
            int delay = ringCalibration >= 2 ? Props.fastRespawnTicks : Props.respawnTicks;
            respawnTick = Find.TickManager.TicksGame + delay;

            if (ResurrectionUtility.TryResurrect(neomaPawn))
            {
                var sickDef = DefDatabase<HediffDef>.GetNamed("ResurrectionSickness", false);
                if (sickDef != null)
                {
                    var sick = neomaPawn.health.hediffSet.GetFirstHediffOfDef(sickDef);
                    if (sick != null) neomaPawn.health.RemoveHediff(sick);
                }

                if (deathPos.IsValid)
                {
                    neomaPawn.apparel?.DropAll(deathPos);
                    neomaPawn.inventory?.DropAllNearPawn(deathPos);
                }

                if (corpse != null && !corpse.Destroyed)
                    corpse.Destroy(DestroyMode.Vanish);

                if (neomaPawn.Spawned)
                    neomaPawn.DeSpawn();
            }

            Messages.Message(
                $"The Neoma Ring's companion has entered hibernation. Restoration available in {GenDate.ToStringTicksToPeriod(respawnTick - Find.TickManager.TicksGame)}.",
                MessageTypeDefOf.NegativeEvent, false);
        }

        // Called when the ring is equipped by a pawn.
        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);

            // If the previous wearer died, handle unbind before allowing a new bind.
            if (isBound && _wearer != null && _wearer.Dead)
                UnbindFromWearer();

            HediffDef biolockDef = HediffDef.Named("NeomaBiolock");

            // Reject equip if the ring is already bound to a living colonist.
            foreach (Map map in Find.Maps)
            {
                foreach (Pawn colonist in map.mapPawns.FreeColonists)
                {
                    if (colonist != pawn && colonist.health.hediffSet.HasHediff(biolockDef))
                    {
                        Messages.Message(
                            $"The Neoma Ring is biologically locked to {colonist.LabelShort}. It does not respond to {pawn.LabelShort}.",
                            MessageTypeDefOf.RejectInput, false);
                        return;
                    }
                }
            }

            // Bind to this pawn.
            _wearer = pawn;
            isBound = true;

            if (!pawn.health.hediffSet.HasHediff(biolockDef))
                pawn.health.AddHediff(biolockDef);

            // Sync BioLock severity to current calibration level.
            UpdateBiolockSeverity(pawn);

            if (neomaPawn == null || neomaPawn.Dead || neomaPawn.Destroyed)
            {
                if (!inHibernation)
                    SpawnNeoma(pawn);
            }
            else if (!neomaPawn.Spawned && !inHibernation)
            {
                GenPlace.TryPlaceThing(neomaPawn, pawn.Position, pawn.Map, ThingPlaceMode.Near);
            }
        }

        // Called when the ring is removed from the pawn.
        // BioLock is intentionally NOT removed here — it is permanent.
        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            if (neomaPawn != null && neomaPawn.Spawned)
                neomaPawn.DeSpawn();
        }

        // ── BioLock severity management ──────────────────────────────────────

        private void UpdateBiolockSeverity(Pawn wearer)
        {
            if (wearer == null || wearer.Dead) return;
            HediffDef biolockDef = HediffDef.Named("NeomaBiolock");
            var biolock = wearer.health.hediffSet.GetFirstHediffOfDef(biolockDef);
            if (biolock == null) return;
            int idx = System.Math.Min(ringCalibration, BiolockSeverityByLevel.Length - 1);
            biolock.Severity = BiolockSeverityByLevel[idx];
        }

        // ── Wearer-death unbind ───────────────────────────────────────────────

        private void UnbindFromWearer()
        {
            HediffDef biolockDef = HediffDef.Named("NeomaBiolock");
            var biolock = _wearer?.health.hediffSet.GetFirstHediffOfDef(biolockDef);
            if (biolock != null)
                _wearer.health.RemoveHediff(biolock);

            if (neomaPawn != null && neomaPawn.Spawned)
                neomaPawn.DeSpawn();

            isBound = false;
            _wearer = null;

            Messages.Message(
                "The Neoma Ring's wearer has died. The BioLock has been released — the ring can bind to a new host.",
                MessageTypeDefOf.NeutralEvent, false);
        }

        // ── Companion spawn ───────────────────────────────────────────────────

        private void SpawnNeoma(Pawn wearer)
        {
            PawnKindDef neomaKind = DefDatabase<PawnKindDef>.GetNamed(Props.pawnKind, false);
            if (neomaKind == null)
            {
                Log.Error($"SyntheraCore: Could not find PawnKindDef '{Props.pawnKind}' for NeomaRing");
                return;
            }

            PawnGenerationRequest req = new PawnGenerationRequest(
                neomaKind,
                Faction.OfPlayer,
                PawnGenerationContext.NonPlayer,
                -1, true, false, false, false, true, 0,
                allowFood: false,
                allowAddictions: false,
                forceNoIdeo: true,
                forbidAnyTitle: true,
                fixedBiologicalAge: 25,
                fixedChronologicalAge: 25,
                forceNoBackstory: true
            );

            Pawn neoma = PawnGenerator.GeneratePawn(req);
            neoma.Name = new NameTriple("Neoma", "", "");

            if (neoma.relations    == null) neoma.relations    = new Pawn_RelationsTracker(neoma);
            if (neoma.interactions == null) neoma.interactions = new Pawn_InteractionsTracker(neoma);

            while (neoma.story.traits.allTraits.Count > 0)
                neoma.story.traits.allTraits.RemoveLast();

            SyntheraUtils.SetupPawn(neoma);

            GenPlace.TryPlaceThing(neoma, wearer.Position, wearer.Map, ThingPlaceMode.Near);
            neomaPawn = neoma;

            SoundDefOf.PsychicPulseGlobal.PlayOneShotOnCamera(wearer.Map);
            FleckMaker.Static(wearer.Position, wearer.Map, FleckDefOf.PsycastAreaEffect, 5f);
            Messages.Message("The Neoma Ring has summoned its bound companion.", MessageTypeDefOf.PositiveEvent);
        }

        // ── Companion restoration from hibernation ────────────────────────────

        private void RestoreCompanion()
        {
            Pawn wearer = _wearer ?? (parent as Apparel)?.Wearer;
            if (wearer == null || !wearer.Spawned) return;

            inHibernation = false;
            GenPlace.TryPlaceThing(neomaPawn, wearer.Position, wearer.Map, ThingPlaceMode.Near);

            if (ringCalibration >= 2)
            {
                var syndromeDef = DefDatabase<HediffDef>.GetNamed("SyntheraHibernationSyndrome", false);
                if (syndromeDef != null)
                    neomaPawn.health.AddHediff(syndromeDef).Severity = 1f;
            }

            var conDef = DefDatabase<HediffDef>.GetNamed("SyntheraConsciousness", false);
            if (conDef != null && neomaPawn.health.hediffSet.GetFirstHediffOfDef(conDef) == null)
                neomaPawn.health.AddHediff(conDef);

            SoundDefOf.PsychicPulseGlobal.PlayOneShotOnCamera(wearer.Map);
            FleckMaker.Static(wearer.Position, wearer.Map, FleckDefOf.PsycastAreaEffect, 3f);
            Messages.Message("The Neoma Ring's companion has been restored from backup.", MessageTypeDefOf.PositiveEvent);
        }

        // ── Calibration material consumption ─────────────────────────────────

        private bool TryConsumeCalibrationMaterials(int calibLevel)
        {
            Pawn wearer = _wearer ?? (parent as Apparel)?.Wearer;
            Map  map    = wearer?.Map ?? Find.AnyPlayerHomeMap;
            if (map == null) return false;

            ThingDefCountClass[] costs = calibLevel == 1
                ? new[]
                  {
                      new ThingDefCountClass(DefDatabase<ThingDef>.GetNamed("SyntheraArchotechShard"), 5),
                      new ThingDefCountClass(ThingDefOf.Gold,     50),
                      new ThingDefCountClass(ThingDefOf.Plasteel, 100),
                  }
                : new[]
                  {
                      new ThingDefCountClass(DefDatabase<ThingDef>.GetNamed("SyntheraArchotechShard"), 10),
                      new ThingDefCountClass(ThingDefOf.Gold,            100),
                      new ThingDefCountClass(ThingDefOf.ComponentSpacer,   5),
                  };

            foreach (var cost in costs)
            {
                if (cost.thingDef == null) continue;
                if (map.resourceCounter.GetCount(cost.thingDef) < cost.count)
                {
                    Messages.Message(
                        $"Calibration requires {cost.count}× {cost.thingDef.label} but only {map.resourceCounter.GetCount(cost.thingDef)} available.",
                        MessageTypeDefOf.RejectInput, false);
                    return false;
                }
            }

            foreach (var cost in costs)
            {
                if (cost.thingDef == null) continue;
                int remaining = cost.count;
                foreach (Thing t in map.listerThings.ThingsOfDef(cost.thingDef).ToList())
                {
                    if (remaining <= 0) break;
                    int take = System.Math.Min(remaining, t.stackCount);
                    t.SplitOff(take).Destroy();
                    remaining -= take;
                }
            }
            return true;
        }

        // ── Gizmos ────────────────────────────────────────────────────────────

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra()) yield return g;

            if (neomaPawn == null && !inHibernation) yield break;

            // Hibernation restore button
            if (inHibernation)
            {
                var restoreBtn = new Command_Action
                {
                    action       = RestoreCompanion,
                    defaultLabel = "Restore companion",
                    defaultDesc  = ringCalibration >= 2
                        ? "Restore the companion from backup. Temporary hibernation syndrome will be applied."
                        : "Restore the companion from backup.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/Trade", true)
                };
                if (Find.TickManager.TicksGame < respawnTick)
                    restoreBtn.Disable("Restoring backup: " + GenDate.ToStringTicksToPeriod(respawnTick - Find.TickManager.TicksGame));
                yield return restoreBtn;
                yield break;
            }

            // Status gizmo
            string status = neomaPawn.Dead    ? "deceased" :
                            neomaPawn.Spawned ? $"active at {neomaPawn.Position}" :
                                                "recalled";
            string calLabel = ringCalibration == 0 ? "uncalibrated"
                            : ringCalibration == 1 ? "calibration I"
                                                   : "calibration II";

            yield return new Command_Action
            {
                action = () => Messages.Message(
                    $"Neoma Ring — companion: {status} | {calLabel}.",
                    MessageTypeDefOf.NeutralEvent),
                defaultLabel = "Ring status",
                defaultDesc  = $"Companion: {status} | {calLabel}",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/Trade", true)
            };

            // Recall gizmo
            if (neomaPawn.Spawned && !neomaPawn.Dead)
            {
                Pawn wearer = _wearer ?? (parent as Apparel)?.Wearer;
                yield return new Command_Action
                {
                    action = delegate
                    {
                        if (wearer == null || !wearer.Spawned)
                        {
                            Messages.Message("Cannot recall: ring wearer is not present.", MessageTypeDefOf.RejectInput, false);
                            return;
                        }
                        neomaPawn.DeSpawn();
                        GenPlace.TryPlaceThing(neomaPawn, wearer.Position, wearer.Map, ThingPlaceMode.Near);
                        Messages.Message("Companion recalled to the ring wearer's location.", MessageTypeDefOf.NeutralEvent);
                    },
                    defaultLabel = "Recall companion",
                    defaultDesc  = "Teleport the companion back to the ring wearer.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/Trade", true)
                };
            }

            // Calibration gizmos (Phase A keeps the existing 2-level system)
            if (ringCalibration == 0 && ResearchProjectDef.Named("NeomaRingCalibrationI")?.IsFinished == true)
            {
                yield return new Command_Action
                {
                    action = delegate
                    {
                        Find.WindowStack.Add(new Dialog_MessageBox(
                            "Calibration I will consume 5× Archotech Shards, 50× Gold, and 100× Plasteel. " +
                            "The companion can be restored after death instead of the bond being severed. Proceed?",
                            "Calibrate", () =>
                            {
                                if (TryConsumeCalibrationMaterials(1))
                                {
                                    ringCalibration = 1;
                                    UpdateBiolockSeverity(_wearer);
                                    Messages.Message("The Neoma Ring hums with new resonance. Calibration I complete.", MessageTypeDefOf.PositiveEvent);
                                }
                            },
                            "Cancel", null,
                            title: "Calibrate Ring I"));
                    },
                    defaultLabel = "Calibrate ring I",
                    defaultDesc  = "Unlock the ring's backup system. Costs 5× Archotech Shard, 50× Gold, 100× Plasteel.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/Trade", true)
                };
            }

            if (ringCalibration == 1 && ResearchProjectDef.Named("NeomaRingCalibrationII")?.IsFinished == true)
            {
                yield return new Command_Action
                {
                    action = delegate
                    {
                        Find.WindowStack.Add(new Dialog_MessageBox(
                            "Calibration II will consume 10× Archotech Shards, 100× Gold, and 5× Spacer Components. " +
                            "The ring achieves full resonance — complete backup with faster restore. Proceed?",
                            "Calibrate", () =>
                            {
                                if (TryConsumeCalibrationMaterials(2))
                                {
                                    ringCalibration = 2;
                                    UpdateBiolockSeverity(_wearer);
                                    Messages.Message("The Neoma Ring pulses with deep resonance. Calibration II complete.", MessageTypeDefOf.PositiveEvent);
                                }
                            },
                            "Cancel", null,
                            title: "Calibrate Ring II"));
                    },
                    defaultLabel = "Calibrate ring II",
                    defaultDesc  = "Unlock full backup resonance. Costs 10× Archotech Shard, 100× Gold, 5× Spacer Component.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/Trade", true)
                };
            }
        }

        public override string CompInspectStringExtra()
        {
            if (neomaPawn == null && !inHibernation)
                return "Neoma Ring: no companion summoned";

            string calLabel = ringCalibration == 0 ? "uncalibrated"
                            : ringCalibration == 1 ? "calibration I"
                                                   : "calibration II";

            string boundInfo = isBound && _wearer != null
                ? $" | bound to {_wearer.LabelShort}"
                : " | unbound";

            if (inHibernation)
            {
                string eta = Find.TickManager.TicksGame < respawnTick
                    ? $" ({GenDate.ToStringTicksToPeriod(respawnTick - Find.TickManager.TicksGame)})"
                    : " (ready)";
                return $"Ring [{calLabel}]: companion hibernating{eta}{boundInfo}";
            }

            string compStatus = neomaPawn.Spawned ? "deployed" : "recalled";
            return $"Ring [{calLabel}]: companion {compStatus}{boundInfo}";
        }
    }
}
