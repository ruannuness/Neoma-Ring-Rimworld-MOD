using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace FormgelCore
{
    public class CompProperties_HeatRisk : CompProperties
    {
        public float explosionDamage = 50f;
        public float explosionRadius = 3f;
        public float heatThreshold = 40f;
        public float explosionChancePerHour = 0.1f;

        public CompProperties_HeatRisk()
        {
            compClass = typeof(CompHeatRisk);
        }
    }
}