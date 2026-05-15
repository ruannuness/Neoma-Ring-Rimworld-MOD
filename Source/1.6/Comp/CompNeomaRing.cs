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

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref neomaPawn, "neomaPawn");
        }

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);

            // Enforce biolock: if the ring is already bound to another colonist, reject
            HediffDef biolockDef = HediffDef.Named("NeomaBiolock");
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

            if (neomaPawn == null || neomaPawn.Dead || neomaPawn.Destroyed)
                SpawnNeoma(pawn);

            if (!pawn.health.hediffSet.HasHediff(biolockDef))
                pawn.health.AddHediff(biolockDef);
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            DespawnNeoma();

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDef.Named("NeomaBiolock"));
            if (hediff != null)
                pawn.health.RemoveHediff(hediff);
        }

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

            if (neoma.relations == null) neoma.relations = new Pawn_RelationsTracker(neoma);
            if (neoma.interactions == null) neoma.interactions = new Pawn_InteractionsTracker(neoma);

            while (neoma.story.traits.allTraits.Count > 0)
                neoma.story.traits.allTraits.RemoveLast();

            SyntheraUtils.SetupPawn(neoma);

            GenPlace.TryPlaceThing(neoma, wearer.Position, wearer.Map, ThingPlaceMode.Near);
            neomaPawn = neoma;

            SoundDefOf.PsychicPulseGlobal.PlayOneShotOnCamera(wearer.Map);
            FleckMaker.Static(wearer.Position, wearer.Map, FleckDefOf.PsycastAreaEffect, 5f);
            Messages.Message("Neoma has been summoned by the Neoma Ring!", MessageTypeDefOf.PositiveEvent);
        }

        private void DespawnNeoma()
        {
            if (neomaPawn != null && neomaPawn.Spawned)
                neomaPawn.DeSpawn();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra()) yield return g;

            if (neomaPawn == null) yield break;

            string status = neomaPawn.Dead     ? "deceased" :
                            neomaPawn.Spawned  ? $"active at {neomaPawn.Position}" :
                                                 "recalled";

            yield return new Command_Action
            {
                action = () => Messages.Message(
                    $"Neoma Ring — Neoma is {status}.",
                    MessageTypeDefOf.NeutralEvent),
                defaultLabel = "Ring Status",
                defaultDesc = $"Neoma is currently: {status}.",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/Trade", true)
            };

            if (neomaPawn.Spawned && !neomaPawn.Dead)
            {
                Pawn wearer = (parent as Apparel)?.Wearer;
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
                        Messages.Message("Neoma has been recalled to the ring wearer.", MessageTypeDefOf.NeutralEvent);
                    },
                    defaultLabel = "Recall Neoma",
                    defaultDesc = "Teleport Neoma back to the ring wearer's location.",
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/TechPrint", true)
                };
            }
        }
    }
}
