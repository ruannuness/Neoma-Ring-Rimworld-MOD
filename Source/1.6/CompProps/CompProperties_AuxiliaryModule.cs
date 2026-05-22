using System.Collections.Generic;
using Verse;

namespace SyntheraCore
{
    public enum AuxModuleType
    {
        CombatAmplifier,
        CoreOptimizer,
        RoleSpecializer,
        WorkUnlocker,
        ArmorMatrix,
        OverclockCore,
        VersatilityProtocol
    }

    public enum AvatarRole
    {
        None,
        Soldier,
        Worker,
        Medic,
        Scout
    }

    public class CompProperties_AuxiliaryModule : CompProperties
    {
        public AuxModuleType moduleType;
        public float detectionRadius   = 5f;
        public int   scanIntervalTicks = 250;

        // CombatAmplifier
        public string pawnBuffHediff;

        // CoreOptimizer
        public float respawnTicksMultiplier     = 1f;
        public float recreateCooldownMultiplier = 1f;

        // RoleSpecializer
        public AvatarRole role = AvatarRole.None;

        // WorkUnlocker
        public List<string> unlockedWorkTypes;

        public CompProperties_AuxiliaryModule()
        {
            compClass = typeof(CompAuxiliaryModule);
        }
    }
}
