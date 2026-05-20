using RimWorld;
using System.Text;
using Verse;

namespace SyntheraCore
{
    // Custom Thing class so the item label shows the stored Synthera's name.
    public class ThingConsciousnessCore : ThingWithComps
    {
        public override string LabelNoCount
        {
            get
            {
                var comp = this.TryGetComp<CompConsciousnessCore>();
                if (comp?.StoredConsciousness != null)
                    return base.LabelNoCount + " [" + comp.StoredConsciousness.Name.ToStringShort + "]";
                return base.LabelNoCount;
            }
        }
    }

    // Stores a Synthera pawn inside a physical item so it can be moved between altars.
    // The pawn is serialised deep into the comp; destroying the item by any means other
    // than Deconstruct kills the consciousness permanently.
    public class CompConsciousnessCore : ThingComp
    {
        public Pawn StoredConsciousness;

        public CompProperties_ConsciousnessCore Props => (CompProperties_ConsciousnessCore)props;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref StoredConsciousness, "StoredConsciousness");
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            if (mode != DestroyMode.Deconstruct && StoredConsciousness != null && !StoredConsciousness.Destroyed)
            {
                StoredConsciousness.Destroy(DestroyMode.KillFinalize);
                Messages.Message(
                    "A Synthera memory core was destroyed. The stored consciousness has been lost.",
                    MessageTypeDefOf.NegativeEvent, false);
            }
        }

        public override string CompInspectStringExtra()
        {
            if (StoredConsciousness == null) return "Memory core: empty";

            var p = StoredConsciousness;
            var sb = new StringBuilder();
            sb.AppendLine($"Stored: {p.Name.ToStringFull}");

            if (p.skills?.skills != null)
            {
                sb.AppendLine("── Skills ──────────────");
                var skills = p.skills.skills;
                for (int i = 0; i < skills.Count; i += 2)
                {
                    string left  = FormatSkill(skills[i]);
                    string right = i + 1 < skills.Count ? "  " + FormatSkill(skills[i + 1]) : "";
                    sb.AppendLine(left + right);
                }
            }

            if (p.story?.traits?.allTraits is { Count: > 0 } traits)
            {
                sb.Append("Traits: ");
                foreach (var t in traits) sb.Append(t.LabelCap + "  ");
            }

            return sb.ToString().TrimEnd();
        }

        private static string FormatSkill(SkillRecord s)
        {
            string passion = s.passion == Passion.Major ? "★★" :
                             s.passion == Passion.Minor ? "★"  : "  ";
            return $"{s.def.LabelCap,-13} {s.levelInt,2} {passion}";
        }
    }
}
