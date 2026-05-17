# SyntheraCore — Roadmap de Próximos Passos

**Última atualização:** 2026-05-15  
**Estado atual:** Tiered aux modules implementados (20 buildings, 11 research projects, 15 hediffs, 4 research lines separadas).

---

## Prioridade Alta

### 1. Texturas para os Módulos Auxiliares

**Problema:** Todos os 20 buildings auxiliares usam o mesmo placeholder `Things/Building/Tier01/Tier01`.

**O que fazer:**
- Criar texturas distintas por tipo de módulo (Combat, Work, Optimizer, Role)
- Idealmente 1 textura por tier também (pode ser recolor/variação da base)
- Atualizar `<graphicData>` em `Buildings_SyntheraAux.xml` para cada grupo de buildings
- Sugestão de paths:
  - `Things/Building/AuxCombat/AuxCombatTierI`
  - `Things/Building/AuxWork/AuxWorkTierI`
  - `Things/Building/AuxOptimizer/AuxOptimizerTierI`
  - `Things/Building/AuxRole/AuxRoleTierI`

**Complexidade:** Arte — sem código.

---

### 2. Localização (Keyed Strings)

**Problema:** As pesquisas e buildings dos módulos auxiliares têm labels/descriptions inline no XML. Os altares principais usam keyed strings (`SyntheraCore_NeomaTier1_Label` etc.) mas os módulos não.

**O que fazer:**
- Criar `1.6/Languages/English/Keyed/Keyed_AuxModules.xml`
- Mover todos os `<label>` e `<description>` das ThingDefs e ResearchProjectDefs dos módulos para chaves
- Isso abre o mod para tradução por outros usuários

**Exemplo de estrutura:**
```xml
<SyntheraAuxCombatTierI_Label>combat uplink node I</SyntheraAuxCombatTierI_Label>
<SyntheraAuxCombatTierI_Desc>A basic auxiliary uplink...</SyntheraAuxCombatTierI_Desc>
<AuxCombatTierI_ResearchLabel>combat uplink I</AuxCombatTierI_ResearchLabel>
```

**Complexidade:** Baixa — só XML e strings.

---

### 3. Inspect String mais rica no CompAuxiliaryModule

**Problema:** O inspect string atual mostra apenas `"Aux module: linked to [altar] [status]"`. Não informa o que o módulo está fazendo.

**O que fazer** em `CompAuxiliaryModule.CompInspectStringExtra()`:
- Combat: mostrar qual hediff está ativo e os valores de bônus
- Optimizer: mostrar percentual de redução de cooldown e taxa de stress relief/day
- Work: listar os work types desbloqueados
- Role: mostrar qual role e tier estão ativos

**Exemplo:**
```
Aux module: linked to tier II altar [active]
  ↳ Cooldown -50% | Stress relief 0.035/day
```

**Complexidade:** Média — só C#, sem novos sistemas.

---

## Prioridade Média

### 4. Gizmo no Altar mostrando Módulos Ativos

**Problema:** O jogador não tem feedback visual no altar sobre quais módulos auxiliares estão linkados e ativos.

**O que fazer** em `CompSyntheraSpawner.CompGetGizmosExtra()`:
- Adicionar um `Command_Action` informativo (não clicável, apenas exibição) listando os módulos ativos
- Exemplo: `"Active modules: Combat II, Optimizer I"`
- Ler de `RegisteredAuxTypes` (que já existe)

**Complexidade:** Baixa — C# puro, sem novos sistemas.

---

### 5. Migração de Saves Antigos (Hediffs Órfãos)

**Problema:** Saves criados com a versão antiga do mod têm hediffs como `SyntheraRoleSoldier`, `SyntheraRoleWorker`, `SyntheraAmpBuff`, `SyntheraStressRelief` nos pawns. Esses defNames não existem mais.

**O que fazer:**
- Opção A (simples): Não fazer nada — RimWorld remove hediffs com def null automaticamente na próxima vez que o save carrega, com um warning no log. Funcional mas gera log noise.
- Opção B (limpa): Adicionar no `SyntheraCoreInit` um `[StaticConstructorOnStartup]` que verifica todos os pawns no mapa e remove hediffs com defName antigo.
- **Recomendação:** Opção A por agora, Opção B antes do release público.

**Complexidade:** Baixa (Opção B).

---

### 6. Balanceamento de Numbers

**Os números atuais precisam de validação in-game:**

