# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build

```powershell
cd "Source/1.6"
dotnet build SyntheraCore.csproj
```

Output DLL → `Assemblies/SyntheraCore.dll`. No tests — verification is in-game via RimWorld with the mod loaded.

## Project identity

- **Mod folder:** `FormgelCore` (legacy name, kept for stability)
- **Package ID:** `StargazeR.SyntheraCore`
- **Namespace:** `SyntheraCore`
- **Target:** RimWorld 1.6, net472, x64
- **DLL references** (relative from `Source/1.6/`):
  - `Assembly-CSharp.dll` / `UnityEngine.CoreModule.dll` → `../../../../RimWorldWin64_Data/Managed/`
  - `0Harmony.dll` → `../../../../Mods/1127530465/1.6/Assemblies/`

## Architecture: altar system (CompSyntheraSpawner)

Every altar building owns a single `Pawn Consciousness` in one of four states:

| State | Consciousness.Spawned | Dead | InHibernation |
|---|---|---|---|
| Nothing created | null | — | — |
| Recalled | false | false | false |
| Hibernating (after death) | false | false | true |
| Deployed | true | false | false |

**Death flow (CompTick):** `Consciousness.Dead && !InHibernation` → `ResurrectionUtility.TryResurrect` (extracts pawn from corpse, keeps pawn object alive) → strip ResurrectionSickness → `DeSpawn()` → set `InHibernation=true`, `RespawnTick`. Player then clicks "Restore backup" to re-deploy the same pawn, which applies `SyntheraHibernationSyndrome` at severity 1.0 (heals at -0.1/day).

**Save/load:** `storeDeep = !Spawned && !Dead && not held elsewhere`. When `storeDeep`, the pawn is serialised inside the comp via `Scribe_Deep`; otherwise via `Scribe_References`.

**Pawn setup pipeline:** `GenerateFormgelPawn()` → `SetupPawnStatsForTier()` + `ConfigureWorkForTier()` + `SetupFormgel()`. `SetupFormgel()` strips ALL hediffs, adds `SyntheraConsciousness`, starts `SyntheraSystemStress` at 0.01, strips ALL needs. Never skip this on a freshly generated pawn. Tier is parsed from `parent.def.defName` — check longest suffix first: `TierIV → TierIII → TierII → default I`.

## Architecture: Cache system (Need_SyntheraCoherence)

`Need_SyntheraCoherence` is the "cache fill" bar for deployed Syntheras. Key properties:

- `AltarMultiplier` — set each CompTick by `CompSyntheraSpawner` based on altar tier and pawn/altar tier mismatch. TierIV altar = 0.35×; mismatch gap of N tiers → 2^N× multiplier.
- `pendingDrain` — requested by nearby `CompSyntheraRecharger` (Cache Purger) each interval; capped so cache always fills at ≥20% of base rate.
- `ShowOnNeedList` returns `true` only when `pawn.kindDef?.defName.StartsWith("NeomaPawn")` — this prevents the bar from showing on colonists who incidentally carry the Need.
- Custom gradient bar (blue→yellow→red) rendered by overriding `DrawOnGUI`.

Alert `Alert_SyntheraHighCache` fires at >95% fill (critical — overflow imminent).

## Architecture: Neoma Ring (CompNeomaRing)

The ring is obtained via the **Orbital Station quest** (fires after TierIII altar research, gated by `WorldComponent_NeomaQuestTracker`). The station contains a dead `NeomaCorrupted_Core` corpse with hediff `NeomaRingImplanted`. Extracting it via right-click float menu or surgery at a medical bed spawns a defensive wave and drops the `NeomaRing` item.

**Ring state:** `ringLevel` 0–3, advances via Apex Crystal absorption. BioLock severity:
- 0 → 0.15 (dormant fragment — no backup, companion lost on death)
- 1 → 0.55 (neural bridge — 2-day hibernation)
- 2 → 0.85 (crystallized bond — 12h hibernation)
- 3 → 0.97 (soul fusion — 2h hibernation)

