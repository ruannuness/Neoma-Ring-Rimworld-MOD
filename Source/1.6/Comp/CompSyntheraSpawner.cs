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
        public Color FormgelColor;
        public CompProperties_SyntheraSpawner Props => (CompProperties_SyntheraSpawner)props;
        public int RespawnTick = 0;
        public int RecreateCooldownTick = 0;

        private bool InHibernation = false;
        private int HibernationDeathCount = 0;
        private bool SavedDeep = false;
        private CompPowerTrader compPower;

        public bool HasPower => compPower != null && compPower.PowerOn;

        public static readonly Color[] colors = new Color[]
        {
            new Color(0,    1f,   0.5f, 0.8f),
            new Color(0,    0.5f, 1f,   0.8f),
            new Color(1f,   0.25f,0.25f,0.8f),
            new Color(1f,   0.8f, 0,    0.8f),
            new Color(0.75f,0,    1f,   0.8f),
            new Color(1f,   0.5f, 0,    0.8f),
            new Color(0.1f, 0.1f, 0.1f, 0.8f),
            new Color(0.9f, 0.9f, 0.9f, 0.8f)
        };
        public static readonly string[] colorNames = { "Green", "Blue", "Red", "Yellow", "Purple", "Orange", "Black", "White" };

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
            Scribe_Values.Look(ref FormgelColor, "FormgelColor");
            Scribe_Values.Look(ref RespawnTick, "RespawnTick");
            Scribe_Values.Look(ref RecreateCooldownTick, "RecreateCooldownTick");
            Scribe_Values.Look(ref InHibernation, "InHibernation");
            Scribe_Values.Look(ref HibernationDeathCount, "HibernationDeathCount");
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

            // Detect in-world death → enter hibernation.
            // We immediately resurrect (to extract the pawn from the corpse without destroying it)
            // and then DeSpawn so the pawn is safely held by this comp until restoration.
            if (Consciousness.Dead && !InHibernation)
            {
                InHibernation = true;
                HibernationDeathCount++;
                // Each successive death adds more restoration time: 3×, 5×, 7×, …
                RespawnTick = Find.TickManager.TicksGame + Props.respawnTicks * (HibernationDeathCount * 2 + 1);

                if (ResurrectionUtility.TryResurrect(Consciousness))
                {
                    // Strip resurrection sickness — AIs don't get nauseous.
                    var sickDef = DefDatabase<HediffDef>.GetNamed("ResurrectionSickness", false);
                    if (sickDef != null)
                    {
                        var sick = Consciousness.health.hediffSet.GetFirstHediffOfDef(sickDef);
                        if (sick != null) Consciousness.health.RemoveHediff(sick);
                    }
                    if (Consciousness.Spawned)
                        Consciousness.DeSpawn();
                }

                Messages.Message(
                    $"{Consciousness.Name.ToStringShort} entered hibernation. Backup restoration available in {GenDate.ToStringTicksToPeriod(RespawnTick - Find.TickManager.TicksGame)}.",
                    parent,
                    MessageTypeDefOf.NegativeEvent);
            }
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
            HibernationDeathCount = 0;
            RespawnTick = 0;

            int tier = GetBuildingTier();
            SetupPawnStatsForTier(tier);
            ConfigureWorkForTier(tier);

            if (ModsConfig.IdeologyActive && Faction.OfPlayer.ideos?.PrimaryIdeo != null)
                Consciousness.ideo.SetIdeo(Faction.OfPlayer.ideos.PrimaryIdeo);

            p.apparel?.DestroyAll();
            SetupFormgel();
        }

        // Must check longer strings first so TierIV doesn't match TierII.
        private int GetBuildingTier()
        {
            string defName = parent.def.defName;
            if (defName.Contains("TierIV"))  return 4;
            if (defName.Contains("TierIII")) return 3;
            if (defName.Contains("TierII"))  return 2;
            return 1;
        }

        private void SetupPawnStatsForTier(int tier)
        {
            if (Consciousness.skills == null) return;
            foreach (SkillRecord skill in Consciousness.skills.skills)
            {
                skill.passion = Passion.Minor;
                skill.levelInt = Mathf.Min(5 + (tier - 1) * 3, 20);
            }
            Consciousness.skills.Notify_SkillDisablesChanged();
        }

        private void ConfigureWorkForTier(int tier)
        {
            if (Consciousness.workSettings == null) return;
            Consciousness.workSettings.EnableAndInitialize();

            foreach (WorkTypeDef workType in DefDatabase<WorkTypeDef>.AllDefs)
            {
                int priority = 3;
                if (tier == 1 && (workType == WorkTypeDefOf.Crafting || workType == WorkTypeDefOf.Hunting || workType == WorkTypeDefOf.Doctor))
                    priority = 0;
                else if (tier >= 2 && workType == WorkTypeDefOf.Warden)
                    priority = 0;

                Consciousness.workSettings.SetPriority(workType, priority);
            }
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

            var stressDef = DefDatabase<HediffDef>.GetNamed("SyntheraSystemStress", false);
            if (stressDef != null)
                Consciousness.health.AddHediff(stressDef).Severity = 0.01f;

            if (Consciousness.needs == null)
                Consciousness.needs = new Pawn_NeedsTracker(Consciousness);

            Consciousness.needs.AddOrRemoveNeedsAsAppropriate();

            var allNeeds = Consciousness.needs.AllNeeds.ToList();
            foreach (Need need in allNeeds)
                Consciousness.needs.AllNeeds.Remove(need);

            FormgelColor = colors[1]; // Default: Blue
            Consciousness.story.HairColor = FormgelColor;
            Consciousness.story.skinColorOverride = FormgelColor;
            Consciousness.Drawer.renderer.SetAllGraphicsDirty();
        }

        public void DespawnFormgel(bool explode, bool goneForGood = false)
        {
            if (Consciousness == null || !Consciousness.Spawned) return;

            // Recalling = maintenance; reset system stress so the next deployment starts clean.
            var stressDef = DefDatabase<HediffDef>.GetNamed("SyntheraSystemStress", false);
            if (stressDef != null)
            {
                var h = Consciousness.health.hediffSet.GetFirstHediffOfDef(stressDef);
                if (h != null) Consciousness.health.RemoveHediff(h);
            }

            if (Consciousness.carryTracker?.CarriedThing != null)
                Consciousness.carryTracker.TryDropCarriedThing(Consciousness.Position, ThingPlaceMode.Near, out _);

            Consciousness.apparel?.DropAll(Consciousness.Position);
            Consciousness.inventory?.DropAllNearPawn(Consciousness.Position);

            if (explode)
            {
                RespawnTick = Find.TickManager.TicksGame + Props.respawnTicks;
                Map map = Consciousness.Map;
                if (map != null)
                {
                    DamageDef slimeDamage = DefDatabase<DamageDef>.GetNamed("Slime", false) ?? DamageDefOf.Burn;
                    GenExplosion.DoExplosion(
                        Consciousness.Position, map, Props.explosionRadius, slimeDamage, Consciousness,
                        postExplosionSpawnThingDef: ThingDefOf.Filth_Slime, postExplosionSpawnChance: 1f);
                }
                if (goneForGood && !Consciousness.Dead)
                    Consciousness.Kill(null);
            }

            if (Consciousness.Spawned)
                Consciousness.DeSpawn();
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

                // Ensure system stress is present for this deployment cycle.
                var stressDef = DefDatabase<HediffDef>.GetNamed("SyntheraSystemStress", false);
                if (stressDef != null && Consciousness.health.hediffSet.GetFirstHediffOfDef(stressDef) == null)
                    Consciousness.health.AddHediff(stressDef).Severity = 0.01f;

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
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/Trade", true)
                };
                if (!HasPower)
                    createBtn.Disable("Requires power.");
                yield return createBtn;
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
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/Trade", true)
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
                        GenerateFormgelPawn();
                        SpawnFormgel();
                        RecreateCooldownTick = Find.TickManager.TicksGame + Props.recreateCooldownTicks;
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
            }
            else
            {
                yield return new Command_Action
                {
                    action = delegate
                    {
                        DespawnFormgel(false);
                        RespawnTick = Find.TickManager.TicksGame + (int)(Props.spawnIntervalDays * 60000f);
                    },
                    defaultLabel = "Despawn avatar",
                    defaultDesc  = "Return the avatar to the core. Resets system stress. Available again after cooldown.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/Forbid", true)
                };

                yield return new Command_Action
                {
                    action = delegate { Find.WindowStack.Add(new Dialog_NameAvatar(Consciousness)); },
                    defaultLabel = "Rename avatar",
                    defaultDesc  = "Set a custom name for this avatar.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/RenameZone", true)
                };

                yield return new Command_Action
                {
                    action = delegate
                    {
                        List<FloatMenuOption> options = new List<FloatMenuOption>();
                        for (int i = 0; i < colorNames.Length; i++)
                        {
                            int idx = i;
                            options.Add(new FloatMenuOption(colorNames[idx], delegate
                            {
                                FormgelColor = colors[idx];
                                Consciousness.story.HairColor = FormgelColor;
                                Consciousness.story.skinColorOverride = FormgelColor;
                                Consciousness.Drawer.renderer.SetAllGraphicsDirty();
                                PortraitsCache.SetDirty(Consciousness);
                            }));
                        }
                        Find.WindowStack.Add(new FloatMenu(options));
                    },
                    defaultLabel = "Avatar color",
                    defaultDesc  = "Choose the avatar's color.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/Trade", true)
                };
            }
        }

        public override string CompInspectStringExtra()
        {
            if (Consciousness == null)
                return $"Core: {parent.def.label}";

            if (InHibernation)
                return $"Avatar: {Consciousness.Name.ToStringShort} [HIBERNATING]";

            if (Consciousness.Spawned)
            {
                var stressDef = DefDatabase<HediffDef>.GetNamed("SyntheraSystemStress", false);
                if (stressDef != null)
                {
                    var h = Consciousness.health.hediffSet.GetFirstHediffOfDef(stressDef);
                    if (h != null)
                    {
                        string level = h.Severity < 0.3f ? "Nominal"
                                     : h.Severity < 0.6f ? "Mild stress"
                                     : h.Severity < 0.9f ? "High stress"
                                     : "Critical";
                        return $"Avatar: {Consciousness.Name.ToStringShort} | System: {level}";
                    }
                }
            }

            return $"Avatar: {Consciousness.Name.ToStringShort}";
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
