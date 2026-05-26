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
        private int  ringLevel    = 0; // 0–3; advances via Apex Crystal absorption
        private bool inHibernation = false;
        private int  respawnTick   = 0;
        private int  emergencyRecallCooldownTick = 0;

        private Pawn _wearer;
        private bool isBound = false;

        // Maps ringLevel 0–3 to NeomaBiolock hediff severity.
        // Level 0 = dormant fragment (0.15)
        // Level 1 = neural bridge    (0.55) — Apex I
        // Level 2 = crystallized bond(0.85) — Apex II
        // Level 3 = soul fusion      (0.97) — Apex III
        private static readonly float[] BiolockSeverityByLevel = { 0.15f, 0.55f, 0.85f, 0.97f };

        public Pawn NeomaPawn => neomaPawn;

        // True when the ring is bound to a different living pawn — used by the equip-block patch.
        public bool IsBoundToOther(Pawn pawn) =>
            isBound && _wearer != null && !_wearer.Dead && _wearer != pawn;

        // Called by Recipe_AbsorbApexCrystal after a successful crystal absorption on Neoma.
        public void AdvanceLevel()
        {
            if (ringLevel >= 3) return;
            ringLevel++;
            UpdateBiolockSeverity(_wearer);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref neomaPawn,  "neomaPawn");
            Scribe_References.Look(ref _wearer,    "_wearer");
            Scribe_Values.Look(ref ringLevel,                   "ringLevel",                   0);
            Scribe_Values.Look(ref inHibernation,               "inHibernation",               false);
            Scribe_Values.Look(ref respawnTick,                 "respawnTick",                 0);
            Scribe_Values.Look(ref emergencyRecallCooldownTick, "emergencyRecallCooldownTick", 0);
            Scribe_Values.Look(ref isBound,                     "isBound",                     false);
        }

        public override void CompTick()
        {
            base.CompTick();

            // Release BioLock if the wearer has died.
            if (isBound && _wearer != null && _wearer.Dead)
                UnbindFromWearer();

            // Handle companion death.
            if (neomaPawn == null || !neomaPawn.Dead || inHibernation) return;

            Corpse  corpse   = neomaPawn.Corpse;
            IntVec3 deathPos = corpse?.Position
                            ?? (parent as Apparel)?.Wearer?.Position
                            ?? IntVec3.Invalid;

            if (ringLevel == 0)
            {
                DropAndDestroyCorpse(deathPos, corpse);
                Messages.Message(
                    "The Neoma Ring's backup system is dormant. The companion is lost — absorb an Apex Crystal to unlock the backup channel.",
                    MessageTypeDefOf.NegativeEvent, false);
                neomaPawn = null;
                return;
            }

            // Level 1–3: enter hibernation with the appropriate delay.
            inHibernation = true;
            int delay   = Props.GetRespawnTicks(ringLevel);
            respawnTick = Find.TickManager.TicksGame + delay;

            if (ResurrectionUtility.TryResurrect(neomaPawn))
            {
                var sickDef = DefDatabase<HediffDef>.GetNamed("ResurrectionSickness", false);
                if (sickDef != null)
                {
                    var sick = neomaPawn.health.hediffSet.GetFirstHediffOfDef(sickDef);
                    if (sick != null) neomaPawn.health.RemoveHediff(sick);
                }
                DropAndDestroyCorpse(deathPos, corpse);
                if (neomaPawn.Spawned) neomaPawn.DeSpawn();
            }

            Messages.Message(
                $"The Neoma Ring's companion has entered hibernation. Restoration available in {GenDate.ToStringTicksToPeriod(delay)}.",
                MessageTypeDefOf.NegativeEvent, false);
        }

        private static void DropAndDestroyCorpse(IntVec3 pos, Corpse corpse)
        {
            if (pos.IsValid && corpse?.InnerPawn is Pawn p)
            {
                p.apparel?.DropAll(pos);
                p.inventory?.DropAllNearPawn(pos);
            }
            if (corpse != null && !corpse.Destroyed)
                corpse.Destroy(DestroyMode.Vanish);
        }

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);

            if (isBound && _wearer != null && _wearer.Dead)
                UnbindFromWearer();

            HediffDef biolockDef = HediffDef.Named("NeomaBiolock");

            _wearer = pawn;
            isBound = true;

            if (!pawn.health.hediffSet.HasHediff(biolockDef))
                pawn.health.AddHediff(biolockDef);

            UpdateBiolockSeverity(pawn);

            // Re-deploy already-existing live companion.
            if (neomaPawn != null && !neomaPawn.Dead && !neomaPawn.Destroyed
                && !neomaPawn.Spawned && !inHibernation)
            {
                GenPlace.TryPlaceThing(neomaPawn, pawn.Position, pawn.Map, ThingPlaceMode.Near);
            }
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            if (neomaPawn != null && neomaPawn.Spawned)
                neomaPawn.DeSpawn();
        }

        // ── BioLock ──────────────────────────────────────────────────────────

        private void UpdateBiolockSeverity(Pawn wearer)
        {
            if (wearer == null || wearer.Dead) return;
            HediffDef biolockDef = HediffDef.Named("NeomaBiolock");
            var biolock = wearer.health.hediffSet.GetFirstHediffOfDef(biolockDef);
            if (biolock == null) return;
            int idx = System.Math.Min(ringLevel, BiolockSeverityByLevel.Length - 1);
            biolock.Severity = BiolockSeverityByLevel[idx];
        }

        private void UnbindFromWearer()
        {
            HediffDef biolockDef = HediffDef.Named("NeomaBiolock");
            var biolock = _wearer?.health.hediffSet.GetFirstHediffOfDef(biolockDef);
            if (biolock != null) _wearer.health.RemoveHediff(biolock);

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

            int ancientAge = Rand.Range(6000, 12000);

            PawnGenerationRequest req = new PawnGenerationRequest(
                neomaKind, Faction.OfPlayer, PawnGenerationContext.NonPlayer,
                -1, true, false, false, false, true, 0,
                allowFood: false, allowAddictions: false,
                forceNoIdeo: true, forbidAnyTitle: true,
                fixedBiologicalAge: 25, fixedChronologicalAge: ancientAge,
                forceNoBackstory: true);

            Pawn neoma = PawnGenerator.GeneratePawn(req);
            neoma.gender = Gender.Female;
            neoma.Name = new NameTriple("Neoma", "", "");

            if (neoma.relations    == null) neoma.relations    = new Pawn_RelationsTracker(neoma);
            if (neoma.interactions == null) neoma.interactions = new Pawn_InteractionsTracker(neoma);

            while (neoma.story.traits.allTraits.Count > 0)
                neoma.story.traits.allTraits.RemoveLast();

            SyntheraUtils.SetupNeomaCompanion(neoma);

            // Ancient AI — extremely capable across all disciplines.
            if (neoma.skills != null)
            {
                foreach (var skill in neoma.skills.skills)
                { skill.Level = Rand.RangeInclusive(14, 18); skill.xpSinceLastLevel = 0; }

                static void Max(Pawn p, SkillDef d)
                { var s = p.skills?.GetSkill(d); if (s != null) { s.Level = 20; s.xpSinceLastLevel = 0; } }
                Max(neoma, SkillDefOf.Shooting);
                Max(neoma, SkillDefOf.Intellectual);
            }

            // OP traits befitting a transcendent AI construct.
            static void Trait(Pawn p, string defName, int degree = 0)
            {
                var def = DefDatabase<TraitDef>.GetNamed(defName, false);
                if (def == null || p.story?.traits == null) return;
                if (!p.story.traits.HasTrait(def))
                    p.story.traits.GainTrait(new Trait(def, degree));
            }
            Trait(neoma, "Industrious");           // works 25% faster
            Trait(neoma, "ShootingAccuracy",  2);  // Crack Shot
            Trait(neoma, "NaturalMood",       2);  // Sanguine

            GenPlace.TryPlaceThing(neoma, wearer.Position, wearer.Map, ThingPlaceMode.Near);
            neomaPawn = neoma;

            SoundDefOf.PsychicPulseGlobal.PlayOneShotOnCamera(wearer.Map);
            FleckMaker.Static(wearer.Position, wearer.Map, FleckDefOf.PsycastAreaEffect, 5f);

            // Unlock hidden ring research tab on first ever summon.
            // NeomaRing_Unlock is the invisible trigger that makes NeomaRing_Gate visible.
            // Both are completed here so the 4 research nodes appear immediately.
            var unlock = DefDatabase<ResearchProjectDef>.GetNamed("NeomaRing_Unlock", false);
            if (unlock != null && !unlock.IsFinished)
                Find.ResearchManager.FinishProject(unlock, doCompletionDialog: false);

            var gate = DefDatabase<ResearchProjectDef>.GetNamed("NeomaRing_Gate", false);
            if (gate != null && !gate.IsFinished)
            {
                Find.ResearchManager.FinishProject(gate, doCompletionDialog: false);
                Messages.Message("SyntheraCore_RingResearchUnlocked".Translate(), MessageTypeDefOf.SilentInput);
            }

            Messages.Message("The Neoma Ring has summoned its bound companion.", MessageTypeDefOf.PositiveEvent);
        }

        // ── Restore from hibernation ──────────────────────────────────────────

        private void RestoreCompanion()
        {
            Pawn wearer = _wearer ?? (parent as Apparel)?.Wearer;
            if (wearer == null || !wearer.Spawned) return;

            inHibernation = false;
            GenPlace.TryPlaceThing(neomaPawn, wearer.Position, wearer.Map, ThingPlaceMode.Near);

            var syndromeDef = DefDatabase<HediffDef>.GetNamed("SyntheraHibernationSyndrome", false);
            if (syndromeDef != null)
                neomaPawn.health.AddHediff(syndromeDef).Severity = 1f;

            var conDef = DefDatabase<HediffDef>.GetNamed("SyntheraConsciousness", false);
            if (conDef != null && neomaPawn.health.hediffSet.GetFirstHediffOfDef(conDef) == null)
                neomaPawn.health.AddHediff(conDef);

            SoundDefOf.PsychicPulseGlobal.PlayOneShotOnCamera(wearer.Map);
            FleckMaker.Static(wearer.Position, wearer.Map, FleckDefOf.PsycastAreaEffect, 3f);
            Messages.Message("The Neoma Ring's companion has been restored from backup.", MessageTypeDefOf.PositiveEvent);
        }

        // ── Emergency Recall (Apex I+, 6 h cooldown = 15 000 ticks) ─────────

        private void DoEmergencyRecall()
        {
            Pawn wearer = _wearer ?? (parent as Apparel)?.Wearer;
            if (wearer == null || !wearer.Spawned) return;

            if (inHibernation)
            {
                respawnTick = Find.TickManager.TicksGame;
                RestoreCompanion();
            }
            else if (neomaPawn != null && !neomaPawn.Dead)
            {
                if (neomaPawn.Spawned) neomaPawn.DeSpawn();
                GenPlace.TryPlaceThing(neomaPawn, wearer.Position, wearer.Map, ThingPlaceMode.Near);
                Messages.Message("Emergency recall: companion teleported to ring wearer.", MessageTypeDefOf.NeutralEvent);
            }

            emergencyRecallCooldownTick = Find.TickManager.TicksGame + 15000;
        }

        // ── Display helpers ───────────────────────────────────────────────────

        private static string LevelName(int level)
        {
            if (level == 1) return "Apex I";
            if (level == 2) return "Apex II";
            if (level == 3) return "Apex III — Transcendent";
            return "Dormant";
        }

        private static string BiolockStageName(int level)
        {
            if (level == 1) return "neural bridge";
            if (level == 2) return "crystallized bond";
            if (level == 3) return "soul fusion";
            return "dormant fragment";
        }

        // ── Gizmos ────────────────────────────────────────────────────────────

        // CompGetGizmosExtra is intentionally empty — gizmos are injected via Patch_Pawn_NeomaRingGizmos
        // so they appear exactly once on the wearer's bar (apparel comps are not included by vanilla).
        public override IEnumerable<Gizmo> CompGetGizmosExtra() =>
            base.CompGetGizmosExtra();

        // Called by Patch_Pawn_NeomaRingGizmos when the wearer is selected.
        public IEnumerable<Gizmo> GetRingGizmos()
        {
            // ── Invoke gizmo (no companion yet, or lost at level 0) ──
            if (neomaPawn == null && !inHibernation)
            {
                Pawn wearer = _wearer ?? (parent as Apparel)?.Wearer;
                if (wearer != null && wearer.Spawned)
                {
                    yield return new Command_Action
                    {
                        action       = () => SpawnNeoma(wearer),
                        defaultLabel = "Invoke Neoma",
                        defaultDesc  = "Summon the Neoma AI companion bound to this ring.",
                        icon         = ContentFinder<Texture2D>.Get("UI/Commands/Spawn", true)
                    };
                }
                yield break;
            }

            // ── Restore button (shown only while hibernating) ──
            if (inHibernation)
            {
                var restoreBtn = new Command_Action
                {
                    action       = RestoreCompanion,
                    defaultLabel = "Restore companion",
                    defaultDesc  = "Restore the companion from backup. Temporary hibernation syndrome will be applied.",
                    icon         = ContentFinder<Texture2D>.Get("UI/Commands/Spawn", true)
                };
                if (Find.TickManager.TicksGame < respawnTick)
                    restoreBtn.Disable("Restoring backup: " +
                        GenDate.ToStringTicksToPeriod(respawnTick - Find.TickManager.TicksGame));
                yield return restoreBtn;
                yield break;
            }

            // ── Status gizmo ──
            string compStatus = neomaPawn == null    ? "lost"
                              : neomaPawn.Dead        ? "deceased"
                              : neomaPawn.Spawned     ? $"active at {neomaPawn.Position}"
                                                      : "recalled";
            yield return new Command_Action
            {
                action = () => Messages.Message(
                    $"Neoma Ring — level: {LevelName(ringLevel)} | BioLock: {BiolockStageName(ringLevel)} | companion: {compStatus}",
                    MessageTypeDefOf.NeutralEvent),
                defaultLabel = $"Ring status [{LevelName(ringLevel)}]",
                defaultDesc  = $"Companion: {compStatus} | BioLock: {BiolockStageName(ringLevel)}",
                icon         = ContentFinder<Texture2D>.Get("UI/Commands/Trade", true)
            };

            if (neomaPawn == null || neomaPawn.Dead) yield break;

            // ── Recall (normal) ──
            if (neomaPawn.Spawned)
            {
                Pawn wearer = _wearer ?? (parent as Apparel)?.Wearer;
                yield return new Command_Action
                {
                    action = delegate
                    {
                        if (wearer == null || !wearer.Spawned)
                        {
                            Messages.Message("Cannot recall: ring wearer is not present.",
                                MessageTypeDefOf.RejectInput, false);
                            return;
                        }
                        neomaPawn.DeSpawn();
                        GenPlace.TryPlaceThing(neomaPawn, wearer.Position, wearer.Map, ThingPlaceMode.Near);
                        Messages.Message("Companion recalled to the ring wearer's location.",
                            MessageTypeDefOf.NeutralEvent);
                    },
                    defaultLabel = "Recall companion",
                    defaultDesc  = "Teleport the companion back to the ring wearer.",
                    icon         = ContentFinder<Texture2D>.Get("UI/Commands/Despawn", true)
                };
            }

            // ── Emergency Recall (Apex I+) ──
            if (ringLevel >= 1)
            {
                var emergencyBtn = new Command_Action
                {
                    action       = DoEmergencyRecall,
                    defaultLabel = "Emergency recall",
                    defaultDesc  = "Force-recall the companion instantly, even from hibernation. 6-hour cooldown.",
                    icon         = ContentFinder<Texture2D>.Get("UI/Commands/Recall", true)
                };
                if (Find.TickManager.TicksGame < emergencyRecallCooldownTick)
                    emergencyBtn.Disable("Emergency recall cooling down: " +
                        GenDate.ToStringTicksToPeriod(emergencyRecallCooldownTick - Find.TickManager.TicksGame));
                yield return emergencyBtn;
            }
        }

        public override string CompInspectStringExtra()
        {
            if (neomaPawn == null && !inHibernation)
                return "Neoma Ring: no companion summoned";

            string boundInfo = isBound && _wearer != null
                ? $" | bound to {_wearer.LabelShort}"
                : " | unbound";

            if (inHibernation)
            {
                string eta = Find.TickManager.TicksGame < respawnTick
                    ? $" ({GenDate.ToStringTicksToPeriod(respawnTick - Find.TickManager.TicksGame)})"
                    : " (ready)";
                return $"Ring [{LevelName(ringLevel)}]: companion hibernating{eta}{boundInfo}";
            }

            string status = neomaPawn != null && neomaPawn.Spawned ? "deployed" : "recalled";
            return $"Ring [{LevelName(ringLevel)}]: companion {status}{boundInfo}";
        }
    }
}