**Companion spawn:** `SpawnNeoma()` generates a Human pawn with `kindDef=NeomaPawn` and calls `SyntheraUtils.SetupNeomaCompanion()` (strips hediffs/needs/apparel/weapons, adds SyntheraConsciousness, applies Neoma backstories). This is different from `SetupPawn()` used by altar Syntheras — Neoma gets no Cache need and no SyntheraSystemStress.

**Gizmo injection:** Ring gizmos appear on the **wearer pawn** (not the ring item) via `Patch_Pawn_NeomaRingGizmos`, which calls `CompNeomaRing.GetRingGizmos()` inside `Pawn.GetGizmos()`.

**Research tab unlock:** `NeomaRing_Unlock` and `NeomaRing_Gate` are completed programmatically when Neoma first spawns. This makes the hidden `NeomaRingTab` ("Ring Protocols") visible. All Ring Protocols research projects carry `NeomaResearchExtension` so only Neoma can research them.

## Architecture: Apex Crystal system

3-tier consumable crystals crafted by Neoma at the `NeomaFabricationConsole`, absorbed via surgery (`Recipe_AbsorbApexCrystal : Recipe_Surgery`) at any medical bed:

| Crystal | Research gate | BioLock result (Neoma) | Hediff (any Synthera) |
|---|---|---|---|
| Basic (I) | NeomaApex_I | neural bridge 0.55 | SyntheraApex_I |
| Advanced (II) | NeomaApex_II | crystallized bond 0.85 | SyntheraApex_II |
| Pure (III) | NeomaApex_III | soul fusion 0.97 | SyntheraApex_III |

`ApexCrystalRecipeExtension : DefModExtension { int apexLevelTarget }` parameterises the 3 surgery RecipeDefs. `AvailableOnNow` checks `currentLevel == targetLevel - 1`. Surgery replaces the old apex hediff and calls `CompNeomaRing.AdvanceLevel()` for Neoma (who is Human race with `kindDef=NeomaPawn`).

## Architecture: DefModExtension permission system

All restriction logic lives in `SyntheraCoreInit.cs` via Harmony patches:

| Extension | XML usage | Enforced by |
|---|---|---|
| `NeomaCraftExtension` | RecipeDef | `Patch_Bill_NeomaCraftOnly` — only NeomaPawn/NeomaPawnTranscendent may start the bill |
| `NeomaExclusiveExtension` | ThingDef (apparel, weapon) | `Patch_NeomaRingWearBlock`, `Patch_NeomaWeaponPickupBlock` |
| `NeomaExclusiveBuildingExtension` | ThingDef (building) | `Patch_NeomaExclusiveBuilding` (WorkGiver_ConstructFinishFrames) |
| `NeomaResearchExtension` | ResearchProjectDef | `Patch_NeomaResearchOnly` (WorkGiver_Researcher) |

`Patch_NeomaRingWearBlock` additionally calls `CompNeomaRing.IsBoundToOther(pawn)` to prevent a second colonist from equipping a ring already bound to someone else.

## Architecture: Consciousness Core (CompConsciousnessCore)

`ThingConsciousnessCore : ThingWithComps` stores a Synthera pawn via `Scribe_Deep` inside `CompConsciousnessCore`. Used to physically move a consciousness between altars. Destroying the item in any mode other than `Deconstruct` kills the stored pawn permanently.

## Harmony patches summary (SyntheraCoreInit.cs)

