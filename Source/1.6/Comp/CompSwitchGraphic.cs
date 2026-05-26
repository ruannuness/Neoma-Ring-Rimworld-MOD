using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace SyntheraCore
{
    public class CompProperties_SwitchGraphic : CompProperties
    {
        // Texture shown when unpowered. The def's graphicData.texPath is the powered (on) state,
        // which is also what appears in the architect panel and research UI.
        public string graphicPathOff;

        public CompProperties_SwitchGraphic()
        {
            compClass = typeof(CompSwitchGraphic);
        }
    }

    public class CompSwitchGraphic : ThingComp
    {
        private Graphic _graphicOn;
        private Graphic _graphicOff;
        private bool _isOn;

        private CompProperties_SwitchGraphic Props => (CompProperties_SwitchGraphic)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            _graphicOn = parent.def.graphicData.GraphicColoredFor(parent);
            _graphicOff = GraphicDatabase.Get<Graphic_Single>(
                Props.graphicPathOff,
                _graphicOn.Shader,
                parent.def.graphicData.drawSize,
                Color.white);

            var power = parent.GetComp<CompPowerTrader>();
            var flick = parent.GetComp<CompFlickable>();
            _isOn = power != null && power.PowerOn && (flick == null || flick.SwitchIsOn);
            ApplyGraphic();
        }

        public override void ReceiveCompSignal(string signal)
        {
            switch (signal)
            {
                case "PowerTurnedOn":
                case "FlickedOn":
                    SetOn(true);
                    break;
                case "PowerTurnedOff":
                case "FlickedOff":
                    SetOn(false);
                    break;
            }
        }

        private void SetOn(bool on)
        {
            if (_isOn == on) return;
            _isOn = on;
            ApplyGraphic();
        }

        private void ApplyGraphic()
        {
            if (_graphicOn == null || _graphicOff == null) return;
            Traverse.Create(parent).Field("graphicInt").SetValue(_isOn ? _graphicOn : _graphicOff);
            parent.Map?.mapDrawer.MapMeshDirty(parent.Position, ~0UL);
        }
    }
}
