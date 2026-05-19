# SyntheraCore — Documento de Progresso

> **Para Claude:** Consulte este arquivo no início de cada sessão. Atualize "Estado do Build" após cada compilação, "Histórico" ao final de cada sessão, e mova itens entre as seções conforme o progresso avança.

---

## Visão Geral

**Package ID:** `StargazeR.SyntheraCore`  
**Mod folder:** `FormgelCore` (nome legado mantido por estabilidade)  
**Target:** RimWorld 1.6, .NET Framework 4.7.2

O mod gira em torno de **avatares digitais (Syntheras)** invocados por altares construídos pelo jogador. Syntheras são projeções de consciência artificial — não comem, não dormem, acumulam stress de sistema ao ficarem ativos, e não deixam cadáver ao morrer (o corpo dissolve, apenas equipamentos e inventário são droppados).

**Inspiração central:** O **Neoma Ring** — um item único de masmorra (não craftável) que convoca uma companion IA de alto nível. O Ring pode ser calibrado progressivamente para melhorar a gestão do companion após a morte.

---

## Estado do Build

| Campo | Valor |
|-------|-------|
| Última compilação | 2026-05-18 (sessão 3 — Fase A) |
| Status | ✅ Sucesso — 0 erros, 0 avisos |
| DLL gerada | `Assemblies/SyntheraCore.dll` |
| Projeto | `Source/1.6/SyntheraCore.csproj` |
| Comando | `cd Source/1.6 && dotnet build SyntheraCore.csproj` |

---

## Sistemas Implementados

### Persona Core

- [x] **`SyntheraPersonaCore`** — item consumível necessário para construir todos os altares; não pode ser comprado/vendido; só fabricado pela companion
- [x] **`NeomaFabricationConsole`** — bancada 2×1, 400W, desbloqueada por `NeomaRingCalibrationI`; apenas a companion do Ring pode operar
- [x] **Restrição via Harmony** — `Patch_Bill_NeomaCraftOnly` bloqueia qualquer pawn que não seja `kindDef == "NeomaPawn"` em receitas com `NeomaCraftExtension`
- [x] **Receita `NeomaMake_PersonaCore`** — 2× ArchotechShard + 20× Ouro + 15× Plasteel → 1× Persona Core; prereq: `NeomaRingCalibrationI`
- [x] **Custos por tier**: Tier I=1, Tier II=2, Tier III=4, Tier IV=8, Miku Stage=3 Persona Cores

### Core

- [x] **Geração de avatar** — `CompSyntheraSpawner.GenerateFormgelPawn()` cria pawn com raça SyntheraRace, estatísticas por tier, hediffs base
- [x] **Deploy / Recall** — `SpawnFormgel()` / `DespawnFormgel()` com reset de stress no recall
- [x] **Hibernação** — ao morrer em campo, avatar é ressuscitado, despawnado, entra hibernação com timer; restaurado manualmente via gizmo
- [x] **Morte sem cadáver** — ao morrer, apparel e inventário droppam na posição da morte, cadáver destruído (`DestroyMode.Vanish`)
- [x] **Sistema de stress** — `SyntheraSystemStress` acumula +0.033/dia; penalidades em 0.3 / 0.6 / 0.9 de severidade; zerado no recall
- [x] **Recriação de avatar** — descarta o atual e gera um novo (cooldown de 3 dias)
- [x] **Renomear avatar** — dialog in-game para nome customizado
- [x] **Exportar / Importar consciência** — embala o pawn em `SyntheraMemoryCore` para transporte entre altares

### Altares (Tiers I–IV)

| Altar | Energia | Pawn | Slots Aux | Recall cooldown | Persona Cores |
|-------|---------|------|-----------|----------------|---------------|
| Tier I | 2000 W | NeomaPawnTierI | 2 | ~2h | 1 |
| Tier II | 1000 W | NeomaPawnTierII | 4 | ~8h | 2 |
| Tier III | 500 W | NeomaPawnTierIII | 6 | 3 dias | 4 |
| Tier IV | 200 W | NeomaPawnTierIV | 8 | **5 dias** (era 30) | 8 |

