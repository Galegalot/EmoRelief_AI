# 🥊 EmoRelief AI

### Intelligent Emotional Catharsis Game

> **Academic project** · Year 2 Bachelor of Engineering in Computer Engineering
>
> **Institution:** Southeast Asia University (SAU) · **Course:** AI Learning and Data Science

EmoRelief AI is an interactive mobile/desktop stress-relief prototype: a safe, non-judgmental digital sandbox for emotional venting. It combines behavioral telemetry with applied AI to interpret gameplay patterns and offer empathetic feedback and personalized wellness suggestions.

## 🎯 Project Objective

The project connects **Data Science** (behavioral telemetry tracking) with **Applied Artificial Intelligence** (large language models) to:

1. Capture interaction metrics such as clicks per second, combat momentum, tool choices, and hesitation patterns.
2. Transform raw telemetry into structured behavioral features.
3. Use a Thai-centric large language model (Pathumma ThaiLLM) to interpret emotional states, generate empathetic feedback, and suggest actionable mental wellness activities and localized resources in Chiang Mai, Thailand.

## 🏗️ High-Level System Architecture

The prototype follows a decoupled, three-tier flow:

| Tier | Responsibility |
| --- | --- |
| **Interactive game** | Hosts the face-striking combat session and captures player interactions. |
| **Telemetry and feature extraction** | Aggregates gameplay events into behavioral features for analysis. |
| **AI interpretation and recommendations** | Sends structured features to the language model and presents generated feedback and localized suggestions. |

```text
┌─────────────────────┐    gameplay events    ┌────────────────────────┐
│  Interactive game   │ ────────────────────▶ │ Telemetry & features   │
└─────────────────────┘                       └───────────┬────────────┘
                                                         │ structured data
                                                         ▼
                                              ┌────────────────────────┐
                                              │ AI feedback & wellness │
                                              │ recommendations        │
                                              └────────────────────────┘
```

## 📊 Telemetry & Data Science Feature Extraction

During a face-striking combat session, the game extracts seven core behavioral features:

| Feature | Purpose |
| --- | --- |
| **Total fight duration** | Tracks session length as a measure of accumulated stress. |
| **Effective fighting time** | Analyzes the continuity of emotional discharge. |
| **Maximum attack speed (CPS)** | Identifies sudden increases in interaction speed. |
| **Maximum-speed duration** | Measures how long peak attack speed is sustained. |
| **Tool counters** | Quantifies actions using tools such as punches, slaps, bats, and darts. |
| **Pause tracking** | Characterizes play patterns, such as relentless versus hesitant or overthinking styles. |
| **Primary tool used** | Identifies preferred interaction tools and related gameplay patterns. |

## 🧠 AI Learning & LLM Integration

- **Model:** `Pathumma-ThaiLLM-qwen3-8b-think-3.0.0`, developed by NECTEC and accessed via [thaillm.or.th](http://thaillm.or.th).
- **Prompt engineering:** A Thai-focused psychological system prompt uses zero-shot/one-shot guidance and structured JSON constraints.
- **Inference post-processing:** Extracts JSON payloads from internal reasoning tokens (`<think>...</think>`) for reliable UI integration.
- **Localized context:** Generates three tailored self-care location suggestions in Chiang Mai: nature/parks, community malls, and mental health hospitals.

## 👥 Authors

**Department of Computer Engineering**, Faculty of Engineering, Southeast Asia University (SAU)
