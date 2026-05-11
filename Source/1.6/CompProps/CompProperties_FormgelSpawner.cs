using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace FormgelCore
{
	public class CompProperties_FormgelSpawner : CompProperties
	{
		public string pawnKind;
		public float spawnIntervalDays = 1f;
		public int maxPawnsToSpawn = 1;
		public SoundDef spawnSound;

		public CompProperties_FormgelSpawner()
		{
			this.compClass = typeof(CompFormgelSpawner);
		}

		static CompProperties_FormgelSpawner()
		{
			// Force loading of this type
		}
	}
}
