using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace FormgelCore
{
    [StaticConstructorOnStartup]
    public class CompFormgelSpawner : ThingComp
    {
        public Pawn Consciousness;
        public Color FormgelColor;
        public CompProperties_FormgelSpawner Props => (CompProperties_FormgelSpawner)props;
        public int RespawnTick = 0;

        public static Color[] colors = new Color[] 
        { 
            new Color(0, 1f, 0.5f, 0.8f), // Green
            new Color(0, 0.5f, 1f, 0.8f), // Blue
            new Color(1f, 0.25f, 0.25f, 0.8f), // Red
            new Color(1f, 0.8f, 0, 0.8f), // Yellow
            new Color(0.75f, 0, 1f, 0.8f), // Purple
            new Color(1f, 0.5f, 0, 0.8f), // Orange
            new Color(0.1f, 0.1f, 0.1f, 0.8f), // Black
            new Color(0.9f, 0.9f, 0.9f, 0.8f) // White
        };
        public static string[] colorNames = new string[] { "Green", "Blue", "Red", "Yellow", "Purple", "Orange", "Black", "White" };

        private bool SavedDeep = false;
        private CompPowerTrader compPower;

        // Property that was missing to fix the !HasPower error
        public bool HasPower => compPower != null && compPower.PowerOn;

        public override void PostExposeData()
        {
            base.PostExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (Consciousness != null && !Consciousness.Spawned && !Find.WorldPawns.Contains(Consciousness) && !Consciousness.InContainerEnclosed && Consciousness.CarriedBy == null)
                {
                    SavedDeep = true;
                    Scribe_Values.Look<bool>(ref SavedDeep, "SavedDeep");
                    Scribe_Deep.Look<Pawn>(ref Consciousness, "Consciousness");
                }
                else
                {
                    SavedDeep = false;
                    Scribe_Values.Look<bool>(ref SavedDeep, "SavedDeep");
                    Scribe_References.Look<Pawn>(ref Consciousness, "Consciousness");
                }
            }
            else
            {
                Scribe_Values.Look<bool>(ref SavedDeep, "SavedDeep");
                if (SavedDeep)
                {
                    Scribe_Deep.Look<Pawn>(ref Consciousness, "Consciousness");
                }
                else
                    Scribe_References.Look<Pawn>(ref Consciousness, "Consciousness");
            }
            Scribe_Values.Look<Color>(ref FormgelColor, "FormgelColor");
            Scribe_Values.Look<int>(ref RespawnTick, "RespawnTick");
        }

        public void GenerateFormgelPawn()
        {
            PawnKindDef pawnKind = DefDatabase<PawnKindDef>.GetNamed(Props.pawnKind, false);
            if (pawnKind == null)
            {
                Log.Error($"FormgelCore: Could not find PawnKindDef named {Props.pawnKind}");
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
            p.Name = new NameTriple("", pawnKind.label, "");
            
            // Basic assignments
            if (p.relations == null) p.relations = new Pawn_RelationsTracker(p);
            if (p.interactions == null) p.interactions = new Pawn_InteractionsTracker(p);
            
            while (p.story.traits.allTraits.Count > 0)
            {
                p.story.traits.allTraits.RemoveLast();
            }
            
            Consciousness = p;
            
            // TODO: Move skill and work settings to PawnKindDef or a custom XML extension for better moddability.
            int tier = GetBuildingTier(); 
            SetupPawnStatsForTier(tier);
            ConfigureWorkForTier(tier);
            
            if (ModsConfig.IdeologyActive && Faction.OfPlayer.ideos?.PrimaryIdeo != null)
                Consciousness.ideo.SetIdeo(Faction.OfPlayer.ideos.PrimaryIdeo);
            
            p.apparel?.DestroyAll();
            SetupFormgel();
        }

        // TODO: This tier system is coupled to the defName. Refactor to be data-driven from XML.
        private int GetBuildingTier()
        {
            string defName = parent.def.defName;
            if (defName.Contains("Tier4")) return 4;
            if (defName.Contains("Tier3")) return 3;
            if (defName.Contains("Tier2")) return 2;
            return 1; 
        }

        // TODO: This should be configured in PawnKindDef or a custom extension.
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

        // TODO: This should be configured in PawnKindDef or a custom extension.
        private void ConfigureWorkForTier(int tier)
        {
            if (Consciousness.workSettings == null) return;
            Consciousness.workSettings.EnableAndInitialize();

            foreach (WorkTypeDef workType in DefDatabase<WorkTypeDef>.AllDefs)
            {
                int priority = 3;
                if (tier == 1 && (workType == WorkTypeDefOf.Crafting || workType == WorkTypeDefOf.Hunting || workType == WorkTypeDefOf.Doctor))
                    priority = 0;
                else if (tier == 2 && (workType == WorkTypeDefOf.Warden))
                    priority = 0;
                else if (tier == 3 && workType == WorkTypeDefOf.Warden)
                    priority = 0;

                Consciousness.workSettings.SetPriority(workType, priority);
            }
        }

        private void RemoveUnnecessaryNeeds()
        {
            if (Consciousness.needs == null) return;
            
            // Correction: Using ToList() to avoid list modification error in foreach
            var allNeeds = Consciousness.needs.AllNeeds.ToList();
            foreach (Need need in allNeeds)
            {
                Consciousness.needs.AllNeeds.Remove(need);
            }
        }

        void SetupFormgel()
        {
            if (Consciousness == null) return;

            var hediffs = Consciousness.health.hediffSet.hediffs.ToList();
            foreach (Hediff hediff in hediffs)
            {
                Consciousness.health.RemoveHediff(hediff);
            }
            
            var hediffDef = DefDatabase<HediffDef>.GetNamed("FormgelConsciousness", false);
            if (hediffDef != null)
            {
                Consciousness.health.AddHediff(hediffDef);
            }
            
            if (Consciousness.needs == null)
                Consciousness.needs = new Pawn_NeedsTracker(Consciousness);
            
            Consciousness.needs.AddOrRemoveNeedsAsAppropriate();
            RemoveUnnecessaryNeeds();
            
            FormgelColor = colors[1]; 
            Consciousness.story.HairColor = FormgelColor;
            Consciousness.story.skinColorOverride = FormgelColor;
            Consciousness.Drawer.renderer.SetAllGraphicsDirty();
            
            DespawnFormgel(false);
        }

        public void DespawnFormgel(bool explode, bool goneForGood = false)
        {
            if (Consciousness == null || !Consciousness.Spawned) return;

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
                    GenExplosion.DoExplosion(Consciousness.Position, map, Props.explosionRadius, slimeDamage, Consciousness, 
                        postExplosionSpawnThingDef: ThingDefOf.Filth_Slime, postExplosionSpawnChance: 1f);
                }
                if (goneForGood && !Consciousness.Dead)
                    Consciousness.Kill(null);
            }

            if (Consciousness.Spawned)
                Consciousness.DeSpawn();
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

            if (Consciousness != null)
            {
                if (!Consciousness.Spawned)
                {
                    Command_Action spawn = new Command_Action
                    {
                        action = delegate { SpawnFormgel(); },
                        defaultLabel = "Spawn formgel",
                        defaultDesc = "Assemble the formgel avatar",
                        icon = ContentFinder<Texture2D>.Get("UI/Commands/Trade", true)
                    };

                    if (Find.TickManager.TicksGame < this.RespawnTick)
                    {
                        spawn.Disable("Formgel regenerating: " + GenDate.ToStringTicksToPeriod(this.RespawnTick - Find.TickManager.TicksGame));
                    }
                    yield return spawn;
                }
                else
					{
						Command_Action createBtn = new Command_Action
						{
							action = delegate 
							{ 
								// Double check before executing
								if (!HasPower) {
									Messages.Message("Cannot create: No power.", MessageTypeDefOf.RejectInput, false);
									return;
								}
								GenerateFormgelPawn(); 
								SpawnFormgel(); 
							},
							defaultLabel = "Create formgel",
							defaultDesc = "Generate a new formgel avatar",
							icon = ContentFinder<Texture2D>.Get("UI/Commands/Trade", true)
						};


						yield return createBtn;
					}

                yield return new Command_Action
                {
                    action = delegate
                    {
                        List<FloatMenuOption> options = new List<FloatMenuOption>();
                        for (int i = 0; i < colorNames.Length; i++)
                        {
                            int index = i;
                            options.Add(new FloatMenuOption(colorNames[i], delegate
                            {
                                FormgelColor = colors[index];
                                Consciousness.story.HairColor = FormgelColor;
                                Consciousness.story.skinColorOverride = FormgelColor;
                                Consciousness.Drawer.renderer.SetAllGraphicsDirty();
                                PortraitsCache.SetDirty(Consciousness);
                            }));
                        }
                        Find.WindowStack.Add(new FloatMenu(options));
                    },
                    defaultLabel = "Formgel color",
                    defaultDesc = "Choose the formgel avatar color",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/Trade", true)
                };
            }
            else
            {
                yield return new Command_Action
                {
                    action = delegate { GenerateFormgelPawn(); SpawnFormgel(); },
                    defaultLabel = "Create formgel",
                    defaultDesc = "Generate a new formgel avatar",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/Trade", true)
                };
            }
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
            {
                // If the pawn was destroyed, it needs to be generated again
                GenerateFormgelPawn();
            }

            if (!Consciousness.Spawned)
            {
                GenPlace.TryPlaceThing(Consciousness, parent.Position, parent.Map, ThingPlaceMode.Near);
                Consciousness.Drawer.renderer.EnsureGraphicsInitialized();
                
                if (Consciousness.Dead)
                    ResurrectionUtility.TryResurrect(Consciousness);

                if(Props.spawnSound != null)
                    Props.spawnSound.PlayOneShotOnCamera(parent.Map);
                else
                    SoundDefOf.PsychicPulseGlobal.PlayOneShotOnCamera(parent.Map);

                FleckMaker.Static(parent.Position, parent.Map, FleckDefOf.PsycastAreaEffect, 5f);
                Consciousness.ageTracker?.ResetAgeReversalDemand(Pawn_AgeTracker.AgeReversalReason.ViaTreatment);
            }
        }

        public override string CompInspectStringExtra()
        {
            if (Consciousness != null)
                return $"Formgel: {Consciousness.Name.ToStringShort}";
            
            return $"Core: {parent.def.label}";
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
                {
                    DespawnFormgel(false);
                }
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            // Automatic despawn if power is lost
            if (Consciousness != null && Consciousness.Spawned && !HasPower)
            {
                DespawnFormgel(false);
            }
        }
    }
}