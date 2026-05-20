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
