# Bit Blast

A falling-block puzzle game that teaches boolean logic (XOR, AND, NAND) through gameplay instead of a textbook. Built in Unity 2D with C# in under 24 hours for a hackathon.

**Play it:** https://block-blast-but-i-need-to.study/

## Concept

As computer architecture students, we wanted to take a dry but essential CS topic — boolean logic — and make it genuinely fun. An 8-bit register sits at the bottom of the screen while blocks of 1–4 bits fall from the top, each labeled with a gate operation. When a block lands, it applies its operation to the register bits it touches. A live truth table sits on the side panel as a reference — until players don't need it anymore.

Match the goal state to clear the register and level up.

## Features

- Gate-dependent win conditions: XOR and NAND target all 1s, AND targets all 0s, with scoring and detection branching per gate type
- Variable block widths (1–4 bits) with dynamic spawn, movement clamping, landing detection, and gate alignment all derived from a single size value
- Coordinate mapping between Unity's local canvas space and register array indices for pixel-accurate block landing
- Lock-delay coroutine to prevent double-trigger race conditions when a block lands
- Live truth table sidebar that teaches gate behavior passively through play
- Scoring and level progression, with fall speed increasing each level

## Tech Stack

- **Unity 2D** (Universal Render Pipeline)
- **C#** — all game logic lives in `Assets/Resources/Scripts/GameManager.cs`
- **TextMeshPro** for UI text rendering

## Project Structure

```
Assets/
├── Resources/
│   ├── Scripts/GameManager.cs   # register state, gate logic, win detection, scoring, levels
│   └── png/                      # gate sprites, truth tables, UI icons
├── Prefab/bit.prefab             # the falling bit-block prefab
├── Scenes/SampleScene.unity      # main game scene
└── Settings/                     # URP renderer & pipeline settings
```

## Running Locally

1. Clone the repo and open the project folder in **Unity Hub** (built with a recent Unity 2D/URP version).
2. Open `Assets/Scenes/SampleScene.unity`.
3. Press **Play** in the Unity Editor.

## What's Next

Looking to expand with more number bases, a two-player mode, and mobile support.