- [x] **Risco de calor** — `CompHeatRisk`: Tier I explode se temperatura ultrapassar limiar (explosão + slime)
- [x] **Emergency Recall** (todos os tiers) — teleporta avatar instantaneamente ao altar, cooldown 1 dia
- [x] **Maintenance Pulse** (Tier II+) — reduz stress em 0.25 imediatamente, consome 5× Aço, cooldown 3 dias
- [x] **Signal Burst** (Tier III+) — dobra o raio de detecção de todos os módulos aux conectados por 24h

### Módulos Auxiliares (7 tipos × 3 tiers + Archotech)

Módulos 1×1 colocados próximos ao altar; escaneiam por proximidade e aplicam efeitos ao avatar ativo.

| Tipo | Efeito |
|------|--------|
| Combat Amplifier | Bônus de combate progressivo |
| Core Optimizer | Reduz cooldowns + alivia stress |
| Role Specializer | Aplica papel (Soldier / Worker / Medic / Scout) |
| Work Unlocker | Desbloqueia tipos de trabalho |
| Armor Matrix | Redução de dano (15% → 85%) |
| Overclock Core | Bônus de trabalho intenso + stress acelerado |
| Versatility Protocol | Buffs gerais equilibrados |

- [x] **Cap de slots** — respeita `maxAuxModules + BonusAuxSlots` (slot expanders aumentam o limite)
- [x] **Un único por tipo** — cada tipo de módulo só pode estar ativo uma vez por altar (exceto Role Specializer até `maxRoleModules`)

### Upgrades Estruturais dos Altares

Construções 1×1 que afetam o ALTAR, não o avatar.

| Building | Efeito | Research |
|----------|--------|----------|
| AltarSlotExpander | +2 slots de módulo aux | AltarEnhancementI |
| AltarRespawnBooster | ×0.7 nos cooldowns de spawn | AltarEnhancementI |
| AltarCoolingArray | −60% saída de calor | AltarEnhancementI |
| AltarPowerCondenser | −30% consumo de energia | AltarEnhancementII |

### Neoma Ring (Sistema de Calibração — Base para Fase B: Resonance Protocol)

Item **único de masmorra** (não craftável). Convoca companion IA de nível máximo. Progressão por calibração do item único.

| Nível | Como Obter | Morte do Companion |
|-------|-----------|-------------------|
| 0 — Bruto | Drop de masmorra | **Hibernação 2 dias** (era: vínculo rompido) |
| 1 — Calibração I | Pesquisa + 5× Archotech Shard + 50× Ouro + 100× Plasteel | Hibernação 2 dias |
| 2 — Calibração II | Pesquisa + 10× Archotech Shard + 100× Ouro + 5× Componente Especial | Backup completo + HibernationSyndrome |

- [x] **BioLock permanente** — `NeomaBiolock` hediff não decai nem é removido ao desequipar; só desaparece quando o portador morre
- [x] **BioLock evolui com o anel** — 6 estágios de severity (L0: −5% Consciousness → L5: +20% Moving +10% Consciousness +10% Manipulation)
- [x] **Rastreamento do portador** — `_wearer` salvo via `Scribe_References`; CompTick detecta morte do portador
- [x] **Morte do portador** → `UnbindFromWearer()`: remove BioLock do cadáver, companion despawnado, anel liberado para novo portador
- [x] **Companion nunca morre permanentemente** — sempre hiberna (qualquer nível de calibração)
- [x] **Recall do companion** — gizmo para teleportar o companion de volta ao portador
- [x] **Gizmos de calibração** — aparecem quando pesquisa concluída; atualizam severity do BioLock ao calibrar

### Holographic Stage (Hatsune Miku)

- [x] Estrutura 5×5 que invoca Hatsune Miku como avatar Synthera especial
- [x] Requer pesquisa própria `MikuStageResearch` (desbloqueada por NeomaTierII)
- [x] Forçado: nome "Hatsune Miku", cabelo twintails teal, habilidades Social/Artístico elevadas, traço Kind

