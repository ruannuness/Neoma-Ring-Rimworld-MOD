using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace FormgelCore
{
    public class CompNeomaRing : ThingComp
    {
        public CompProperties_NeomaRing Props => (CompProperties_NeomaRing)props;
        private bool neomaSpawned = false;

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            if (!neomaSpawned)
            {
                SpawnNeoma(pawn);
                neomaSpawned = true;
            }
        }

        private void SpawnNeoma(Pawn wearer)
        {
            PawnKindDef neomaKind = DefDatabase<PawnKindDef>.GetNamed("NeomaPawn", false);
            if (neomaKind == null)
            {
                Log.Error("NeomaPawn not found!");
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
            
            // Setup similar to formgels
            if (neoma.relations == null) neoma.relations = new Pawn_RelationsTracker(neoma);
            if (neoma.interactions == null) neoma.interactions = new Pawn_InteractionsTracker(neoma);
            
            while (neoma.story.traits.allTraits.Count > 0)
            {
                neoma.story.traits.allTraits.RemoveLast();
            }
            
            // Remove needs
            if (neoma.needs != null)
            {
                var allNeeds = neoma.needs.AllNeeds.ToList();
                foreach (Need need in allNeeds)
                {
                    neoma.needs.AllNeeds.Remove(need);
                }
            }
            
            // Add consciousness hediff
            var hediffDef = DefDatabase<HediffDef>.GetNamed("FormgelConsciousness", false);
            if (hediffDef != null)
            {
                neoma.health.AddHediff(hediffDef);
            }
            
            // Spawn near wearer
            GenPlace.TryPlaceThing(neoma, wearer.Position, wearer.Map, ThingPlaceMode.Near);
            
            // Sound and effect
            SoundDefOf.PsychicPulseGlobal.PlayOneShotOnCamera(wearer.Map);
            FleckMaker.Static(wearer.Position, wearer.Map, FleckDefOf.PsycastAreaEffect, 5f);
            
            Messages.Message("Neoma foi invocada pelo Anel de Neoma!", MessageTypeDefOf.PositiveEvent);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            List<Gizmo> gizmos = new List<Gizmo>();
            gizmos.AddRange(base.CompGetGizmosExtra());

            gizmos.Add(new Command_Action
            {
                action = () => Messages.Message("Anel de Neoma - Neoma já foi invocada!", MessageTypeDefOf.NeutralEvent),
                defaultLabel = "Status Anel",
                defaultDesc = "Verificar status do anel",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/Trade", true)
            });

            return gizmos;
        }
    }
}