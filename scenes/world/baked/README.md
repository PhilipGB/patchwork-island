# Baked v1.2.1 town snapshot

This directory is the generated OUTPUT of `SceneBakeTool` and is now the
runtime static world.

- `Town_Baked.tscn` is instantiated by `src/World/WorldSceneAdapter.cs`.
- `parts/*.tscn` are the reusable exact building, landmark, sign-socket, rally
  and decoration roots extracted by the bake.
- The bake was executed inside this v1.2.1 source checkout with Godot 4.7.2
  .NET (`ed1daf0bf`). It exited 0 and produced 32 reusable roots plus 488
  top-level town roots.
- Static geometry, collisions and the deterministic seeded foliage come from
  the original `WorldBuilder.Build()` call. They are not a hand-modelled
  approximation.
- The original `WorldBuilder` remains in the repository only as the source for
  this deterministic bake tool. `Main` no longer constructs it at runtime.

Do not regenerate these files by hand. Re-run the bake tool when the procedural
source of truth intentionally changes, then re-run the world-parity checks.
