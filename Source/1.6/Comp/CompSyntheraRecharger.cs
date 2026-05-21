using RimWorld;
using Verse;

namespace SyntheraCore
{
    public class CompSyntheraRecharger : ThingComp
    {
        private CompPowerTrader compPower;
        private int tickCounter;

        public CompProperties_SyntheraRecharger Props => (CompProperties_SyntheraRecharger)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            compPower = parent.TryGetComp<CompPowerTrader>();
        }

        public override void CompTick()
        {
            base.CompTick();
            if (++tickCounter < 150) return;
            tickCounter = 0;

            if (compPower != null && !compPower.PowerOn) return;
            if (parent.Map == null) return;

            foreach (Thing t in GenRadial.RadialDistinctThingsAround(
                parent.Position, parent.Map, Props.radius, useCenter: true))
            {
                if (!(t is Pawn pawn)) continue;
                var need = pawn.needs?.TryGetNeed<Need_SyntheraCoherence>();
                need?.Recharge(Props.rechargePerInterval);
            }
        }

        public override string CompInspectStringExtra()
        {
            if (compPower != null && !compPower.PowerOn)
                return "Coherence field: offline";
            return $"Coherence field: active (r={Props.radius}c)";
        }
    }
}
