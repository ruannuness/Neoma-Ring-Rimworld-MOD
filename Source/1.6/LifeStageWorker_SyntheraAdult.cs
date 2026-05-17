using RimWorld;
using System.Linq;
using Verse;

namespace SyntheraCore
{
    // Custom life stage worker that initializes the story fields the renderer requires
    // (headType, hairDef, beardDef, bodyType) WITHOUT going through
    // LifeStageWorker_HumanlikeAdult, which HAR patches with a postfix that
    // crashes for plain ThingDef pawns (expects AlienRace.ThingDef_AlienRace data).
    public class LifeStageWorker_SyntheraAdult : LifeStageWorker
    {
        public override void Notify_LifeStageStarted(Pawn pawn, LifeStageDef previousLifeStage)
        {
            base.Notify_LifeStageStarted(pawn, previousLifeStage);

            if (pawn.story == null) return;

            if (pawn.story.bodyType == null)
                pawn.story.bodyType = BodyTypeDefOf.Thin;

            if (pawn.story.headType == null)
            {
                var heads = DefDatabase<HeadTypeDef>.AllDefs
                    .Where(h => h.gender == pawn.gender || h.gender == Gender.None)
                    .ToList();
                if (heads.Count == 0)
                    heads = DefDatabase<HeadTypeDef>.AllDefs.ToList();
                if (heads.Count > 0)
                    pawn.story.headType = heads.RandomElement();
            }

            if (pawn.story.hairDef == null)
            {
                var hairs = DefDatabase<HairDef>.AllDefs
                    .Where(h => h.styleTags != null && h.styleTags.Contains("HairBasic"))
                    .ToList();
                if (hairs.Count == 0)
                    hairs = DefDatabase<HairDef>.AllDefs.ToList();
                if (hairs.Count > 0)
                    pawn.story.hairDef = hairs.RandomElement();
            }

            // beardDef lives in pawn.style (Pawn_StyleTracker) in 1.6, not pawn.story.
        }
    }
}
