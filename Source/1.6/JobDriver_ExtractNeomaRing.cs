using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace SyntheraCore
{
    public class JobDriver_ExtractNeomaRing : JobDriver
    {
        private const int ExtractionTicks = 600; // 10 in-game seconds

        private Corpse Corpse => (Corpse)job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            yield return Toils_General.Wait(ExtractionTicks, TargetIndex.A)
                .WithProgressBarToilDelay(TargetIndex.A);

            var finish = ToilMaker.MakeToil("NeomaRingFinish");
            finish.initAction = () =>
            {
                var corpse = Corpse;
                Recipe_ExtractNeomaRing.ExecuteExtraction(corpse.InnerPawn, corpse.Map, corpse.Position);
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }
}
