using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace SyntheraCore
{
    // DefModExtension marker — tag any RecipeDef with this to restrict it to NeomaPawn only.
    public class NeomaCraftExtension : DefModExtension { }

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

            if (p.story?.traits?.allTraits is { Count: > 0 } traits)
            {
                string traitStr = string.Join(", ", traits.Select(t => t.LabelCap.ToString()));
                extra.Add(new StatDrawEntry(
                    StatCategoryDefOf.Basics,
                    "Traits",
                    traitStr,
                    "Personality traits of the stored consciousness.",
                    9999));
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
}
