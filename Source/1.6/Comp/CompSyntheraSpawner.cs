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

            // Every NeedInterval: push altar multiplier to the need, then check for overflow.
            if (Consciousness.Spawned && Find.TickManager.TicksGame % 150 == 0)
            {
                var coh = Consciousness.needs?.TryGetNeed<Need_SyntheraCoherence>();
                if (coh != null)
                {
                    coh.AltarMultiplier = CalcAltarMultiplier();

                    if (coh.CurLevel >= 1f)
                    {
                        int failTicks = GetCoherenceFailTicks();
                        Messages.Message(
                            $"{Consciousness.Name.ToStringShort}'s cache overflowed. Projection collapsed — recovery cycle required before re-deployment.",
                            parent, MessageTypeDefOf.NegativeEvent);
                        DespawnFormgel(false);
                        RespawnTick = Find.TickManager.TicksGame + failTicks;
                    }
                }
            }

            if (SignalBurstActive && Find.TickManager.TicksGame >= signalBurstEndTick)
                SignalBurstActive = false;
        }

        // Altar tier slows cache fill; mismatch between pawn tier and altar tier accelerates it.
        private float CalcAltarMultiplier()
        {
            float altarBase = GetBuildingTier() switch
            {
                5 => 0.35f, // Miku building
                4 => 0.35f,
                3 => 0.50f,
                2 => 0.75f,
                _ => 1.00f  // Tier I
            };

            int gap = GetPawnTier() - GetBuildingTier();
            if (gap <= 0) return altarBase;

            float bottleneck = gap switch
            {
                1 => 2f,
                2 => 4f,
                _ => 8f
            };
            return altarBase * bottleneck;
        }

        private int GetPawnTier()
        {
            string kind = Consciousness?.kindDef?.defName ?? "";
            if (kind.Contains("Miku"))    return 4;
            if (kind.Contains("TierIV")) return 4;
            if (kind.Contains("TierIII")) return 3;
            if (kind.Contains("TierII")) return 2;
            return 1;
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
            if (coh != null) { coh.CurLevel = 0f; coh.ClearStrainHediff(); }

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

            if (tier == 5)
            {
                // Hatsune Miku — Virtual Diva: performer profile.
                foreach (SkillRecord s in Consciousness.skills.skills)
                { s.levelInt = 2; s.passion = Passion.None; }

                void Set(SkillDef def, int level, Passion passion)
                {
                    var s = Consciousness.skills.GetSkill(def);
                    if (s != null) { s.levelInt = level; s.passion = passion; }
                }

                // Fixos: identidade da personagem
                Set(SkillDefOf.Artistic,    16, Passion.Major);
                Set(SkillDefOf.Social,      14, Passion.Major);
                Set(SkillDefOf.Intellectual, 10, Passion.Minor);

                // Aleatórios: variam a cada geração
                Set(SkillDefOf.Cooking,      Rand.Range(5, 10), Passion.Minor);
                Set(SkillDefOf.Animals,      Rand.Range(5, 10), Passion.Minor);
                Set(SkillDefOf.Medicine,     Rand.Range(4,  8), Passion.None);
                Set(SkillDefOf.Plants,       Rand.Range(3,  7), Passion.None);
                Set(SkillDefOf.Crafting,     Rand.Range(3,  6), Passion.None);
                Set(SkillDefOf.Shooting,     Rand.Range(2,  6), Passion.None);
                Set(SkillDefOf.Construction, Rand.Range(2,  5), Passion.None);
                Set(SkillDefOf.Melee,        Rand.Range(1,  5), Passion.None);
                Set(SkillDefOf.Mining,       Rand.Range(0,  4), Passion.None);

                Consciousness.Name = new NameTriple("", "Hatsune Miku", "");

                void AddTrait(string defName)
                {
                    var def = DefDatabase<TraitDef>.GetNamed(defName, false);
                    if (def != null && !Consciousness.story.traits.HasTrait(def))
                        Consciousness.story.traits.GainTrait(new Trait(def));
                }
                AddTrait("Kind");
                AddTrait("Optimist");
                AddTrait("Industrious");
            }
            else
            {
                int minLevel = Mathf.Min(2 + (tier - 1) * 3, 17);
                int maxLevel = Mathf.Min(8 + (tier - 1) * 3, 20);

                // Base skill levels — all start at None passion
                foreach (SkillRecord skill in Consciousness.skills.skills)
                {
                    skill.levelInt = Rand.Range(minLevel, maxLevel + 1);
                    skill.passion  = Passion.None;
                }

                // Specialty: 1-2 random skills boosted above tier ceiling — defines the synthera's role
                int specialtyCount = Rand.Range(1, 3);
                foreach (SkillRecord s in Consciousness.skills.skills.InRandomOrder().Take(specialtyCount))
                    s.levelInt = Mathf.Min(s.levelInt + Rand.Range(3, 7), 20);

                // Passion budget — distributed across random skills (Major costs 2, Minor costs 1)
                int passionBudget = tier switch { 1 => 2, 2 => 3, 3 => 4, _ => 5 };
                foreach (SkillRecord s in Consciousness.skills.skills.InRandomOrder())
                {
                    if (passionBudget <= 0) break;
                    if (passionBudget >= 2 && Rand.Value < 0.35f)
                    { s.passion = Passion.Major; passionBudget -= 2; }
                    else
                    { s.passion = Passion.Minor; passionBudget -= 1; }
                }

                AssignRandomTraits(tier);
            }

            Consciousness.skills.Notify_SkillDisablesChanged();

            Consciousness.story.bodyType = tier switch
            {
                1 => BodyTypeDefOf.Thin,
                2 => BodyTypeDefOf.Male,
                3 => BodyTypeDefOf.Female,
                5 => BodyTypeDefOf.Female,
                _ => BodyTypeDefOf.Hulk
            };
        }

        private static readonly string[] PositiveTraits =
        {
            "Industrious", "Kind", "Optimist", "Tough", "Nimble",
            "FastLearner", "Transhumanist", "Careful", "QuickSleeper"
        };

        private static readonly string[] NegativeTraits =
        {
            "Nervous", "Volatile", "Abrasive", "Pessimist",
            "Wimp", "Pyromaniac", "Depressive", "Jealous"
        };

        private void AssignRandomTraits(int tier)
        {
            // Tier I: 1 trait. Tier II: 1–2. Tier III: 2. Tier IV: 2–3.
            int count = tier switch
            {
                1 => 1,
                2 => Rand.Range(1, 3),
                3 => 2,
                _ => Rand.Range(2, 4)
            };

            // Negative chance per slot: Tier I=65%, II=45%, III=25%, IV=10%
            float negativeChance = tier switch
            {
                1 => 0.65f,
                2 => 0.45f,
                3 => 0.25f,
                _ => 0.10f
            };

            var story = Consciousness.story.traits;
            var pool = PositiveTraits.ToList().InRandomOrder().ToList();
            var negPool = NegativeTraits.ToList().InRandomOrder().ToList();
            int negIdx = 0, posIdx = 0;

            for (int i = 0; i < count; i++)
            {
                bool pickNeg = Rand.Value < negativeChance;
                string defName = null;

                if (pickNeg && negIdx < negPool.Count)
                    defName = negPool[negIdx++];
                else if (posIdx < pool.Count)
                    defName = pool[posIdx++];

                if (defName == null) break;

                var def = DefDatabase<TraitDef>.GetNamed(defName, false);
                if (def == null || story.HasTrait(def)) continue;
                if (story.allTraits.Any(t => t.def.ConflictsWith(def))) continue;

                story.GainTrait(new Trait(def));
            }
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

            // Lower tiers have random hardware limitations — 1 extra work type disabled
            if (effectiveTier <= 2)
            {
                var blockable = DefDatabase<WorkTypeDef>.AllDefs
                    .Where(w => Consciousness.workSettings.GetPriority(w) > 0
                             && w != WorkTypeDefOf.Hauling
                             && w != WorkTypeDefOf.Cleaning)
                    .ToList();
                if (blockable.Count > 0)
                    Consciousness.workSettings.SetPriority(blockable.RandomElement(), 0);
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

            // Strip biological needs; keep Joy, Comfort, and Social (conscious-AI appropriate).
            var toStrip = Consciousness.needs.AllNeeds
                .Where(n => !(n is Need_SyntheraCoherence)
                         && n.def?.defName != "Joy"
                         && n.def?.defName != "Comfort"
                         && n.def?.defName != "Social")
                .ToList();
            foreach (Need need in toStrip)
                Consciousness.needs.AllNeeds.Remove(need);

            if (Consciousness.needs.TryGetNeed<Need_SyntheraCoherence>() == null)
            {
                var coherenceNeed = new Need_SyntheraCoherence(Consciousness);
                coherenceNeed.def = DefDatabase<NeedDef>.GetNamed("NeedSyntheraCoherence", false);
                if (coherenceNeed.def != null)
                {
                    coherenceNeed.CurLevel = 0f;
                    Consciousness.needs.AllNeeds.Add(coherenceNeed);
                }
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
                    Consciousness.style.BodyTattoo = DefDatabase<TattooDef>.GetNamed("NoTattoo_Body", false)
                        ?? DefDatabase<TattooDef>.AllDefs.FirstOrDefault();
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
            if (coh != null) { coh.CurLevel = 0f; coh.ClearStrainHediff(); }

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
                        safeNeed.CurLevel = 0f;
                        Consciousness.needs.AllNeeds.Add(safeNeed);
                    }
                }

                // Always reset cache to 0 on spawn — clears any stale value from saves or old system.
                var cohSpawn = Consciousness.needs?.TryGetNeed<Need_SyntheraCoherence>();
                if (cohSpawn != null) { cohSpawn.CurLevel = 0f; cohSpawn.ClearStrainHediff(); }

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
            SoundDef sound = Props.spawnSound ?? SoundDefOf.PsychicPulseGlobal;
            sound.PlayOneShot(new TargetInfo(parent.Position, parent.Map));

            FleckMaker.Static(parent.Position, parent.Map, FleckDefOf.PsycastAreaEffect, 5f);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);

            if (Consciousness == null || Consciousness.Destroyed) return;

            string pawnName = Consciousness.Name?.ToStringShort ?? "The avatar";
            IntVec3 dropPos = Consciousness.Spawned ? Consciousness.Position : parent.Position;

            // Drop carried items before despawning.
            if (Consciousness.Spawned)
            {
                Consciousness.equipment?.DropAllEquipment(dropPos);
                Consciousness.apparel?.DropAll(dropPos);
                Consciousness.inventory?.DropAllNearPawn(dropPos);
            }

            if (mode == DestroyMode.KillFinalize)
            {
                // Violent destruction: explosion + dump into corrupted core.
                var slimeDamage = DefDatabase<DamageDef>.GetNamed("Slime", false) ?? DamageDefOf.Burn;
                GenExplosion.DoExplosion(dropPos, previousMap, 2f, slimeDamage, null,
                    postExplosionSpawnThingDef: ThingDefOf.Filth_Slime, postExplosionSpawnChance: 1f);

                // Mark consciousness as corrupted so the player sees a recovery penalty on restore.
                var syndromeDef = DefDatabase<HediffDef>.GetNamed("SyntheraHibernationSyndrome", false);
                if (syndromeDef != null)
                {
                    var existing = Consciousness.health.hediffSet.GetFirstHediffOfDef(syndromeDef);
                    if (existing == null) Consciousness.health.AddHediff(syndromeDef).Severity = 1f;
                    else existing.Severity = 1f;
                }

                // Cache is trashed on violent shutdown.
                var coh = Consciousness.needs?.TryGetNeed<Need_SyntheraCoherence>();
                if (coh != null) coh.CurLevel = 0f;

                DropCore("SyntheraCorruptedCore", dropPos, previousMap, pawnName, corrupted: true);
            }
            else if (mode == DestroyMode.Deconstruct)
            {
                // Clean deconstruct: consciousness safely transferred to a standard memory core.
                DropCore("SyntheraMemoryCore", dropPos, previousMap, pawnName, corrupted: false);
            }
            else
            {
                // Any other mode (Vanish, etc.): permanent loss.
                if (Consciousness.Spawned) Consciousness.DeSpawn();
                if (!Consciousness.Destroyed) Consciousness.Destroy(DestroyMode.Vanish);
            }
        }

        private void DropCore(string defName, IntVec3 pos, Map map, string pawnName, bool corrupted)
        {
            if (Consciousness.Spawned) Consciousness.DeSpawn();

            var coreDef = DefDatabase<ThingDef>.GetNamed(defName, false);
            if (coreDef == null)
            {
                if (!Consciousness.Destroyed) Consciousness.Destroy(DestroyMode.Vanish);
                return;
            }

            Thing core = ThingMaker.MakeThing(coreDef);
            var comp = core.TryGetComp<CompConsciousnessCore>();
            if (comp != null) comp.StoredConsciousness = Consciousness;
            Consciousness = null;

            GenPlace.TryPlaceThing(core, pos, map, ThingPlaceMode.Near);

            Messages.Message(
                corrupted
                    ? $"{pawnName}'s consciousness was emergency-dumped into a corrupted core. The altar's destruction left cache fragments — recovery is possible but the avatar will need time to stabilise."
                    : $"The altar was deconstructed. {pawnName}'s consciousness was safely transferred to a memory core.",
                MessageTypeDefOf.NegativeEvent, false);
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
                    defaultLabel = "Recall avatar",
                    defaultDesc  = "Recall the avatar to the core. The avatar releases its cache back to core systems cleanly. Available again after cooldown.",
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

                // Maintenance Pulse and Signal Burst are temporarily disabled — not yet practical.
                // if (altarTier >= 2) { ... pulseBtn ... yield return pulseBtn; }
                // if (altarTier >= 3) { ... burstBtn ... yield return burstBtn; }
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
                string cacheStr = "Cache: 0%";
                var coh = Consciousness.needs?.TryGetNeed<Need_SyntheraCoherence>();
                if (coh != null)
                    cacheStr = coh.CurLevel < 0.75f ? $"Cache: {(int)(coh.CurLevel * 100)}%"
                             : coh.CurLevel < 0.9f  ? $"Cache: {(int)(coh.CurLevel * 100)}% [filling]"
                             : $"Cache: {(int)(coh.CurLevel * 100)}% [OVERFLOW IMMINENT]";
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
