# Docs index — Hero's Arena

Entry point to the repository documentation. Each section links to its owning file; plans, requirements, and sources are cross-referenced so nothing is orphaned.

## Project plan & tracking

| Doc | Purpose |
|-----|---------|
| [`docs/project-plan.md`](project-plan.md) | Project plan: stack, toolchain, commands, testing, completed features, open items, risks, cross-references |
| [`tasks/plan.md`](../tasks/plan.md) | Task-level production-readiness plan (T0.x–T5.x, phases, checkpoints, risks, resolved decisions) |
| [`tasks/todo.md`](../tasks/todo.md) | Task checklist mirroring `tasks/plan.md`, with verification evidence per round |

## Feature specifications

| Doc | Purpose |
|-----|---------|
| [`docs/recommendations.md`](recommendations.md) | R1 Tutorial / "First Wave" onboarding, R2 Difficulty selector (Easy/Normal/Hard), R3 Perk transparency + Sandbox mode; R-FX1–3 surroundings FX; R-LA1–5 Living Arena wildlife — endpoint/control specs, file maps, effort, sources |
| [`docs/ux-feedback-spec.md`](ux-feedback-spec.md) | Combat-feedback juice: damage numbers, wave/boss warning banners, power-up feedback, low-health state, pause/upgrade UX — EventBus hooks, C# signatures, timing values, file map |

## Investigation & research

| Doc | Purpose |
|-----|---------|
| [`docs/investigation-notes/2026-10-17-novice-onboarding.md`](investigation-notes/2026-10-17-novice-onboarding.md) | Novice onboarding findings (R1 source) |
| [`docs/investigation-notes/2026-10-17-illusion-fx-systems.md`](investigation-notes/2026-10-17-illusion-fx-systems.md) | Illusion FX systems findings (R-FX source) |
| [`docs/investigation-notes/2026-10-17-living-arena-wildlife.md`](investigation-notes/2026-10-17-living-arena-wildlife.md) | Living arena wildlife findings (R-LA source) |
| [`docs/external-research/godot-43-2d-lighting.md`](external-research/godot-43-2d-lighting.md) | Godot 4.3 2D lighting research |
| [`docs/external-research/godot-43-screen-shaders.md`](external-research/godot-43-screen-shaders.md) | Godot 4.3 screen shaders research |
| [`docs/external-research/godot-43-2d-particles.md`](external-research/godot-43-2d-particles.md) | Godot 4.3 2D particles research |
| [`docs/external-research/godot-43-ambient-life-impl.md`](external-research/godot-43-ambient-life-impl.md) | Godot 4.3 ambient life implementation research |
| [`docs/external-research/living-world-wildlife-patterns.md`](external-research/living-world-wildlife-patterns.md) | Living-world wildlife patterns research |
| [`docs/external-research/2d-destruction-illusions.md`](external-research/2d-destruction-illusions.md) | 2D destruction illusions research |

## Reports

| Doc | Purpose |
|-----|---------|
| [`docs/surgical-investigation-report.md`](surgical-investigation-report.md) | Surgical investigation report (markdown) |
| [`docs/surgical-investigation-report.html`](surgical-investigation-report.html) | Surgical investigation report (HTML) |
| [`docs/surgical-investigation-report.json`](surgical-investigation-report.json) | Surgical investigation report (JSON) |
| [`docs/surgical-investigation-report.sarif`](surgical-investigation-report.sarif) | Surgical investigation report (SARIF) |

## Project conventions

| Doc | Purpose |
|-----|---------|
| [`AGENTS.md`](../AGENTS.md) | Agent conventions: stack, toolchain, commands, testing, quality gates, branch hygiene, audit history, known gotchas |
| [`README.md`](../README.md) | User-facing overview: tech stack, architecture, building, testing, CI, known gaps |

## Repository root files

- [`Hero-sArena.csproj`](../Hero-sArena.csproj) — single C# project (Godot.NET.Sdk/4.3.0, net8.0)
- [`project.godot`](../project.godot) — Godot project config (autoloads registered here)
- [`export_presets.cfg`](../export_presets.cfg) — export presets (Linux/X11, Windows Desktop, macOS)
- [`install.sh`](../install.sh) — GUI installer (zenity; desktop icon + applications-menu entry)
- [`AGENTS.md`](../AGENTS.md) — see above