### Árvore de Pesquisa

```
X=-2 (Calibração do Ring)        X=0 (Espinha)    X=1 (Lateral)         X=2-14 (Módulos Aux)
(-2,1) NeomaRingCalibrationI  ←  (0,1) NeomaTierII → (1,1) MikuStageResearch
(-2,2) NeomaRingCalibrationII ←  (0,2) NeomaTierIII  (1,2) AltarEnhancementI
                                  (0,3) NeomaTierIV   (1,3) AltarEnhancementII
```

### Outros Itens

- [x] **SyntheraArchotechShard** — recurso raro, custo de calibração do ring e construção de módulos Archotech
- [x] **SyntheraMemoryCore** — item portátil que armazena uma consciência; inflamável; destruído = consciência perdida
- [x] **SyntheraArchotechTerminal** — bancada de pesquisa 2×2, ×2.0 de velocidade de pesquisa, custa 10× Archotech Shard

---

## Arquitetura — Arquivos Críticos

```
FormgelCore/
├── Assemblies/SyntheraCore.dll          ← DLL compilada (output do build)
├── PROGRESSO.md                         ← Este arquivo
├── Source/1.6/
│   ├── SyntheraCore.csproj
│   ├── SyntheraCoreInit.cs              ← StaticConstructorOnStartup; Harmony patch de shadow graphic
│   ├── LifeStageWorker_SyntheraAdult.cs ← Evita crash do HAR ao inicializar pawn
│   ├── Comp/
│   │   ├── CompSyntheraSpawner.cs   ← [CENTRAL] Todo o ciclo de vida do altar
│   │   ├── CompAuxiliaryModule.cs   ← Módulos aux: scan + apply/remove effects
│   │   ├── CompAltarUpgrade.cs      ← Upgrades estruturais (SlotExpander etc.)
│   │   ├── CompHeatRisk.cs          ← Risco de explosão por calor (Tier I)
│   │   ├── CompNeomaRing.cs         ← Anel de companion com calibração
│   │   └── CompConsciousnessCore.cs ← Item de transporte de consciência
│   ├── CompProps/
│   │   ├── CompProperties_SyntheraSpawner.cs
│   │   ├── CompProperties_AuxiliaryModule.cs
│   │   ├── CompProperties_AltarUpgrade.cs
│   │   ├── CompProperties_ConsciousnessCore.cs
│   │   ├── CompProps_HeatRisk.cs
│   │   └── CompProps_NeomaRing.cs
│   └── Utils/SyntheraUtils.cs           ← SetupPawn() compartilhado (ring usa este, não SetupFormgel)
└── 1.6/
    ├── Defs/
    │   ├── ThingDefs_Buildings/
    │   │   ├── Buildings_Neoma.xml          ← 4 altares (TierI–IV)
    │   │   ├── Buildings_Miku.xml           ← Holographic Stage
    │   │   ├── Buildings_SyntheraCore.xml   ← ⚠ LEGADO REMOVIDO (arquivo esvaziado)
    │   │   ├── Buildings_SyntheraAux.xml    ← Todos os módulos aux (38 defs)
    │   │   ├── Buildings_AltarUpgrades.xml  ← 4 upgrades estruturais
    │   │   └── Buildings_ArchotechTerminal.xml
    │   ├── ThingDefs_Items/
    │   │   ├── Items_ArchotechShard.xml
    │   │   └── Items_MemoryCore.xml
    │   ├── ThingDefs_Apparel/Apparel_Neoma.xml  ← NeomaRing
    │   ├── ThingDefs_Pawns/
    │   │   ├── PawnKinds_Neoma.xml          ← TierI–IV + Miku + NeomaPawn
    │   │   └── Races_Synthera.xml           ← SyntheraRace (HAR)
    │   ├── HediffDefs/
    │   │   ├── Hediffs_SyntheraCore.xml     ← SyntheraConsciousness, SyntheraSystemStress
    │   │   ├── Hediffs_Neoma.xml            ← NeomaBiolock, HibernationSyndrome
    │   │   └── Hediffs_AuxModules.xml       ← Todos os hediffs de módulo
    │   ├── ResearchDefs/
    │   │   ├── ResearchProjects_Neoma.xml   ← Espinha + Ring + Miku + AltarEnhancement
    │   │   ├── ResearchProjects_SyntheraAux.xml ← Módulos aux
    │   │   └── ResearchTabs_Neoma.xml
    │   └── ...outros (BackstoryDefs, LifeStageDefs, HairDefs, etc.)
    └── Textures/
```