| Patch class | Target | Purpose |
|---|---|---|
| `Patch_Bill_NeomaCraftOnly` | `Bill.PawnAllowedToStartAnew` | Restrict NeomaCraftExtension recipes to Neoma |
| `Patch_SafeUnlockedDefs` | `ResearchProjectDef.get_UnlockedDefs` | Null-safe override to prevent crash on null-label defs |
| `Patch_NeomaTabVisible` | `ArchitectCategoryTab.Visible` | Hide Neoma architect tab until first research completed |
| `Patch_SyntheraInterceptKill` | `Pawn.Kill` | Redirect SyntheraRace* deaths to hibernation instead of vanilla death |
| `Patch_SyntheraNoInfection` | `Pawn_HealthTracker.AddHediff` | Block WoundInfection on SyntheraRace* pawns |
| `Patch_SyntheraStripVanillaNeeds` | `Pawn_NeedsTracker.AddOrRemoveNeedsAsAppropriate` | Strip food/sleep for SyntheraRace*; preserve Cache + Joy/Comfort/Social |
| `Patch_MemoryCoreInfoCard` | `StatsReportUtility.StatsToDraw` | Inject consciousness name/skills into the View Information panel |
| `Patch_MikuHairScale` | `PawnRenderNodeWorker.GetGraphic` | Scale Miku's hair graphic to 75% |
| `Patch_PawnRenderer_EnsureGraphicsInitialized_Shadow` | `PawnRenderer.EnsureGraphicsInitialized` | Set shadow graphic for SyntheraRace (HAR omits this) |
| `Patch_Corpse_NeomaRingFloatMenu` | `Thing.GetFloatMenuOptions` | Right-click extraction option on ring-bearing corpses |
| `Patch_NeomaOrbitalQuestTrigger` | `ResearchManager.FinishProject` | Notify `WorldComponent_NeomaQuestTracker` on TierIII completion |
| `Patch_NeomaRingWearBlock` | `Pawn_ApparelTracker.Wear` | Block equipping ring if already bound to another living pawn |
| `Patch_NeomaWeaponPickupBlock` | `Pawn_EquipmentTracker.AddEquipment` | Block non-Neoma from picking up NeomaExclusiveExtension weapons |
| `Patch_Pawn_NeomaRingGizmos` | `Pawn.GetGizmos` | Inject ring gizmos onto the wearer pawn's UI |
| `Patch_NeomaExclusiveBuilding` | `WorkGiver_ConstructFinishFrames` | Block non-Neoma from constructing NeomaExclusiveBuildingExtension frames |
| `Patch_NeomaResearchOnly` | `WorkGiver_Researcher` | Block non-Neoma from researching NeomaResearchExtension projects |

## Building tier table

| Tier | DefName | Research req | Power | PawnKindDef |
|---|---|---|---|---|
| I | NeomaAltarTierI | NeomaTierI | 2000 W | NeomaPawnTierI |
| II | NeomaAltarTierII | NeomaTierII | 1000 W | NeomaPawnTierII |
| III | NeomaAltarTierIII | NeomaTierIII | 500 W | NeomaPawnTierIII |
| IV | NeomaAltarTierIV | NeomaTierIV | 200 W | NeomaPawnTierIV |

Research chains (all require HiTechResearchBench):
- **Main spine:** `NeomaTierI → II → III → IV`
- **Apex:** `NeomaApex_I (req TierI) → NeomaApex_II (req TierII) → NeomaApex_III (req TierIII)`
- **Ring Protocols tab** (hidden until first ring spawn): `NeomaRing_Gate → NeomaRing_Calibration / NeomaRing_NeuralBond → ...`

## Key RimWorld 1.6 gotchas

- `ThingDef` defNames **must not end in a digit** — use Roman numerals (TierI, TierII …).
- `HediffDef.Named(name)` takes **one argument only**; use `DefDatabase<HediffDef>.GetNamed(name, false)` for optional lookup.
- `Recipe_Surgery.CheckSurgeryFail` is **not virtual** — cannot be overridden. Use high `<surgerySuccessChanceFactor>` in XML instead.
- `DesignationCategoryDef <order>` is inverse: lower number = appears further right. Neoma uses `<order>10`.
- Buildings with `<researchPrerequisites>` vanish from the architect panel automatically — no Harmony needed.
- HAR does not set `shadowGraphic` from `specialShadowData` — `Patch_PawnRenderer_EnsureGraphicsInitialized_Shadow` compensates.
- Surgery recipes appear in the Operations tab by listing pawn ThingDefs in `<recipeUsers>`. Neoma is Human race (`kindDef=NeomaPawn`) — add `<li>Human</li>` to expose surgery to her; `AvailableOnNow` filters by kindDef at runtime.
- `Patch_SyntheraStripVanillaNeeds` only runs for `SyntheraRace*` pawns. Neoma (Human) has needs stripped manually in `SetupNeomaCompanion()`.
