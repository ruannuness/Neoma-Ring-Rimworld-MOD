# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build

```powershell
cd "Source/1.6"
dotnet build SyntheraCore.csproj
```

Output DLL goes to `Assemblies/SyntheraCore.dll` (used directly by RimWorld). There are no tests. Verification is done by launching RimWorld with the mod enabled and observing in-game behaviour.

## Project identity

- **Mod folder:** `FormgelCore` (legacy name kept for file-system stability)
- **Package ID:** `StargazeR.SyntheraCore`
- **Assembly name / namespace:** `SyntheraCore`
- **Target:** RimWorld 1.6, net472, x64
- **DLL references** (all relative from `Source/1.6/`):
  - `Assembly-CSharp.dll` → `../../../../RimWorldWin64_Data/Managed/`
  - `UnityEngine.CoreModule.dll` → same path
  - `0Harmony.dll` → `../../../../Mods/1127530465/1.6/Assemblies/`

## Repository layout

```
FormgelCore/
├── Assemblies/          ← compiled output (SyntheraCore.dll + RimWorld refs)
├── Source/1.6/          ← all C# source
│   ├── SyntheraCore.csproj
│   ├── SyntheraCoreInit.cs          (StaticConstructorOnStartup entry point)
│   ├── Comp/
│   │   ├── CompSyntheraSpawner.cs   (main building logic)
│   │   ├── CompHeatRisk.cs          (Tier I heat/explosion mechanic)
│   │   └── CompNeomaRing.cs         (wearable ring logic)
│   ├── CompProps/
│   │   ├── CompProperties_SyntheraSpawner.cs
│   │   ├── CompProps_HeatRisk.cs
│   │   └── CompProps_NeomaRing.cs
│   └── Utils/
│       └── SyntheraUtils.cs         (SetupPawn shared helper)
└── 1.6/
    ├── Defs/
    │   ├── ThingDefs_Buildings/     (Buildings_Neoma.xml, Buildings_SyntheraCore.xml)
    │   ├── ThingDefs_Pawns/         (PawnKinds_Neoma.xml)
    │   ├── ThingDefs_Apparel/       (Apparel_Neoma.xml — the ring)
    │   ├── HediffDefs/              (Hediffs_SyntheraCore.xml, Hediffs_Neoma.xml)
    │   ├── ResearchDefs/            (ResearchProjects_Neoma.xml, ResearchTabs_Neoma.xml)
    │   └── DesignationCategories_Neoma.xml
    ├── Languages/English/Keyed/
    └── Textures/Things/Building/Tier0{1-4}/
```

## Architecture: how the pieces connect

### Core mechanic — CompSyntheraSpawner
Every altar building has `CompSyntheraSpawner`. It owns a single `Pawn Consciousness` that is either deployed on the map or held in memory by the comp. Key state machine:

| State | Consciousness.Spawned | Consciousness.Dead | InHibernation |
|---|---|---|---|
| No avatar created | — (null/Destroyed) | — | — |
| Recalled (waiting) | false | false | false |
| Hibernating (died) | false | false | true |
| Deployed | true | false | false |

Death flow in `CompTick`: detects `Consciousness.Dead && !InHibernation` → immediately calls `ResurrectionUtility.TryResurrect` (extracts pawn from corpse without destroying pawn object) → strips ResurrectionSickness → `DeSpawn()` → sets `InHibernation = true`, `RespawnTick`. Player then clicks "Restore backup" to re-deploy the **same** pawn with `SyntheraHibernationSyndrome` applied.

Save/load: `storeDeep = !Spawned && !Dead && not held elsewhere`. If `storeDeep`, the pawn is serialised inside the comp via `Scribe_Deep`; otherwise via `Scribe_References` (e.g. when on the map in a bed or world).

### Pawn setup pipeline
`GenerateFormgelPawn()` → `SetupPawnStatsForTier(tier)` + `ConfigureWorkForTier(tier)` + `SetupFormgel()`

`SetupFormgel()` (and `SyntheraUtils.SetupPawn()` for the ring) strips ALL hediffs, adds `SyntheraConsciousness`, adds `SyntheraSystemStress` at 0.01, strips ALL needs. **Never skip this call on a freshly generated pawn.**

Tier is parsed from `parent.def.defName` — check longest suffix first: `TierIV → TierIII → TierII → 1`.

### Building tier progression

| Tier | DefName | Research req | Power | PawnKindDef |
|---|---|---|---|---|
| I | NeomaAltarTierI | NeomaTierI | 2000 W | NeomaPawnTierI |
| II | NeomaAltarTierII | NeomaTierII | 1000 W | NeomaPawnTierII |
| III | NeomaAltarTierIII | NeomaTierIII | 500 W | NeomaPawnTierIII |
| IV | NeomaAltarTierIV | NeomaTierIV | 200 W | NeomaPawnTierIV |

Research chain: `NeomaTierI → II → III → IV → NeomaRingResearch` (all require HiTechResearchBench).

### Hediff balance system

- `SyntheraSystemStress` — grows +0.033/day while deployed; cleared on recall (maintenance). Stages: Nominal → Mild (-10% work) → High (-25% work, -15% move) → Critical (-50%/-30%).
- `SyntheraHibernationSyndrome` — applied at severity 1.0 on backup restore; heals -0.1/day over 10 days. Penalty scales down through 3 stages.
- `SyntheraConsciousness` — permanent buff (+20% move, +10% work) marking the pawn as an AI avatar.

### Inter-component communication
- Power signals: `CompSyntheraSpawner.ReceiveCompSignal("PowerTurnedOff" / "FlickedOff")` → despawn.
- Heat explosion: `CompHeatRisk.TriggerExplosion()` calls `parent.GetComp<CompSyntheraSpawner>().DespawnFormgel(explode: true)` directly.
- Ring: `CompNeomaRing` operates independently; uses `SyntheraUtils.SetupPawn()` (not `SetupFormgel()` — be aware of the divergence).

### RimWorld XML rules (1.6)
- `ThingDef` defNames **must not end in a digit** — use Roman numerals (TierI, TierII, …).
- `DesignationCategoryDef` `<order>` is inverse: lower number = appears later/rightmost. Neoma uses `<order>10` to appear last.
- Buildings with `<researchPrerequisites>` disappear from the architect panel automatically until researched — no Harmony patch needed for this.
