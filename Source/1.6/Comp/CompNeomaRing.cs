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
        private Pawn neomaPawn;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look<Pawn>(ref neomaPawn, "neomaPawn");
        }

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            if (neomaPawn == null || neomaPawn.Dead || neomaPawn.Destroyed)
            {
                SpawnNeoma(pawn);
            }
            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("NeomaBiolock"));
            if (hediff == null)
            {
                pawn.health.AddHediff(HediffDef.Named("NeomaBiolock"));
            }
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            DespawnNeoma();
            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("NeomaBiolock"));
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }

        private void SpawnNeoma(Pawn wearer)
        {
            PawnKindDef neomaKind = DefDatabase<PawnKindDef>.GetNamed(Props.pawnKind, false);
            if (neomaKind == null)
            {
                Log.Error($"FormgelCore: Could not find PawnKindDef named {Props.pawnKind}");
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
            
            FormgelUtils.SetupPawn(neoma);
            
            // Spawn near wearer
            GenPlace.TryPlaceThing(neoma, wearer.Position, wearer.Map, ThingPlaceMode.Near);
            neomaPawn = neoma;

            // Sound and effect
            SoundDefOf.PsychicPulseGlobal.PlayOneShotOnCamera(wearer.Map);
            FleckMaker.Static(wearer.Position, wearer.Map, FleckDefOf.PsycastAreaEffect, 5f);
            
            Messages.Message("Neoma has been summoned by the Neoma Ring!", MessageTypeDefOf.PositiveEvent);
        }

        private void DespawnNeoma()
        {
            if (neomaPawn != null && neomaPawn.Spawned)
            {
                neomaPawn.DeSpawn();
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra())
            {
                yield return g;
            }

            if (neomaPawn != null)
            {
                yield return new Command_Action
                {
                    action = () => Messages.Message("Neoma Ring - Neoma is currently active.", MessageTypeDefOf.NeutralEvent),
                    defaultLabel = "Ring Status",
                    defaultDesc = "Check the status of the ring.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/Trade", true)
                };
            }
        }
    }
}