### Como os componentes se conectam

- **Altar → Avatar:** `CompSyntheraSpawner` é o dono exclusivo de `Pawn Consciousness`. Gera, deploya, despawna, serializa.
- **Módulo Aux → Altar:** `CompAuxiliaryModule` escaneia por `CompSyntheraSpawner` num raio, registra-se em `RegisteredAuxTypes`, aplica/remove hediffs no avatar.
- **Upgrade Estrutural → Altar:** `CompAltarUpgrade` escaneia por `CompSyntheraSpawner`, modifica campos transientes (`BonusAuxSlots`, `SpawnIntervalMultiplier`, etc.).
- **Calor → Altar:** `CompHeatRisk` chama `parent.GetComp<CompSyntheraSpawner>().DespawnFormgel(explode: true)` ao superaquecer.
- **Ring → Companion:** `CompNeomaRing` opera independentemente; usa `SyntheraUtils.SetupPawn()` (NÃO `SetupFormgel()` — divergência intencional).
- **Power off → Despawn:** `ReceiveCompSignal("PowerTurnedOff")` no `CompSyntheraSpawner` dispara despawn automático.

---

## Problemas Conhecidos

### 🔴 Crítico

| # | Problema | Arquivo(s) Afetado(s) | Status |
|---|---------|----------------------|--------|
| 1 | **5 texturas de building ausentes** — altares Tier I–IV e Miku Stage exibem ícone/gráfico incorreto no mapa e no menu de construção | `Things/Building/Tier01/Tier01.png`, `Tier02/`, `Tier03/`, `Tier04/`, `Miku/MikuStage.png` | ⏳ Pendente — precisa de arte |

### 🟡 Médio

| # | Problema | Detalhes | Status |
|---|---------|---------|--------|
| 2 | **Neoma Ring sem quest/spawn** — o ring é conceitualmente um drop de masmorra, mas não tem mecânica de drop ainda; precisa ser spawned via dev mode para testar | Futura quest de dungeon | ⏳ Planejado |
| 3 | **CoolingArray e PowerCondenser não afetam os comps reais** — os campos `HeatOutputMultiplier` e `PowerMultiplier` existem no spawner mas `CompHeatPusher` e `CompPowerTrader` não os leem; o efeito visual não se manifesta | `CompAltarUpgrade.cs`, `CompHeatPusher` (vanilla), `CompPowerTrader` (vanilla) | ⏳ Pendente — requer Harmony patch ou substituição de comp |

### 🟢 Baixo / Cosmético

| # | Problema | Status |
|---|---------|--------|
| 4 | Todos os upgrades estruturais usam a textura genérica `Tier01` (herdada de `SyntheraAuxBase`) — diferenciação visual ausente | ⏳ Pendente — arte |
| 5 | `SyntheraMemoryCore` usa textura de `ArchotechShard` como placeholder | ⏳ Pendente — arte |

---

## Próximos Passos

### Alta Prioridade

- [ ] **Texturas dos altares Tier I–IV** — criar/comissionar arte para `Things/Building/Tier01/` até `Tier04/`
- [ ] **Textura do Holographic Stage** — `Things/Building/Miku/MikuStage.png` (5×5)
- [ ] **Implementar efeito real do CoolingArray** — Harmony patch em `CompHeatPusher` ou substituir por comp customizado que leia `HeatOutputMultiplier`
- [ ] **Implementar efeito real do PowerCondenser** — Harmony patch em `CompPowerTrader.Props.basePowerConsumption` ou similar

### Média Prioridade

