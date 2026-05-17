using HarmonyLib;
using RimWorld;
using System;
using System.Reflection;
using Verse;

namespace SyntheraCore
{
    [StaticConstructorOnStartup]
    static class SyntheraCoreInit
    {
        static SyntheraCoreInit()
        {
            var harmony = new Harmony("StargazeR.SyntheraCore");
            harmony.PatchAll();
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
