using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace SyntheraCore
{
    public class CompProperties_NeomaRing : CompProperties
    {
        public string pawnKind;

        public CompProperties_NeomaRing()
        {
            compClass = typeof(CompNeomaRing);
        }
    }
}