| Módulo | Parâmetro | Valor atual | Verificar |
|--------|-----------|-------------|-----------|
| Optimizer I | stressReliefPerDay | 0.015 | SystemStress acumula 0.033/day — Tier I deveria reduzir ~45% do acúmulo líquido? |
| Optimizer II | stressReliefPerDay | 0.035 | ~100% do acúmulo bruto — avatar em plateau? |
| Optimizer III | stressReliefPerDay | 0.070 | ~200% — stress vai cair ativamente, ok? |
| Optimizer III | respawnMultiplier | 0.25 | 75% de redução — talvez forte demais? |
| Combat III | MeleeDPS | ×1.30 | Empilha com SyntheraConsciousness? |
| Role Soldier III | MoveSpeed | ×1.10 | Empilha com AmpBuff III (×1.18)? |

**O que fazer:** Testar in-game com cada tier e ajustar. Os multiplicadores de MoveSpeed especialmente podem empilhar de forma não intencional.

---

### 7. Limite de Módulos por Altar — Feedback mais Claro

**Problema:** Quando o jogador tenta colocar um segundo módulo do mesmo tipo perto do mesmo altar, recebe uma `Messages.Message` discreta. Fácil de perder.

**O que fazer:**
- Adicionar `disabled = true` e `disabledReason` ao gizmo de construção do altar, mostrando quais slots estão ocupados
- Ou: desabilitar a construção do segundo módulo com uma razão clara no architect panel
- Alternativa mais simples: mudar o `MessageTypeDefOf.RejectInput` para um `Letter` que aparece na lista de cartas

**Complexidade:** Média.

---

## Prioridade Baixa / Futuro

### 8. Sistema de Energia Compartilhada (opcional)

**Ideia:** Altar de Tier IV consome menos energia porque o avatar é mais eficiente. Módulos auxiliares conectados poderiam reduzir o consumo do altar em vez de cada um ter seu próprio consumo.

**Complexidade:** Alta — requer novo sistema de comp communication. Baixa prioridade.

---

### 9. Hediffs de Tier com Ícones Distintos

**Problema:** Todos os hediffs de role/combat usam o ícone padrão de hediff do RimWorld.

**O que fazer:**
- Criar texturas de ícone para cada tipo de módulo (`UI/Icons/...`)
- Adicionar `<defaultLabelColor>` e `<spawnThingOnRemoved>` onde fizer sentido
- Adicionar `<hediffGivers>` para integrações com o Health tab

**Complexidade:** Arte + XML.

---

### 10. Efeitos Sonoros

**O que fazer:**
- Som ao ativar/desativar módulo (link com altar encontrado/perdido)
- Som ao entrar em hibernação
- Reutilizar sons do RimWorld ou criar novos

**Relevante para:** `CompAuxiliaryModule.ApplyEffects()`, `CompSyntheraSpawner` death path.

**Complexidade:** Média (precisa de assets de áudio ou escolha de sons existentes).

---

### 11. Aba Custom no Health Tab (via Harmony)

**Ideia:** Mostrar System Stress, módulos ativos, e status de hibernação diretamente na aba Health do pawn avatar, em vez de só no inspect string do altar.

**Requer Harmony:** `ITab_Pawn_Health.FillTab` Postfix.

**Complexidade:** Alta. Só vale implementar depois de toda a lógica estar estável.

---

## O que NÃO vamos fazer

- **Harmony para esconder a aba Neoma** — removido anteriormente, resolvido por `researchPrerequisites`
- **SyntheraStressRelief hediff** — substituído por manipulação direta de severidade no Optimizer
- **Buildings antigos sem tier** (`SyntheraAuxAmplifier`, `SyntheraAuxOptimizer` etc.) — substituídos pelos tiered
- **Linkagem manual** entre módulo e altar — sistema de proximidade automática é mais fluido

---

## Estado do Build

```
dotnet build → 0 errors, 0 warnings
DLL: Assemblies/SyntheraCore.dll
```

## Arquivos Chave

| Arquivo | Responsabilidade |
|---------|-----------------|
| `Source/1.6/Comp/CompSyntheraSpawner.cs` | Estado do altar, spawn/despawn, hibernação, gizmos |
| `Source/1.6/Comp/CompAuxiliaryModule.cs` | Lógica dos módulos auxiliares, stress relief, role strip |
| `Source/1.6/CompProps/CompProperties_AuxiliaryModule.cs` | Enums + propriedades dos módulos |
| `1.6/Defs/HediffDefs/Hediffs_AuxModules.xml` | Hediffs tiered dos módulos (15 defs) |
| `1.6/Defs/ResearchDefs/ResearchProjects_SyntheraAux.xml` | 4 linhas de pesquisa auxiliar (11 projetos) |
| `1.6/Defs/ThingDefs_Buildings/Buildings_SyntheraAux.xml` | 20 buildings tiered |
