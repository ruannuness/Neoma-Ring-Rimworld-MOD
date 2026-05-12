# Plano de Melhoria para o Mod Formgel Core

Este documento descreve um plano para melhorar o mod Formgel Core, com foco em refatoração, customização e correção de bugs. As mudanças propostas visam tornar o mod mais robusto, flexível e fácil de manter.

## 1. Refatoração do `CompFormgelSpawner` para Customização via XML

**Problema:** Existe uma inconsistência crítica entre o código C# (`CompFormgelSpawner.cs`) e as definições XML (`Buildings_Neoma.xml`). O C# atualmente ignora as propriedades `pawnKind`, `spawnIntervalDays` e `maxPawnsToSpawn` definidas no XML, em vez disso, deriva o `pawnKind` do `defName` do edifício.

**Solução:**

- **Modificar `CompFormgelSpawner.cs`:**
    - Alterar `GenerateFormgelPawn()` para usar `Props.pawnKind` para determinar qual peão gerar.
    - Implementar uma lógica de spawn automático em `CompTick()` que use `Props.spawnIntervalDays` e `Props.maxPawnsToSpawn`.
    - Remover ou adaptar os métodos `GetBuildingTier()` e `GetPawnKindForTier()` para não dependerem mais de "TierX" no `defName`. A configuração de habilidades e trabalho deve ser movida para o XML, se possível.
    - Utilizar `Props.spawnSound` ao gerar o peão.

- **Atualizar `Buildings_Neoma.xml`:**
    - Garantir que todas as `CompProperties_FormgelSpawner` tenham `pawnKind`, `spawnIntervalDays` e `maxPawnsToSpawn` corretamente configurados.

## 2. Correção de Inconsistências de Hediff

**Problema:** O código C# tenta adicionar um `HediffDef` chamado `FormgelConsciousness`, mas o XML define `FormgelHediff`. Isso causará erros. Além disso, o `NeomaBiolock` é definido, mas nunca usado.

**Solução:**

- **Renomear `HediffDef`:** Em `1.6/Defs/HediffDefs/Hediffs_FormgelCore.xml`, renomear `<defName>FormgelHediff</defName>` para `<defName>FormgelConsciousness</defName>`.
- **Implementar `NeomaBiolock`:**
    - Em `CompNeomaRing.cs`, na função `Notify_Equipped`, adicionar lógica para aplicar o `NeomaBiolock` `HediffDef` ao peão que equipa o anel.
    - Adicionar lógica para remover o `HediffDef` se o anel for desequipado.

## 3. Melhorias no `CompNeomaRing`

**Problema:** O `CompNeomaRing` tem o `pawnKind` "NeomaPawn" hardcoded e não possui lógica para despawnar o Neoma quando o anel é desequipado.

**Solução:**

- **Adicionar Propriedade `pawnKind`:**
    - Em `CompProps_NeomaRing.cs`, adicionar `public string pawnKind;`.
    - Em `Apparel_Neoma.xml`, configurar o `pawnKind` para o `CompProperties_NeomaRing`.
    - Em `CompNeomaRing.cs`, usar `Props.pawnKind` em vez de "NeomaPawn".
- **Implementar Despawn:**
    - Em `CompNeomaRing.cs`, sobrescrever o método `Notify_Unequipped` para despawnar o Neoma.
    - Considerar o que acontece se o peão que está usando o anel morrer.

## 4. Localização

**Problema:** A maioria dos `label`s e `description`s está em português, o que dificulta a tradução e o suporte a múltiplos idiomas.

**Solução:**

- **Criar Chaves de Localização:**
    - Mover todos os textos visíveis para o usuário (labels, descriptions, messages) para `1.6/Languages/English/Keyed/FormgelCore.xml`.
    - Substituir os textos hardcoded nos arquivos XML por chaves de localização (ex: `<label>MyMod_BuildingName</label>`).

## 5. Balanceamento e Configuração do `CompHeatRisk`

**Problema:** Apenas o `NeomaAltarTier1` tem `CompHeatRisk`, o que pode ser um descuido de design.

**Solução:**

- **Decidir sobre a Aplicação do Risco de Calor:**
    - Determinar se o risco de calor deve se aplicar a todos os altares, ou se é uma característica intencional do Tier 1.
    - Se for para ser aplicado a outros tiers, adicionar o `CompProperties_HeatRisk` aos `ThingDef`s correspondentes em `Buildings_Neoma.xml`.

## 6. Melhorias Gerais na Qualidade do Código

**Problema:** Existem números mágicos e código duplicado que podem ser melhorados.

**Solução:**

- **Remover Números Mágicos:**
    - Em `CompHeatRisk.cs`, expor valores como `CHECK_INTERVAL` e o multiplicador de chance de explosão como propriedades em `CompProps_HeatRisk.cs`.
- **Refatorar Código Duplicado:**
    - Criar um método utilitário estático (ex: `FormgelUtils.SetupPawn(Pawn pawn)`) que lide com a remoção de necessidades e aplicação do hediff `FormgelConsciousness`.
    - Chamar este método de `CompFormgelSpawner.cs` e `CompNeomaRing.cs`.

## Diagrama de Fluxo Proposto para `CompFormgelSpawner`

```mermaid
graph TD
    A[Edifício com CompFormgelSpawner] -- CompTick --> B{Deve Gerar Formgel?};
    B -- Sim (baseado em spawnIntervalDays & maxPawnsToSpawn) --> C[GenerateFormgelPawn (usando Props.pawnKind)];
    C --> D[SpawnFormgel];
    D --> E[Formgel Gerado no Mapa];
    E -- Energia perdida ou Edifício destruído --> F[DespawnFormgel];
    F -- Se explode=true --> G[Explosão & Timer de Respawn Definido];
    H[Gizmo para Mudar Cor] --> I[Atualizar Cor do Formgel];
```