- [ ] **Quest / spawn do Neoma Ring** — masmorra com boss ou evento de queda que gera o item único
- [ ] **Balanceamento de custos** — revisar custos de construção e pesquisa com save de teste
- [ ] **Localização (pt-BR)** — strings de UI ainda em inglês (labels de gizmos, mensagens)
- [ ] **Textura do Memory Core** — arte própria para `SyntheraMemoryCore`

### Baixa Prioridade / Futuro

- [ ] **Mais papéis de Role Specializer** — ex: Engineer, Crafter, Negotiator
- [ ] **Tier V / Archotech Altar** — altar endgame desbloqueado por `NeomaTierIV`
- [ ] **Synthera Backstories adicionais** — diversificar os textos de Initialization/Deployment
- [ ] **Sons customizados** — atualmente usa `PsychicPulseGlobal` e `Interact_Annihilator` da vanilla

---

## Histórico

### Sessão 2026-05-18 (3) — Fase A: BioLock Permanente + Always-Hibernate
**Implementado:**
- `NeomaBiolock` tornou-se permanente (removida imunizabilidade, trocado `HediffWithComps` → `Hediff`)
- BioLock agora tem **6 estágios de severity** que evoluem com o nível de calibração do anel (L0: −5% Consciousness → L5: +20% Moving +10% Consciousness +10% Manipulation)
- `CompNeomaRing`: adicionados `_wearer` (Scribe_References) e `isBound` para rastrear portador
- Morte do companion **sempre resulta em hibernação** — branch de morte permanente removido completamente
- Morte do portador → `UnbindFromWearer()`: remove BioLock, libera anel para novo vínculo
- BioLock não é mais removido ao desequipar o anel
- Severity do BioLock atualizada automaticamente ao calibrar (`UpdateBiolockSeverity()`)
- Inspect string do anel agora mostra "bound to [nome]" ou "unbound"

**Limpeza:**
- `Buildings_SyntheraCore.xml`: building legado removido (pré-datava o sistema Neoma, sem pesquisa, usava textura de nave vanilla, categoria `Structure`)
- `CompProps_NeomaRing.cs`: removidos imports desnecessários (`System`, `System.Collections.Generic`, `System.Linq`, `UnityEngine`, `RimWorld`)

---

### Sessão 2026-05-18 (2)
**Implementado:**
- Persona Core: novo item `SyntheraPersonaCore` (só fabricável pela companion do Ring)
- `NeomaFabricationConsole`: bancada 2×1 exclusiva para o companion; requer `NeomaRingCalibrationI`
- Receita `NeomaMake_PersonaCore`: 2× ArchotechShard + 20× Ouro + 15× Plasteel
- Harmony patch `Patch_Bill_NeomaCraftOnly`: bloqueia não-NeomaPawn em receitas com `NeomaCraftExtension`
- Adicionado `SyntheraPersonaCore` como requisito de construção para Tier I–IV e Miku Stage
- Fix: `spawnIntervalDays` do Tier IV corrigido de 30 → 5 dias

---

### Sessão 2026-05-18 (1)
**Implementado:**
- P0: Syntheras não deixam cadáver ao morrer — drop de apparel/inventário + `corpse.Destroy(DestroyMode.Vanish)` em `CompSyntheraSpawner` e `CompNeomaRing`
- P1: Pesquisa separada `MikuStageResearch` para o Holographic Stage
- P2: Realinhamento da árvore de pesquisa — Ring Calibration em X=-2, Miku+AltarEnhancement em X=1
- P3: Sistema de calibração do Neoma Ring (níveis 0/1/2, hibernação, gizmos, consumo de materiais)
- P4: Funções ativas dos altares (Emergency Recall, Maintenance Pulse, Signal Burst)
- P5: Transferência de consciência via `SyntheraMemoryCore` (Export/Import gizmos)
- P6: Upgrades estruturais do altar (CompAltarUpgrade + 4 buildings XML)
- Correção: removido parâmetro `forbidItemsOnFail` inválido no RimWorld 1.6
- Criação deste documento (`PROGRESSO.md`)
