# Patchwork Island — first playable coding-agent handoff

**Outcome:** a complete, genuinely playable offline 3D PC game slice for ages approximately 5–10, not a mock-up or framework demo.

## Start here

1. Extract this directory into an empty Git repository (or its project root).
2. Open `STARTING_PROMPT.md` and paste its contents into your coding agent. Use that same prompt with Claude Code, Codex or OpenCode.
3. Let the agent implement and verify the Godot project. It must work through the phases in `IMPLEMENTATION_PLAN.md`, not stop after planning.
4. Open the finished Godot `project.godot` using **Godot 4.7.2 .NET** and run the main scene.

## Handoff materials

- `STARTING_PROMPT.md` — copy-ready instructions for a coding agent.
- `AGENTS.md` and `CLAUDE.md` — persistent agent instructions for common coding harnesses.
- `GAME_SPEC.md` — product design, gameplay rules, controls, world, educational principles and limits.
- `IMPLEMENTATION_PLAN.md` — ordered engineering milestones, verification requirements and cut rules.
- `MISSIONS.json` — reference content for ten real, playable objectives.
- `ART_DIRECTION.md` — concrete visual design and 3D/UI asset specifications.
- `ACCEPTANCE.md` — observable definition of done and test walkthrough.
- `planning/map.svg` — spatial planning map (schematic, not literal game screenshot).
- `assets/icons/*.svg` — original reusable UI icon artwork (game-production assets).
- `reference/*.png` — **concept references**, not production textures. AI-generated concept pictures can contain inconsistent text/details. Where they differ, the written specifications govern.

## Technical baseline

- Engine: Godot **4.7.2 stable, .NET build**, with its matching export templates.
- Language: C#; target `net8.0` where supported. Use the Godot .NET project SDK version that matches the installed editor, and avoid unnecessary packages.
- Platforms: Linux for development and Windows x64 for primary distributable. A Linux build is also desirable. **No web export:** Godot 4 C# web export is unsupported.
- Rendering: Compatibility renderer, low-poly mesh primitives, simple lighting, 1280x720 base viewport with scaling.
- Fully offline at runtime. Do not call remote APIs, require an account, or load assets at runtime from the internet.

**Important:** the two reference PNGs are inspiration only. They are not substitute game assets, and the agent must not paste the concept board onto a 3D environment or use textual artefacts from the images.
