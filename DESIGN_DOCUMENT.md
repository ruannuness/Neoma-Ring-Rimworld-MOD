# Projeto Neoma - Design Document

## 📌 Visão Geral
Um mod que permite ao jogador construir **estruturas progressivas** que invocam pawns cada vez mais poderosos. O tier final é um **anel wearable** encontrado em masmorra, que invoca **Neoma** - uma IA super quebrada que não consome energia.

---

## 🎯 Mecânicas Principais

### 1. **Sistema de Tiers (Estruturas Construíveis)**
Cada tier é uma estrutura diferente que o jogador constrói após pesquisa:

| Tier | Estrutura | Energia | Duração | Pawn | Habilidades | Riscos |
|------|-----------|---------|---------|------|-------------|---------|
| 1 | Altar Básico | MUITA (consumo alto) | Pouco tempo (1-2h) | Fraco | Habilidades mínimas | 🔥 **Calor extremo** - Pode explodir em ambientes quentes |
| 2 | Altar Intermediário | Alta | Tempo médio (4-8h) | Médio | Trabalho + combate básico | Médio |
| 3 | Estrutura Avançada | Média | Tempo longo (dias) | Forte | Combate + produção | Baixo |
| 4 | Estrutura Elite | Baixa | Permanente | Muito forte | Multi-disciplinar | Muito baixo |
| 5+ | **ANEL** (wearable) | **NENHUMA** | **Permanente** | **Neoma** | **Tudo + bonus épicos** | **Nenhum** |

#### ⚠️ **Mecânica de Risco (Tier 1)**
- **Produção de Calor**: O altar básico gera calor extremo durante operação
- **Risco de Explosão**: Se operar em ambientes muito quentes (>40°C), chance de explosão
- **Consequências**: Dano à estrutura, fogo no quarto, possível morte do pawn
- **Mitigação**: Construir em áreas frias, usar resfriadores, ou operar em horários frios

### 2. **O Anel (Tier Final)**
- **Encontrado em**: Masmorra customizada
- **Tipo**: Apparel (anel wearable)
- **Mecânica**: 
  - Não consome energia nenhuma
  - Biolock biológico (vinculado ao primeiro usuário)
  - Se o portador morre → Anel fica incontrolável
  - **Super quebrado**: Invoca **Neoma** - IA lendária com habilidades god-like
- **Como usar**: Equipar o anel permite invocar Neoma

### 3. **Sistema de Energia e Calor**
- Estruturas consomem energia elétrica
- Tier 1: Consumo altíssimo (pode sobrecarregar redes) + **produção de calor extremo**
- Tiers intermediários: Consumo decrescente + calor reduzido
- Anel: **0 consumo** (gratuito, quebrado) + **0 calor**

#### 🔥 **Mecânica de Calor (Tier 1)**
- **Produção**: Gera calor constante durante operação (+50°C na área)
- **Risco**: Em ambientes >40°C, chance crescente de falha catastrófica
- **Explosão**: Dano estrutural, fogo propagado, possível morte do pawn invocado
- **Balanceamento**: Adiciona estratégia - onde e quando usar o tier 1

---

## 🗺️ Progressão do Jogador

### Fase 1: Início
1. Pesquisar Tier 1
2. Construir Altar Básico
3. Invocar pawn fraco (pouco tempo, alto consumo)

### Fase 2: Desenvolvimento
1. Pesquisar tiers 2-4
2. Construir estruturas melhores
3. Pawns mais fortes, duração maior, consumo menor

### Fase 3: Descoberta
1. Explorar masmorra
2. Encontrar o Anel
3. Equipar anel (biolock)
4. Invocar **Neoma** (permanente, sem consumo)

---

## 💻 Estrutura Técnica

### Defs Necessários
```
├── ThingDef (Estruturas - 4 tiers construíveis)
├── ThingDef (Anel - Apparel wearable)
├── ResearchProjectDef (Pesquisas - 1 por tier)
├── ResearchTabDef (nova aba "Projeto Neoma" para pesquisas)
├── HediffDef (Biolock do anel)
├── ThingDef (Pawns - 1 por tier + Neoma)
├── CompProperties_Heat (novo - para mecânica de calor Tier 1)
└── FactionDef/LordJob (Masmorra customizada)
```

### Componentes C#
```
├── CompFormgelSpawner (atual - expande para tiers)
├── CompNeomaRing (novo - lógica do anel wearable)
├── CompHeatRisk (novo - mecânica de calor e explosão Tier 1)
├── NeomaPawnSpawner (novo - gera pawns com atributos por tier)
└── NeomaDungeonManager (novo - masmorra customizada)
```

---

## ❓ Perguntas a Responder

1. **Quantos tiers antes do anel?** (4? 5?)
2. **O que torna Neoma "super quebrada"?** (pawn imortal? habilidades infinitas?)
3. **Masmorra customizada** - Como funciona?
4. **Morte de Neoma** - Permanente ou reinvocável?
5. **Múltiplos pawns** - Sim ou não?
6. **Balanceamento do calor** - Chance de explosão? Dano causado?

---

## 📋 Roadmap Próximos Passos

- [x] **Semana 1**: Finalizar design dos 4+ tiers + mecânica de calor ✅ **CONCLUÍDO**
- [x] **Semana 2**: Criar Defs das estruturas + pesquisas + aba customizada ✅ **CONCLUÍDO**
- [x] **Semana 3**: Expandir CompFormgelSpawner com tiers + energia + calor ✅ **CONCLUÍDO**
- [ ] **Semana 4**: Implementar anel wearable + biolock + Neoma
- [ ] **Semana 5**: Testes + balanceamento
- [ ] **Semana 6+**: Arte + masmorra + release do Projeto Neoma
