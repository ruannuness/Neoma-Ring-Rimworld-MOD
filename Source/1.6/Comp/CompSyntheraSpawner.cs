using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SyntheraCore
{
    public class CompSyntheraSpawner : ThingComp
    {
        public Pawn Consciousness;
        public CompProperties_SyntheraSpawner Props => (CompProperties_SyntheraSpawner)props;
        public int RespawnTick = 0;
        public int RecreateCooldownTick = 0;

        private bool InHibernation = false;
        private bool SavedDeep = false;
        private CompPowerTrader compPower;

        private int lastEmergencyRecallTick  = -99999;
        private int lastMaintenancePulseTick = -99999;
        public  bool SignalBurstActive       = false;
        private int signalBurstEndTick       = 0;

        // Transient fields set by CompAuxiliaryModule — not saved, repopulated within 250 ticks of load.
        public float AuxRespawnMultiplier   = 1f;
        public float AuxRecreateMultiplier  = 1f;
        // String-keyed: non-role modules use moduleType.ToString(), role modules use pawnBuffHediff name.
        public HashSet<string> RegisteredAuxTypes = new HashSet<string>();

        // Transient fields set by CompAltarUpgrade — not saved, repopulated on load.
        public int   BonusAuxSlots           = 0;
        public float SpawnIntervalMultiplier = 1f;
        public float HeatOutputMultiplier    = 1f;
        public float PowerMultiplier         = 1f;

        public bool HasPower => compPower != null && compPower.PowerOn;

        private static readonly string[] machineNames =
        {
            "AETHER", "APEX", "ARCANE", "ARGON", "ARIA", "ATLAS", "AXIOM",
            "BINARY", "BOLT", "CACHE", "CASCADE", "CIPHER", "COBALT", "CORTEX",
            "DELTA", "ECHO", "EPOCH", "FLUX", "GENESIS", "GRID", "HELIX",
            "ION", "IRIS", "JUNO", "KERNEL", "KORE", "LAMBDA", "LYNX",
            "MATRIX", "MERCURY", "MESH", "NEXUS", "NODE", "NOVA",
            "ORACLE", "ORBIT", "PHASE", "PIXEL", "PROXY", "PULSAR", "PRISM",
            "QUANTUM", "QUASAR", "RELAY", "RUNE", "SIGMA", "SLATE", "SYNTH",
            "TITAN", "TOKEN", "UMBRA", "VECTOR", "VERTEX", "VOID", "VORTEX",
            "WAVE", "XENON", "ZERO", "ZETA"
        };

        private static string PickMachineName() => machineNames[Rand.Range(0, machineNames.Length)];

        private static readonly Color MikuTeal     = new Color(0.224f, 0.773f, 0.733f, 1f);
        private static readonly Color MikuSkinPale = new Color(0.90f,  0.95f,  0.95f,  1.00f);

        public override void PostExposeData()
        {
            base.PostExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                // Deep-save only when the pawn is alive and held exclusively by this comp.
                // Dead pawns live inside their Corpse (on the map) and must be ref-saved.
                bool storeDeep = Consciousness != null
                    && !Consciousness.Spawned
                    && !Consciousness.Dead
                    && !Find.WorldPawns.Contains(Consciousness)
                    && !Consciousness.InContainerEnclosed
                    && Consciousness.CarriedBy == null;

                SavedDeep = storeDeep;
                Scribe_Values.Look(ref SavedDeep, "SavedDeep");
                if (storeDeep)
                    Scribe_Deep.Look(ref Consciousness, "Consciousness");
                else
                    Scribe_References.Look(ref Consciousness, "Consciousness");
            }
            else
            {
                Scribe_Values.Look(ref SavedDeep, "SavedDeep");
                if (SavedDeep)
                    Scribe_Deep.Look(ref Consciousness, "Consciousness");
                else
                    Scribe_References.Look(ref Consciousness, "Consciousness");
            }
            Scribe_Values.Look(ref RespawnTick, "RespawnTick");
            Scribe_Values.Look(ref RecreateCooldownTick, "RecreateCooldownTick");
            Scribe_Values.Look(ref InHibernation, "InHibernation");
            Scribe_Values.Look(ref lastEmergencyRecallTick,  "lastEmergencyRecallTick",  -99999);
            Scribe_Values.Look(ref lastMaintenancePulseTick, "lastMaintenancePulseTick", -99999);
            Scribe_Values.Look(ref SignalBurstActive,        "SignalBurstActive",         false);
            Scribe_Values.Look(ref signalBurstEndTick,       "signalBurstEndTick",        0);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            compPower = parent.TryGetComp<CompPowerTrader>();
        }

        public override void ReceiveCompSignal(string signal)
        {
            if (signal == "PowerTurnedOff" || signal == "FlickedOff")
            {
                if (Consciousness != null && Consciousness.Spawned)
                    DespawnFormgel(false);
            }
        }

        public override void CompTick()
        {
            base.CompTick();

            if (Consciousness == null) return;

            if (Consciousness.Spawned && !HasPower)
                DespawnFormgel(false);

            // Fallback: runs only when Kill() was not intercepted (e.g. off-map death).
            if (Consciousness.Dead && !InHibernation)
            {
                Corpse corpse    = Consciousness.Corpse;
                IntVec3 deathPos = corpse?.Position ?? parent.Position;

                if (ResurrectionUtility.TryResurrect(Consciousness))
                {
                    var sickDef = DefDatabase<HediffDef>.GetNamed("ResurrectionSickness", false);
                    if (sickDef != null)
                    {
                        var sick = Consciousness.health.hediffSet.GetFirstHediffOfDef(sickDef);
                        if (sick != null) Consciousness.health.RemoveHediff(sick);
                    }
                    if (corpse != null && !corpse.Destroyed)
                        corpse.Destroy(DestroyMode.Vanish);
                }

                EnterHibernation(deathPos);
            }

            // Auto-recall on coherence collapse — checked every NeedInterval (150 ticks).
            if (Consciousness.Spawned && Find.TickManager.TicksGame % 150 == 0)
            {
                var coh = Consciousness.needs?.TryGetNeed<Need_SyntheraCoherence>();
                if (coh != null && coh.CurLevel <= 0f)
                {
                    int failTicks = GetCoherenceFailTicks();
                    Messages.Message(
                        $"{Consciousness.Name.ToStringShort}'s cache depleted. Avatar recalled — restoring cache before re-deployment.",
                        parent, MessageTypeDefOf.NegativeEvent);
                    DespawnFormgel(false);
                    RespawnTick = Find.TickManager.TicksGame + failTicks;
                }
            }

            if (SignalBurstActive && Find.TickManager.TicksGame >= signalBurstEndTick)
                SignalBurstActive = false;
        }

        private int GetCoherenceFailTicks()
        {
            switch (GetBuildingTier())
            {
                case 4: return 833;    // ~20 min
                case 3: return 2500;   // ~1 h
                case 2: return 5000;   // ~2 h
                default: return 10000; // ~4 h (Tier I)
            }
        }

        // Called by Patch_SyntheraInterceptKill (primary) and the CompTick fallback.
        // Pawn must be alive (or just resurrected) when this runs.
        public void EnterHibernation(IntVec3 deathPos)
        {
            if (InHibernation || Consciousness == null) return;
            InHibernation = true;
            RespawnTick = Find.TickManager.TicksGame + (int)(Props.respawnTicks * AuxRespawnMultiplier);

            var coh = Consciousness.needs?.TryGetNeed<Need_SyntheraCoherence>();
            if (coh != null) { coh.CurLevel = 1f; coh.ClearStrainHediff(); }

            Consciousness.equipment?.DropAllEquipment(deathPos);
            Consciousness.apparel?.DropAll(deathPos);
            Consciousness.inventory?.DropAllNearPawn(deathPos);

            if (Consciousness.Spawned)
                Consciousness.DeSpawn();

            Messages.Message(
                $"{Consciousness.Name.ToStringShort} entered hibernation. Backup restoration available in {GenDate.ToStringTicksToPeriod(RespawnTick - Find.TickManager.TicksGame)}.",
                parent, MessageTypeDefOf.NegativeEvent);
        }

        public void GenerateFormgelPawn()
        {
            if (Consciousness != null && Consciousness.Spawned)
                DespawnFormgel(false);

            PawnKindDef pawnKind = DefDatabase<PawnKindDef>.GetNamed(Props.pawnKind, false);
            if (pawnKind == null)
            {
                Log.Error($"SyntheraCore: Could not find PawnKindDef '{Props.pawnKind}' on {parent.def.defName}");
                return;
            }

            PawnGenerationRequest req = new PawnGenerationRequest(
                pawnKind,
                Faction.OfPlayer,
                PawnGenerationContext.NonPlayer,
                -1, true, false, false, false, true, 0,
                allowFood: false,
                allowAddictions: false,
                forceNoIdeo: true,
                forbidAnyTitle: true,
                fixedBiologicalAge: 18,
                fixedChronologicalAge: 18,
                forceNoBackstory: true
            );

            Pawn p = PawnGenerator.GeneratePawn(req);
            p.Name = new NameTriple("", PickMachineName(), "");

            if (p.relations == null) p.relations = new Pawn_RelationsTracker(p);
            if (p.interactions == null) p.interactions = new Pawn_InteractionsTracker(p);

            while (p.story.traits.allTraits.Count > 0)
                p.story.traits.allTraits.RemoveLast();

            Consciousness = p;
            InHibernation = false;
            RespawnTick = 0;

            int tier = GetBuildingTier();
            SetupPawnStatsForTier(tier);
            ConfigureWorkForTier(tier);

            if (ModsConfig.IdeologyActive && Faction.OfPlayer.ideos?.PrimaryIdeo != null)
                Consciousness.ideo.SetIdeo(Faction.OfPlayer.ideos.PrimaryIdeo);

            p.apparel?.DestroyAll();
            SetupFormgel();

            if (Props.pawnKind == "NeomaPawnMiku")
                SetupMiku();
        }

        // Must check longer strings first so TierIV doesn't match TierII.
        // Tier 5 = Miku building (NeomaCoreMiku); treated as tier 3 for work/skills with identity overrides.
        private int GetBuildingTier()
        {
            string defName = parent.def.defName;
            if (defName.Contains("Miku"))    return 5;
            if (defName.Contains("TierIV"))  return 4;
            if (defName.Contains("TierIII")) return 3;
            if (defName.Contains("TierII"))  return 2;
            return 1;
        }

        private void SetupPawnStatsForTier(int tier)
        {
            if (Consciousness.skills == null) return;
            int baseLevel = tier == 5 ? 11 : Mathf.Min(5 + (tier - 1) * 3, 20);
            foreach (SkillRecord skill in Consciousness.skills.skills)
            {
                skill.passion = Passion.Minor;
                skill.levelInt = baseLevel;
            }
            Consciousness.skills.Notify_SkillDisablesChanged();

            // Tier 5 (NeomaCoreMiku) is a Hatsune Miku easter egg — forced name and skill boosts.
            if (tier == 5)
            {
                Consciousness.Name = new NameTriple("", "Hatsune Miku", "");
                Consciousness.skills.GetSkill(SkillDefOf.Social).levelInt   = 16;
                Consciousness.skills.GetSkill(SkillDefOf.Artistic).levelInt = 14;
                var kindTrait = DefDatabase<TraitDef>.GetNamed("Kind", false);
                if (kindTrait != null && !Consciousness.story.traits.HasTrait(kindTrait))
                    Consciousness.story.traits.GainTrait(new Trait(kindTrait));
            }

            Consciousness.story.bodyType = tier switch
            {
                1 => BodyTypeDefOf.Thin,
                2 => BodyTypeDefOf.Male,
                3 => BodyTypeDefOf.Female,
                5 => BodyTypeDefOf.Female,
                _ => BodyTypeDefOf.Hulk
            };
        }

        private void ConfigureWorkForTier(int tier)
        {
            int effectiveTier = tier == 5 ? 3 : tier;
            if (Consciousness.workSettings == null) return;
            Consciousness.workSettings.EnableAndInitialize();

            foreach (WorkTypeDef workType in DefDatabase<WorkTypeDef>.AllDefs)
            {
                int priority = 3;
                if (effectiveTier == 1 && (workType == WorkTypeDefOf.Crafting || workType == WorkTypeDefOf.Hunting || workType == WorkTypeDefOf.Doctor))
                    priority = 0;
                else if (effectiveTier >= 2 && workType == WorkTypeDefOf.Warden)
                    priority = 0;

                Consciousness.workSettings.SetPriority(workType, priority);
            }
        }

        private void SetupMiku()
        {
            if (Consciousness == null) return;

            Consciousness.Name = new NameTriple("", "Hatsune Miku", "");

            // Twin tails — override whatever SetupFormgel picked from HairBasic pool
            HairDef twintails = DefDatabase<HairDef>.GetNamed("MikuTwintails", false);
            if (twintails != null)
                Consciousness.story.hairDef = twintails;

            // Reinforce HAR color channel values: teal hair, pale neutral skin
            Consciousness.story.HairColor         = MikuTeal;
            Consciousness.story.skinColorOverride  = MikuSkinPale;

            Consciousness.Drawer.renderer.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(Consciousness);
        }

        private void SetupFormgel()
        {
            if (Consciousness == null) return;

            var hediffs = Consciousness.health.hediffSet.hediffs.ToList();
            foreach (Hediff h in hediffs)
                Consciousness.health.RemoveHediff(h);

            var consciousnessDef = DefDatabase<HediffDef>.GetNamed("SyntheraConsciousness", false);
            if (consciousnessDef != null)
                Consciousness.health.AddHediff(consciousnessDef);

            if (Consciousness.needs == null)
                Consciousness.needs = new Pawn_NeedsTracker(Consciousness);

            Consciousness.needs.AddOrRemoveNeedsAsAppropriate();

            var allNeeds = Consciousness.needs.AllNeeds.ToList();
            foreach (Need need in allNeeds)
                Consciousness.needs.AllNeeds.Remove(need);

            var coherenceNeed = new Need_SyntheraCoherence(Consciousness);
            coherenceNeed.def = DefDatabase<NeedDef>.GetNamed("NeedSyntheraCoherence", false);
            if (coherenceNeed.def != null)
            {
                coherenceNeed.CurLevel = 1f;
                Consciousness.needs.AllNeeds.Add(coherenceNeed);
            }

            // SyntheraAdult uses a base LifeStageWorker that bypasses HAR's patched
            // HumanlikeAdult worker. headType and hairDef may remain null after generation.
            // Initialize them here, after pawn is fully built, so the renderer never crashes.
            if (Consciousness.story.headType == null)
            {
                var heads = DefDatabase<HeadTypeDef>.AllDefs
                    .Where(h => h.gender == Consciousness.gender || h.gender == Gender.None)
                    .ToList();
                if (heads.Count == 0)
                    heads = DefDatabase<HeadTypeDef>.AllDefs.ToList();
                if (heads.Count > 0)
                    Consciousness.story.headType = heads.RandomElement();
            }
            if (Consciousness.story.hairDef == null)
            {
                var hairs = DefDatabase<HairDef>.AllDefs
                    .Where(h => h.styleTags != null && h.styleTags.Contains("HairBasic"))
                    .ToList();
                if (hairs.Count == 0)
                    hairs = DefDatabase<HairDef>.AllDefs.ToList();
                if (hairs.Count > 0)
                    Consciousness.story.hairDef = hairs.RandomElement();
            }

            // Set style fields accessed by PawnRenderNode_Beard and PawnRenderNode_Tattoo_*.
            // These are normally set by LifeStageWorker_HumanlikeAdult, which we bypass.
            if (Consciousness.style != null)
            {
                if (Consciousness.style.beardDef == null)
                    Consciousness.style.beardDef = DefDatabase<BeardDef>.GetNamed("NoBeard", false)
                        ?? DefDatabase<BeardDef>.AllDefs.FirstOrDefault();

                if (ModsConfig.IdeologyActive)
                {
                    if (Consciousness.style.BodyTattoo == null)
                        Consciousness.style.BodyTattoo = DefDatabase<TattooDef>.GetNamed("NoTattoo_Body", false)
                            ?? DefDatabase<TattooDef>.AllDefs.FirstOrDefault();
                    if (Consciousness.style.FaceTattoo == null)
                        Consciousness.style.FaceTattoo = DefDatabase<TattooDef>.GetNamed("NoTattoo_Face", false)
                            ?? DefDatabase<TattooDef>.AllDefs.FirstOrDefault();
                }
            }

            if (Consciousness.story.Childhood == null)
                Consciousness.story.Childhood = DefDatabase<BackstoryDef>.GetNamed("Synthera_Initialization", false)
                    ?? DefDatabase<BackstoryDef>.AllDefs.FirstOrDefault(b => b.slot == BackstorySlot.Childhood);

            if (Consciousness.story.Adulthood == null)
                Consciousness.story.Adulthood = DefDatabase<BackstoryDef>.GetNamed("Synthera_Deployment", false)
                    ?? DefDatabase<BackstoryDef>.AllDefs.FirstOrDefault(b => b.slot == BackstorySlot.Adulthood);

            Consciousness.Drawer.renderer.SetAllGraphicsDirty();
        }

        public void DespawnFormgel(bool explode, bool goneForGood = false)
        {
            if (Consciousness == null || !Consciousness.Spawned) return;

            // Restore coherence and clear strain on recall.
            var coh = Consciousness.needs?.TryGetNeed<Need_SyntheraCoherence>();
            if (coh != null) { coh.CurLevel = 1f; coh.ClearStrainHediff(); }

            if (Consciousness.carryTracker?.CarriedThing != null)
                Consciousness.carryTracker.TryDropCarriedThing(Consciousness.Position, ThingPlaceMode.Near, out _);

            Consciousness.equipment?.DropAllEquipment(Consciousness.Position);
            Consciousness.apparel?.DropAll(Consciousness.Position);
            Consciousness.inventory?.DropAllNearPawn(Consciousness.Position);

            if (explode)
            {
                RespawnTick = Find.TickManager.TicksGame + (int)(Props.respawnTicks * AuxRespawnMultiplier);
                Map map = Consciousness.Map;
                if (map != null)
                {
                    DamageDef slimeDamage = DefDatabase<DamageDef>.GetNamed("Slime", false) ?? DamageDefOf.Burn;
                    GenExplosion.DoExplosion(
                        Consciousness.Position, map, Props.explosionRadius, slimeDamage, Consciousness,
                        postExplosionSpawnThingDef: ThingDefOf.Filth_Slime, postExplosionSpawnChance: 1f);
                }
            }

            if (Consciousness.Spawned)
                Consciousness.DeSpawn();

            if (goneForGood && !Consciousness.Destroyed)
                Consciousness.Destroy(DestroyMode.Vanish);
        }

        public void SpawnFormgel()
        {
            if (!HasPower)
            {
                Messages.Message("Formgel cannot be assembled while the core has no power.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (Consciousness == null) return;

            if (Consciousness.Destroyed)
                GenerateFormgelPawn();

            // Safety: if somehow the pawn is still dead (e.g. TryResurrect failed in CompTick),
            // attempt resurrection now. On failure, generate a fresh pawn.
            if (Consciousness.Dead)
            {
                if (!ResurrectionUtility.TryResurrect(Consciousness))
                {
                    GenerateFormgelPawn();
                    return;
                }
                InHibernation = false;
            }

            bool wasInHibernation = InHibernation;
            InHibernation = false;

            if (!Consciousness.Spawned)
            {
                GenPlace.TryPlaceThing(Consciousness, parent.Position, parent.Map, ThingPlaceMode.Near);

                if (wasInHibernation)
                {
                    var syndromeDef = DefDatabase<HediffDef>.GetNamed("SyntheraHibernationSyndrome", false);
                    if (syndromeDef != null)
                        Consciousness.health.AddHediff(syndromeDef).Severity = 1f;
                }

                // Ensure coherence need is present (safety net for existing saves).
                if (Consciousness.needs != null && Consciousness.needs.TryGetNeed<Need_SyntheraCoherence>() == null)
                {
                    var safeNeed = new Need_SyntheraCoherence(Consciousness);
                    safeNeed.def = DefDatabase<NeedDef>.GetNamed("NeedSyntheraCoherence", false);
                    if (safeNeed.def != null)
                    {
                        safeNeed.CurLevel = 1f;
                        Consciousness.needs.AllNeeds.Add(safeNeed);
                    }
                }

                // Ensure base consciousness hediff survived the resurrection.
                var conDef = DefDatabase<HediffDef>.GetNamed("SyntheraConsciousness", false);
                if (conDef != null && Consciousness.health.hediffSet.GetFirstHediffOfDef(conDef) == null)
                    Consciousness.health.AddHediff(conDef);

                Consciousness.Drawer.renderer.EnsureGraphicsInitialized();
                PlaySpawnEffects();
                Consciousness.ageTracker?.ResetAgeReversalDemand(Pawn_AgeTracker.AgeReversalReason.ViaTreatment);
            }
        }

        private void PlaySpawnEffects()
        {
            if (Props.spawnSound != null)
                Props.spawnSound.PlayOneShotOnCamera(parent.Map);
            else
                SoundDefOf.PsychicPulseGlobal.PlayOneShotOnCamera(parent.Map);

            FleckMaker.Static(parent.Position, parent.Map, FleckDefOf.PsycastAreaEffect, 5f);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            if (mode == DestroyMode.Deconstruct || mode == DestroyMode.KillFinalize)
                DespawnFormgel(true, true);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra()) yield return g;

            if (Consciousness == null || Consciousness.Destroyed)
            {
                Command_Action createBtn = new Command_Action
                {
                    action = delegate { GenerateFormgelPawn(); SpawnFormgel(); },
                    defaultLabel = "Create avatar",
                    defaultDesc = "Generate a new synthera avatar.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/Spawn", true)
                };
                if (!HasPower)
                    createBtn.Disable("Requires power.");
                yield return createBtn;

                // ── Import Consciousness ──────────────────────────────────────
                var importBtn = new Command_Action
                {
                    action = delegate
                    {
                        Map map = parent.Map;
                        var cores = new System.Collections.Generic.List<Thing>();
                        foreach (Thing t in GenRadial.RadialDistinctThingsAround(
                                     parent.Position, map, 5f, useCenter: true))
                        {
                            var coreComp = t.TryGetComp<CompConsciousnessCore>();
                            if (coreComp?.StoredConsciousness != null)
                                cores.Add(t);
                        }
                        if (cores.Count == 0)
                        {
                            Messages.Message("No memory core within range (5 cells).", MessageTypeDefOf.RejectInput, false);
                            return;
                        }
                        Thing core = cores[0];
                        var comp = core.TryGetComp<CompConsciousnessCore>();
                        Consciousness = comp.StoredConsciousness;
                        comp.StoredConsciousness = null;
                        core.Destroy(DestroyMode.Deconstruct);
                        InHibernation = false;
                        Messages.Message(
                            $"Consciousness of {Consciousness.Name.ToStringShort} imported. Use 'Spawn avatar' to deploy.",
                            parent, MessageTypeDefOf.PositiveEvent);
                    },
                    defaultLabel = "Import consciousness",
                    defaultDesc  = "Absorb a nearby memory core into this altar, restoring its stored Synthera.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/Trade", true)
                };
                yield return importBtn;
            }
            else if (!Consciousness.Spawned)
            {
                string spawnLabel = InHibernation ? "Restore backup" : "Spawn avatar";
                string spawnDesc  = InHibernation
                    ? "Restore the avatar from backup. It will suffer temporary hibernation syndrome."
                    : "Bring the avatar back into the world.";

                Command_Action spawnBtn = new Command_Action
                {
                    action       = delegate { SpawnFormgel(); },
                    defaultLabel = spawnLabel,
                    defaultDesc  = spawnDesc,
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/Spawn", true)
                };

                if (Find.TickManager.TicksGame < RespawnTick)
                {
                    string prefix = InHibernation ? "Backup restoring: " : "Avatar regenerating: ";
                    spawnBtn.Disable(prefix + GenDate.ToStringTicksToPeriod(RespawnTick - Find.TickManager.TicksGame));
                }
                else if (!HasPower)
                    spawnBtn.Disable("Requires power.");

                yield return spawnBtn;

                Command_Action recreateBtn = new Command_Action
                {
                    action = delegate
                    {
                        string name = Consciousness?.Name?.ToStringShort ?? "unknown";
                        Find.WindowStack.Add(new Dialog_ConfirmRecreate(name, () =>
                        {
                            GenerateFormgelPawn();
                            SpawnFormgel();
                            RecreateCooldownTick = Find.TickManager.TicksGame + (int)(Props.recreateCooldownTicks * AuxRecreateMultiplier);
                        }));
                    },
                    defaultLabel = "Recreate avatar",
                    defaultDesc  = "Discard the current avatar and generate a completely new one. Long cooldown.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/Trade", true)
                };

                if (Find.TickManager.TicksGame < RecreateCooldownTick)
                    recreateBtn.Disable("Recreate on cooldown: " + GenDate.ToStringTicksToPeriod(RecreateCooldownTick - Find.TickManager.TicksGame));
                else if (!HasPower)
                    recreateBtn.Disable("Requires power.");

                yield return recreateBtn;

                // ── Export Consciousness ──────────────────────────────────────
                if (!InHibernation)
                {
                    yield return new Command_Action
                    {
                        action = delegate
                        {
                            Thing core = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SyntheraMemoryCore"));
                            var coreComp = core.TryGetComp<CompConsciousnessCore>();
                            coreComp.StoredConsciousness = Consciousness;
                            Consciousness = null;
                            GenPlace.TryPlaceThing(core, parent.Position, parent.Map, ThingPlaceMode.Near);
                            Messages.Message(
                                $"Consciousness exported to a memory core. Pick it up and bring it to another altar.",
                                parent, MessageTypeDefOf.NeutralEvent);
                        },
                        defaultLabel = "Export consciousness",
                        defaultDesc  = "Package the avatar's consciousness into a portable memory core. The avatar will be unavailable until imported into an altar.",
                        icon = ContentFinder<Texture2D>.Get("UI/Commands/Trade", true)
                    };
                }
            }
            else
            {
                yield return new Command_Action
                {
                    action = delegate
                    {
                        DespawnFormgel(false);
                        RespawnTick = Find.TickManager.TicksGame + (int)(Props.spawnIntervalDays * 60000f * AuxRespawnMultiplier * SpawnIntervalMultiplier);
                    },
                    defaultLabel = "Despawn avatar",
                    defaultDesc  = "Return the avatar to the core. Resets system stress. Available again after cooldown.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/Despawn", true)
                };

                yield return new Command_Action
                {
                    action = delegate { Find.WindowStack.Add(new Dialog_NameAvatar(Consciousness)); },
                    defaultLabel = "Rename avatar",
                    defaultDesc  = "Set a custom name for this avatar.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/RenameZone", true)
                };

                // ── Active functions ──────────────────────────────────────────
                int curTick   = Find.TickManager.TicksGame;
                Map altarMap  = parent.Map;
                int altarTier = GetBuildingTier();

                var recallBtn = new Command_Action
                {
                    action = delegate
                    {
                        if (Consciousness.Spawned)
                            Consciousness.DeSpawn();
                        GenPlace.TryPlaceThing(Consciousness, parent.Position, altarMap, ThingPlaceMode.Near);
                        lastEmergencyRecallTick = Find.TickManager.TicksGame;
                        Messages.Message(
                            $"{Consciousness.Name.ToStringShort} recalled to the altar.",
                            parent, MessageTypeDefOf.NeutralEvent);
                    },
                    defaultLabel = "Emergency recall",
                    defaultDesc  = "Instantly teleport the avatar back to the altar. 1-day cooldown.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/Recall", true)
                };
                if (curTick < lastEmergencyRecallTick + Props.emergencyRecallCooldownTicks)
                    recallBtn.Disable("Recall on cooldown: " + GenDate.ToStringTicksToPeriod(lastEmergencyRecallTick + Props.emergencyRecallCooldownTicks - curTick));
                yield return recallBtn;

                if (altarTier >= 2)
                {
                    var pulseBtn = new Command_Action
                    {
                        action = delegate
                        {
                            if (altarMap.resourceCounter.GetCount(ThingDefOf.Steel) < 5)
                            {
                                Messages.Message("Maintenance Pulse requires 5× Steel.", MessageTypeDefOf.RejectInput, false);
                                return;
                            }
                            int rem = 5;
                            foreach (Thing t in altarMap.listerThings.ThingsOfDef(ThingDefOf.Steel).ToList())
                            {
                                if (rem <= 0) break;
                                int take = System.Math.Min(rem, t.stackCount);
                                t.SplitOff(take).Destroy();
                                rem -= take;
                            }
                            var cohPulse = Consciousness.needs?.TryGetNeed<Need_SyntheraCoherence>();
                            cohPulse?.Recharge(0.25f);
                            lastMaintenancePulseTick = Find.TickManager.TicksGame;
                            Messages.Message(
                                $"Maintenance pulse applied to {Consciousness.Name.ToStringShort}. Cache restored.",
                                parent, MessageTypeDefOf.PositiveEvent);
                        },
                        defaultLabel = "Maintenance pulse",
                        defaultDesc  = "Perform a maintenance cycle, reducing system stress by 0.25. Costs 5× Steel. 3-day cooldown.",
                        icon = ContentFinder<Texture2D>.Get("UI/Commands/Trade", true)
                    };
                    if (curTick < lastMaintenancePulseTick + Props.maintenancePulseCooldownTicks)
                        pulseBtn.Disable("Pulse on cooldown: " + GenDate.ToStringTicksToPeriod(lastMaintenancePulseTick + Props.maintenancePulseCooldownTicks - curTick));
                    yield return pulseBtn;
                }

                if (altarTier >= 3)
                {
                    var burstBtn = new Command_Action
                    {
                        action = delegate
                        {
                            SignalBurstActive  = true;
                            signalBurstEndTick = Find.TickManager.TicksGame + 60000;
                            Messages.Message(
                                "Signal burst active: linked aux module detection radii doubled for 24 hours.",
                                parent, MessageTypeDefOf.PositiveEvent);
                        },
                        defaultLabel = "Signal burst",
                        defaultDesc  = "Doubles the detection radius of all linked aux modules for 24 hours.",
                        icon = ContentFinder<Texture2D>.Get("UI/Commands/Trade", true)
                    };
                    if (SignalBurstActive)
                        burstBtn.Disable("Signal burst active: " + GenDate.ToStringTicksToPeriod(signalBurstEndTick - curTick));
                    yield return burstBtn;
                }
            }
        }

        private string FormatModuleKey(string key)
        {
            if (key.StartsWith("SyntheraRole"))
            {
                var def = DefDatabase<HediffDef>.GetNamed(key, false);
                return def != null ? def.label.CapitalizeFirst() : key.Substring("SyntheraRole".Length);
            }
            switch (key)
            {
                case "CombatAmplifier":     return "Combat amplifier";
                case "CoreOptimizer":       return "Core optimizer";
                case "WorkUnlocker":        return "Work unlocker";
                case "ArmorMatrix":         return "Armor matrix";
                case "OverclockCore":       return "Overclock core";
                case "VersatilityProtocol": return "Versatility protocol";
                default:                    return key;
            }
        }

        private string BuildModulesSummary()
        {
            if (RegisteredAuxTypes.Count == 0)
                return "No modules connected.";
            return string.Join(", ", RegisteredAuxTypes.Select(FormatModuleKey));
        }

        public override string CompInspectStringExtra()
        {
            if (Consciousness == null)
                return $"Core: {parent.def.label}";

            if (InHibernation)
                return $"Avatar: {Consciousness.Name.ToStringShort} [HIBERNATING]";

            if (Consciousness.Spawned)
            {
                string cacheStr = "Full";
                var coh = Consciousness.needs?.TryGetNeed<Need_SyntheraCoherence>();
                if (coh != null)
                    cacheStr = coh.CurLevel > 0.5f  ? $"Cache: {(int)(coh.CurLevel * 100)}%"
                             : coh.CurLevel > 0.25f ? $"Cache: {(int)(coh.CurLevel * 100)}% [unstable]"
                             : $"Cache: {(int)(coh.CurLevel * 100)}% [CRITICAL]";
                string modLine = RegisteredAuxTypes.Count > 0
                    ? $"\nModules ({RegisteredAuxTypes.Count}/{Props.maxAuxModules + BonusAuxSlots}): {BuildModulesSummary()}"
                    : "";
                return $"Avatar: {Consciousness.Name.ToStringShort} | {cacheStr}{modLine}";
            }

            return $"Avatar: {Consciousness.Name.ToStringShort} [offline]";
        }
    }

    public class Dialog_ConfirmRecreate : Window
    {
        private readonly string avatarName;
        private readonly System.Action onConfirm;

        public Dialog_ConfirmRecreate(string avatarName, System.Action onConfirm)
        {
            this.avatarName = avatarName;
            this.onConfirm  = onConfirm;
            this.doCloseButton       = false;
            this.absorbInputAroundWindow = true;
            this.forcePause          = true;
        }

        public override Vector2 InitialSize => new Vector2(400f, 160f);

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard ls = new Listing_Standard();
            ls.Begin(inRect);
            ls.Label($"The avatar {avatarName} will be permanently deleted and replaced by a new one.");
            ls.Gap(12f);
            if (ls.ButtonText("Confirm"))
            {
                onConfirm();
                Close();
            }
            if (ls.ButtonText("Cancel"))
                Close();
            ls.End();
        }
    }

    public class Dialog_NameAvatar : Window
    {
        private readonly Pawn pawn;
        private string nameBuffer;

        public Dialog_NameAvatar(Pawn pawn)
        {
            this.pawn = pawn;
            this.nameBuffer = pawn.Name.ToStringShort;
            this.doCloseButton = true;
            this.absorbInputAroundWindow = true;
            this.forcePause = true;
        }

        public override Vector2 InitialSize => new Vector2(400f, 180f);

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard ls = new Listing_Standard();
            ls.Begin(inRect);
            ls.Label("Avatar name:");
            nameBuffer = ls.TextEntry(nameBuffer);
            if (ls.ButtonText("Confirm") && !nameBuffer.NullOrEmpty())
            {
                pawn.Name = new NameTriple("", nameBuffer.Trim(), "");
                Close();
            }
            ls.End();
        }
    }
}
