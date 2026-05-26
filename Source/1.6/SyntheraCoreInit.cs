using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SyntheraCore
{
    // DefModExtension marker — tag any RecipeDef with this to restrict it to NeomaPawn only.
    public class NeomaCraftExtension : DefModExtension { }

    // DefModExtension marker — tag any ThingDef (apparel, weapon) with this to restrict
    // equip/pickup to NeomaPawn only. Enforced by Patch_NeomaRingWearBlock and Patch_NeomaWeaponPickupBlock.
    public class NeomaExclusiveExtension : DefModExtension { }

    // DefModExtension marker — tag any building ThingDef with this to restrict construction to NeomaPawn.
    // Enforced by Patch_NeomaExclusiveBuilding (WorkGiver_ConstructFinishFrames).
    public class NeomaExclusiveBuildingExtension : DefModExtension { }

    // DefModExtension marker — tag any ResearchProjectDef with this to restrict research to NeomaPawn.
    // Enforced by Patch_NeomaResearchOnly (WorkGiver_Researcher).
    public class NeomaResearchExtension : DefModExtension { }

    [StaticConstructorOnStartup]
    static class SyntheraCoreInit
    {
        static SyntheraCoreInit()
        {
            var harmony = new Harmony("StargazeR.SyntheraCore");
            harmony.PatchAll();
        }
    }

    // Only the Neoma Ring companion may start bills that carry NeomaCraftExtension.
    // Must patch Bill (base class) — PawnAllowedToStartAnew is declared there, not on Bill_Production.
    [HarmonyPatch(typeof(Bill), "PawnAllowedToStartAnew")]
    static class Patch_Bill_NeomaCraftOnly
    {
        static void Postfix(ref bool __result, Pawn p, Bill __instance)
        {
            if (!__result) return;
            if ((__instance as Bill_Production)?.recipe?.HasModExtension<NeomaCraftExtension>() == true)
                __result = p.kindDef?.defName == "NeomaPawn"
                        || p.kindDef?.defName == "NeomaPawnTranscendent"; // Phase B pawnkind
        }
    }

    // Prefix que substitui get_UnlockedDefs por uma versão null-safe.
    // O método original em RimWorld 1.6 crashava ao encontrar um Def com label==null
    // em alguma fonte (ThingDef, RecipeDef, ResearchProjectDef, TerrainDef ou outra).
    // Esta versão itera cada fonte explicitamente, filtra nulls, e define o cache privado.
    [HarmonyPatch(typeof(ResearchProjectDef), "get_UnlockedDefs")]
    static class Patch_SafeUnlockedDefs
    {
        static readonly FieldInfo FCache = typeof(ResearchProjectDef)
            .GetField("cachedUnlockedDefs", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        static bool Prefix(ResearchProjectDef __instance, ref List<Def> __result)
        {
            var cached = FCache?.GetValue(__instance) as List<Def>;
            if (cached != null) { __result = cached; return false; }

            var result = new List<Def>();
            foreach (ThingDef t in DefDatabase<ThingDef>.AllDefs)
                if (t?.label != null && t.researchPrerequisites != null && t.researchPrerequisites.Contains(__instance))
                    result.Add(t);
            foreach (RecipeDef r in DefDatabase<RecipeDef>.AllDefs)
                if (r?.label != null && r.researchPrerequisite == __instance)
                    result.Add(r);
            foreach (ResearchProjectDef r in DefDatabase<ResearchProjectDef>.AllDefs)
                if (r?.label != null && r.prerequisites != null && r.prerequisites.Contains(__instance))
                    result.Add(r);
            foreach (TerrainDef t in DefDatabase<TerrainDef>.AllDefs)
                if (t?.label != null && t.researchPrerequisites != null && t.researchPrerequisites.Contains(__instance))
                    result.Add(t);

            __result = result.OrderBy(d => d.label).Distinct().ToList();
            FCache?.SetValue(__instance, __result);
            return false;
        }
    }

    // Hide the Neoma architect tab until the player has finished at least one Neoma research.
    // ArchitectCategoryTab.Visible already short-circuits when no designators are visible,
    // but only if buildings have researchPrerequisites in XML. This patch is the belt-and-
    // suspenders fallback that hides the tab directly via the category def name.
    [HarmonyPatch]
    static class Patch_NeomaTabVisible
    {
        static bool Prepare() =>
            AccessTools.TypeByName("RimWorld.ArchitectCategoryTab") != null;

        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("RimWorld.ArchitectCategoryTab");
            return AccessTools.PropertyGetter(type, "Visible");
        }

        static void Postfix(object __instance, ref bool __result)
        {
            if (!__result) return;
            var defField = AccessTools.Field(__instance.GetType(), "def");
            var categoryDef = defField?.GetValue(__instance) as DesignationCategoryDef;
            if (categoryDef?.defName != "Neoma") return;
            if (DebugSettings.godMode) return;
            foreach (ResearchProjectDef r in DefDatabase<ResearchProjectDef>.AllDefs)
                if (r?.tab?.defName == "NeomaProject" && r.IsFinished) return;
            __result = false;
        }
    }

    // Intercept Pawn.Kill for Synthera pawns and redirect to hibernation instead of actual death.
    // This prevents all vanilla death notifications: burial letters, ideology obligations, death thoughts.
    // Only fires for SyntheraRace* pawns that are bound to an altar on the same map.
    [HarmonyPatch(typeof(Pawn), "Kill")]
    static class Patch_SyntheraInterceptKill
    {
        static bool Prefix(Pawn __instance)
        {
            if (__instance?.def?.defName?.StartsWith("SyntheraRace") != true) return true;
            if (__instance.Dead) return true;

            Map map = __instance.Map;
            if (map == null) return true; // off-map: let vanilla handle, CompTick fallback runs later

            foreach (Building b in map.listerBuildings.allBuildingsColonist)
            {
                var s = b.TryGetComp<CompSyntheraSpawner>();
                if (s == null || s.Consciousness != __instance) continue;
                s.EnterHibernation(__instance.Position);
                return false; // skip vanilla Kill entirely
            }
            return true; // no altar found, die normally
        }
    }

    // Synthera pawns (SyntheraRace*) are synthetic — they cannot get wound infections.
    // Patched at Pawn_HealthTracker.AddHediff(HediffDef,...) because HediffComp_Infecter's
    // tick method was renamed in RimWorld 1.6 and cannot be directly targeted by name.
    [HarmonyPatch]
    static class Patch_SyntheraNoInfection
    {
        static readonly FieldInfo FPawn =
            AccessTools.Field(typeof(Pawn_HealthTracker), "pawn");

        static MethodBase TargetMethod()
        {
            return typeof(Pawn_HealthTracker)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                {
                    if (m.Name != "AddHediff") return false;
                    var ps = m.GetParameters();
                    return ps.Length > 0 && ps[0].ParameterType == typeof(HediffDef);
                });
        }

        static bool Prepare() => TargetMethod() != null;

        static bool Prefix(Pawn_HealthTracker __instance, HediffDef def)
        {
            if (def?.defName != "WoundInfection") return true;
            var pawn = FPawn?.GetValue(__instance) as Pawn;
            return pawn?.def?.defName?.StartsWith("SyntheraRace") != true;
        }
    }

    // Inject consciousness name + skills into the View Information card for SyntheraMemoryCore.
    [HarmonyPatch(typeof(StatsReportUtility), "StatsToDraw", new[] { typeof(Thing) })]
    static class Patch_MemoryCoreInfoCard
    {
        static void Postfix(Thing thing, ref IEnumerable<StatDrawEntry> __result)
        {
            var comp = thing?.TryGetComp<CompConsciousnessCore>();
            if (comp?.StoredConsciousness == null) return;

            var p = comp.StoredConsciousness;
            var extra = new List<StatDrawEntry>();

            extra.Add(new StatDrawEntry(
                StatCategoryDefOf.Basics,
                "Stored consciousness",
                p.Name.ToStringFull,
                "The digital identity encoded in this core.",
                10000));

            // Tier
            string kind = p.kindDef?.defName ?? "";
            string tier = kind.Contains("Miku")    ? "Special (Virtual Diva)"
                        : kind.Contains("TierIV")  ? "IV"
                        : kind.Contains("TierIII") ? "III"
                        : kind.Contains("TierII")  ? "II"
                        : kind.Contains("TierI")   ? "I"
                        : "—";
            extra.Add(new StatDrawEntry(
                StatCategoryDefOf.Basics,
                "Tier",
                tier,
                "The operational tier this avatar was running at.",
                9999));

            // Corruption / hibernation syndrome
            var syndromeDef = DefDatabase<HediffDef>.GetNamed("SyntheraHibernationSyndrome", false);
            if (syndromeDef != null)
            {
                var syndrome = p.health.hediffSet.GetFirstHediffOfDef(syndromeDef);
                if (syndrome != null)
                {
                    string severity = syndrome.Severity > 0.66f ? "heavy"
                                    : syndrome.Severity > 0.33f ? "moderate" : "light";
                    extra.Add(new StatDrawEntry(
                        StatCategoryDefOf.Basics,
                        "Cache corruption",
                        $"{(int)(syndrome.Severity * 100)}% ({severity})",
                        "Degree of cache fragmentation. Degrades automatically once the avatar is deployed into a functioning altar.",
                        9998));
                }
                else
                {
                    extra.Add(new StatDrawEntry(
                        StatCategoryDefOf.Basics,
                        "Cache corruption",
                        "None — clean export",
                        "No corruption detected. Avatar will deploy without recovery penalty.",
                        9998));
                }
            }

            if (p.story?.traits?.allTraits is { Count: > 0 } traits)
            {
                string traitStr = string.Join(", ", traits.Select(t => t.LabelCap.ToString()));
                extra.Add(new StatDrawEntry(
                    StatCategoryDefOf.Basics,
                    "Traits",
                    traitStr,
                    "Personality traits of the stored consciousness.",
                    9997));
            }

            if (p.skills?.skills != null)
                foreach (var skill in p.skills.skills)
                {
                    string passion = skill.passion == Passion.Major ? " ★★" :
                                     skill.passion == Passion.Minor ? " ★"  : "";
                    extra.Add(new StatDrawEntry(
                        StatCategoryDefOf.Basics,
                        skill.def.label,
                        skill.levelInt + passion,
                        skill.def.description ?? "",
                        8000 - skill.levelInt));
                }

            __result = __result.Concat(extra);
        }
    }

    // Scale down hair graphic for Miku (hair texture is user-made and larger than pawn head).
    // Intercepts GetGraphic on the hair worker and returns a 75%-sized version.
    [HarmonyPatch(typeof(PawnRenderNodeWorker), "GetGraphic")]
    static class Patch_MikuHairScale
    {
        static void Postfix(PawnRenderNodeWorker __instance, PawnRenderNode node, PawnDrawParms parms, ref Graphic __result)
        {
            if (parms.pawn?.def?.defName != "SyntheraRaceMiku") return;
            if (__result == null) return;

            if (!__instance.GetType().Name.Contains("Hair")) return;
            try
            {
                __result = GraphicDatabase.Get(
                    __result.GetType(),
                    __result.path,
                    __result.MatSingle.shader,
                    __result.drawSize * 0.75f,
                    __result.color,
                    __result.colorTwo);
            }
            catch (Exception ex) { Log.Warning("[SyntheraCore] hair scale: " + ex.Message); }
        }
    }

    // HAR skips vanilla EnsureGraphicsInitialized for alien races and does not set
    // shadowGraphic from race.specialShadowData. This postfix replicates that setup.
    [HarmonyPatch(typeof(PawnRenderer), "EnsureGraphicsInitialized")]
    static class Patch_PawnRenderer_EnsureGraphicsInitialized_Shadow
    {
        static readonly FieldInfo FShadow = typeof(PawnRenderer)
            .GetField("shadowGraphic", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        static readonly FieldInfo FPawn = typeof(PawnRenderer)
            .GetField("pawn", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        static void Postfix(PawnRenderer __instance)
        {
            Pawn pawn = FPawn?.GetValue(__instance) as Pawn;
            if (pawn?.def?.defName != "SyntheraRace") return;

            if (FShadow != null && FShadow.GetValue(__instance) == null)
            {
                ShadowData sd = pawn.def.race?.specialShadowData
                    ?? DefDatabase<ThingDef>.GetNamed("Human", false)?.race?.specialShadowData;
                if (sd != null)
                    try { FShadow.SetValue(__instance, new Graphic_Shadow(sd)); }
                    catch (Exception ex) { Log.Warning("[SyntheraCore] shadowGraphic: " + ex.Message); }
            }
        }
    }

    // Strip needs after AddOrRemoveNeedsAsAppropriate for both Synthera race pawns and Neoma.
    // SyntheraRace* keeps Cache + Joy/Comfort/Social; NeomaPawn gets all needs stripped.
    [HarmonyPatch(typeof(Pawn_NeedsTracker), "AddOrRemoveNeedsAsAppropriate")]
    static class Patch_SyntheraStripVanillaNeeds
    {
        static readonly FieldInfo FTrackerPawn =
            AccessTools.Field(typeof(Pawn_NeedsTracker), "pawn");

        static void Postfix(Pawn_NeedsTracker __instance)
        {
            Pawn pawn = FTrackerPawn?.GetValue(__instance) as Pawn;
            if (pawn == null) return;

            bool isSynthera = pawn.def?.defName?.StartsWith("SyntheraRace") == true;
            bool isNeoma    = pawn.kindDef?.defName == "NeomaPawn";

            if (!isSynthera && !isNeoma) return;

            if (isNeoma)
            {
                // Neoma has no biological needs — BioLock sustains her entirely.
                var all = __instance.AllNeeds.ToList();
                foreach (var n in all) __instance.AllNeeds.Remove(n);
                return;
            }

            // SyntheraRace: keep Cache + social needs, strip everything else.
            var toRemove = __instance.AllNeeds
                .Where(n => !(n is Need_SyntheraCoherence)
                         && n.def?.defName != "Joy"
                         && n.def?.defName != "Comfort"
                         && n.def?.defName != "Social")
                .ToList();
            foreach (var n in toRemove)
                __instance.AllNeeds.Remove(n);

            if (__instance.TryGetNeed<Need_SyntheraCoherence>() == null)
            {
                var need = new Need_SyntheraCoherence(pawn);
                need.def = DefDatabase<NeedDef>.GetNamed("NeedSyntheraCoherence", false);
                if (need.def != null) { need.CurLevel = 0f; __instance.AllNeeds.Add(need); }
            }
        }
    }

    // Right-click "Extract Neoma Ring" on corpses carrying the NeomaRingImplanted hediff.
    // GetFloatMenuOptions is defined on Thing, not Corpse — patch the base class and filter.
    [HarmonyPatch(typeof(Thing), nameof(Thing.GetFloatMenuOptions))]
    static class Patch_Corpse_NeomaRingFloatMenu
    {
        static IEnumerable<FloatMenuOption> Postfix(IEnumerable<FloatMenuOption> __result,
            Thing __instance, Pawn selPawn)
        {
            foreach (var opt in __result) yield return opt;

            if (__instance is not Corpse corpse) yield break;

            var implantDef = DefDatabase<HediffDef>.GetNamed("NeomaRingImplanted", false);
            if (implantDef == null) yield break;
            if (corpse.InnerPawn == null) yield break;
            if (!corpse.InnerPawn.health.hediffSet.HasHediff(implantDef)) yield break;

            const string label = "Extract Neoma Ring";

            if (!selPawn.CanReach(corpse, PathEndMode.Touch, Danger.Deadly))
            {
                yield return new FloatMenuOption($"{label} (no path)", null);
                yield break;
            }

            yield return new FloatMenuOption(label, () =>
            {
                var jobDef = DefDatabase<JobDef>.GetNamed("SyntheraExtractNeomaRing", false);
                if (jobDef == null) return;
                selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(jobDef, corpse), JobTag.Misc);
            });
        }
    }

    // Quest triggers on NeomaTierIII research AND after 72 in-game hours.
    // Deferred firing is handled by WorldComponent_NeomaQuestTracker.
    // Also sends narrative completion letters for key research milestones.
    [HarmonyPatch(typeof(ResearchManager), "FinishProject")]
    static class Patch_NeomaOrbitalQuestTrigger
    {
        static void Postfix(ResearchProjectDef proj)
        {
            if (proj == null) return;
            if (proj.defName == "NeomaTierIII")
                WorldComponent_NeomaQuestTracker.Instance?.OnTierIIIResearched();
            SendResearchLetter(proj.defName);
        }

        static void SendResearchLetter(string defName)
        {
            string labelKey, bodyKey;
            switch (defName)
            {
                case "NeomaTierI":
                    labelKey = "SyntheraCore_Letter_TierI_Title";
                    bodyKey  = "SyntheraCore_Letter_TierI_Body";
                    break;
                case "NeomaTierIII":
                    labelKey = "SyntheraCore_Letter_TierIII_Title";
                    bodyKey  = "SyntheraCore_Letter_TierIII_Body";
                    break;
                case "NeomaTierIV":
                    labelKey = "SyntheraCore_Letter_TierIV_Title";
                    bodyKey  = "SyntheraCore_Letter_TierIV_Body";
                    break;
                case "NeomaApex_I":
                    labelKey = "SyntheraCore_Letter_ApexI_Title";
                    bodyKey  = "SyntheraCore_Letter_ApexI_Body";
                    break;
                case "NeomaApex_III":
                    labelKey = "SyntheraCore_Letter_ApexIII_Title";
                    bodyKey  = "SyntheraCore_Letter_ApexIII_Body";
                    break;
                case "NeomaRing_ComponentSynthesis":
                    labelKey = "SyntheraCore_Letter_CompSynth_Title";
                    bodyKey  = "SyntheraCore_Letter_CompSynth_Body";
                    break;
                default: return;
            }
            Find.LetterStack.ReceiveLetter(
                labelKey.Translate(),
                bodyKey.Translate(),
                LetterDefOf.PositiveEvent);
        }
    }

    // Block equipping the Neoma Ring when it is already biologically locked to another living pawn.
    // Prefix on Wear (called before Notify_Equipped) so the ring never enters the apparel slot.
    [HarmonyPatch(typeof(Pawn_ApparelTracker), "Wear")]
    static class Patch_NeomaRingWearBlock
    {
        static bool Prefix(Pawn_ApparelTracker __instance, Apparel newApparel)
        {
            var ring = newApparel?.TryGetComp<CompNeomaRing>();
            if (ring != null && ring.IsBoundToOther(__instance.pawn))
            {
                Messages.Message(
                    $"The Neoma Ring is biologically locked to another host. {__instance.pawn.LabelShort} cannot wear it.",
                    MessageTypeDefOf.RejectInput, false);
                return false;
            }
            if (newApparel?.def?.GetModExtension<NeomaExclusiveExtension>() != null
                && __instance.pawn?.kindDef?.defName != "NeomaPawn")
            {
                Messages.Message(
                    $"This item is biometrically keyed to Neoma. {__instance.pawn.LabelShort} cannot wear it.",
                    MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return true;
        }
    }

    // Block non-Neoma pawns from equipping weapons tagged with NeomaExclusiveExtension.
    [HarmonyPatch(typeof(Pawn_EquipmentTracker), "AddEquipment")]
    static class Patch_NeomaWeaponPickupBlock
    {
        static bool Prefix(Pawn_EquipmentTracker __instance, ThingWithComps newEq)
        {
            if (newEq?.def?.GetModExtension<NeomaExclusiveExtension>() == null) return true;
            if (__instance.pawn?.kindDef?.defName == "NeomaPawn") return true;
            Messages.Message(
                $"This weapon is biometrically keyed to Neoma. {__instance.pawn.LabelShort} cannot equip it.",
                MessageTypeDefOf.RejectInput, false);
            return false;
        }
    }

    // Expose CompNeomaRing gizmos on the wearer's gizmo bar.
    // Pawn.GetGizmos() in RimWorld 1.6 does not include CompGetGizmosExtra from worn apparel comps.
    // We call GetRingGizmos() directly to avoid any double-yield if vanilla ever adds this.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    static class Patch_Pawn_NeomaRingGizmos
    {
        static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (var g in __result) yield return g;

            if (__instance.apparel == null) yield break;
            foreach (Apparel apparel in __instance.apparel.WornApparel)
            {
                var ring = apparel.TryGetComp<CompNeomaRing>();
                if (ring == null) continue;
                foreach (var g in ring.GetRingGizmos())
                    yield return g;
            }
        }
    }

    // Fires when any deployed Synthera's cache exceeds 95% — overflow is seconds away.
    public class Alert_SyntheraHighCache : Alert
    {
        public Alert_SyntheraHighCache()
        {
            defaultLabel       = "Synthera: cache overflow imminent";
            defaultExplanation = "One or more Synthera avatars are above 95% cache capacity. "
                               + "Overflow is imminent — the projection will collapse and require a recovery cycle. "
                               + "Recall immediately to flush the cache, or activate a Cache Purger nearby.";
            defaultPriority    = AlertPriority.Critical;
        }

        public override AlertReport GetReport()
        {
            var culprits = new List<Thing>();
            foreach (Map map in Find.Maps)
                foreach (Pawn p in map.mapPawns.FreeColonistsSpawned)
                {
                    var need = p.needs?.TryGetNeed<Need_SyntheraCoherence>();
                    if (need != null && need.CurLevel > 0.95f)
                        culprits.Add(p);
                }
            return culprits.Count > 0 ? AlertReport.CulpritsAre(culprits) : AlertReport.Inactive;
        }
    }

    // Block non-Neoma pawns from finishing construction frames on NeomaExclusiveBuildingExtension buildings.
    [HarmonyPatch]
    static class Patch_NeomaExclusiveBuilding
    {
        static bool Prepare() =>
            AccessTools.Method(typeof(WorkGiver_ConstructFinishFrames), "JobOnThing") != null;

        static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(WorkGiver_ConstructFinishFrames), "JobOnThing");

        static void Postfix(Pawn pawn, Thing t, ref Job __result)
        {
            if (__result == null) return;
            if (pawn.kindDef?.defName == "NeomaPawn") return;
            var frame = t as Frame;
            if (frame?.def?.entityDefToBuild?.GetModExtension<NeomaExclusiveBuildingExtension>() == null) return;
            __result = null;
        }
    }

    // Block non-Neoma pawns from researching projects marked with NeomaResearchExtension.
    [HarmonyPatch]
    static class Patch_NeomaResearchOnly
    {
        // ResearchManager.currentProj was renamed in 1.6 — locate it by type at startup.
        static readonly FieldInfo FCurrentProj =
            typeof(ResearchManager)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(f => f.FieldType == typeof(ResearchProjectDef));

        static bool Prepare() =>
            FCurrentProj != null
            && AccessTools.Method(typeof(WorkGiver_Researcher), "ShouldSkip",
                   new[] { typeof(Pawn), typeof(bool) }) != null;

        static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(WorkGiver_Researcher), "ShouldSkip",
                new[] { typeof(Pawn), typeof(bool) });

        static void Postfix(Pawn pawn, ref bool __result)
        {
            if (__result) return;
            var proj = FCurrentProj.GetValue(Find.ResearchManager) as ResearchProjectDef;
            if (proj?.GetModExtension<NeomaResearchExtension>() == null) return;
            if (pawn.kindDef?.defName == "NeomaPawn") return;
            __result = true;
        }
    }

    // Removes all Hediff_Injury instances from the pawn on first tick, then self-removes.
    // Referenced by NeomaRepairEffect hediff in Hediffs_NeomaConsumables.xml.
    public class HediffCompProperties_ClearInjuries : HediffCompProperties
    {
        public HediffCompProperties_ClearInjuries() => compClass = typeof(HediffComp_ClearInjuries);
    }

    public class HediffComp_ClearInjuries : HediffComp
    {
        public override void CompPostTick(ref float severityAdjustment)
        {
            if (!parent.pawn.Spawned) return;
            var injuries = parent.pawn.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .ToList();
            foreach (var h in injuries)
                parent.pawn.health.RemoveHediff(h);
            parent.pawn.health.RemoveHediff(parent);
        }
    }

    // Removes SyntheraSystemStress hediff from the pawn on first tick, then self-removes.
    // Referenced by NeomaCachePurgeEffect hediff in Hediffs_NeomaConsumables.xml.
    public class HediffCompProperties_ClearSyntheraStress : HediffCompProperties
    {
        public HediffCompProperties_ClearSyntheraStress() => compClass = typeof(HediffComp_ClearSyntheraStress);
    }

    public class HediffComp_ClearSyntheraStress : HediffComp
    {
        public override void CompPostTick(ref float severityAdjustment)
        {
            if (!parent.pawn.Spawned) return;
            var stressDef = DefDatabase<HediffDef>.GetNamed("SyntheraSystemStress", false);
            if (stressDef != null)
            {
                var stress = parent.pawn.health.hediffSet.GetFirstHediffOfDef(stressDef);
                if (stress != null) parent.pawn.health.RemoveHediff(stress);
            }
            parent.pawn.health.RemoveHediff(parent);
        }
    }

}
