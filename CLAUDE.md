# Nightwall — Project Guide & Engineering Rules

An **isometric horde-survival strategy** game for mobile, inspired by the perspective and
large-scale readability of *They Are Billions*. By day the player builds defensive architecture;
by night hordes attack. The player survives **entirely through defensive architecture** — walls,
gates, traps and manipulating enemy paths — and **never directly controls an attacking unit**.
Target platforms: **Android + iOS**. (Engineering-rule conventions below are adapted from the
sibling BastionBrawl project.)

> **Genre (hard rule).** A 3D isometric horde-survival / base-defence strategy game. The core
> fantasy is *defensive architecture*: the player shapes the battlefield, they do not fight. Every
> design, UI and gameplay decision must stay true to this — **no player-controlled attacking
> units, no direct-attack input.** The player affects the horde only by what they build.

> **Perspective (hard rule).** **Orthographic isometric / three-quarter** view looking down from
> an elevated angle, tuned for large-scale readability (many units on screen at once). The camera
> is `IsoCameraController` on a Cinemachine camera; keep it orthographic.

> **Mobile platform (hard rule).** This is a **mobile iOS + Android** game, played **landscape**
> (an iso strategy view needs the width). Every scene, camera, UI element and piece of geometry
> must be sized and framed for a phone screen — never a desktop/mouse-only layout:
> - **Design for phone aspect ratios** (landscape ≈16:9 to 19.5:9) and verify framing at phone
>   aspect, not the wide Editor Game-view default.
> - **Respect device safe areas** (notch / home indicator / rounded corners): anchor HUD inside
>   the safe area, never flush to the edge.
> - **Touch targets** ≥ ~44pt (iOS) / ~48dp (Android), spaced so adjacent controls aren't mis-tapped.
> - **Readability at phone scale:** chunky silhouettes, high contrast — the battlefield must read
>   on a ~6" screen at arm's length.
> - **Performance budget:** target mid-range mobile GPUs — keep draw calls, overdraw and poly
>   counts modest; the horde means **many agents**, so budget for crowd pathfinding/rendering.
> - Current input is keyboard/mouse (prototype); touch input is a deliberate later step.

> **Prototype scope (current).** Placeholder primitive geometry only; no menus, progression,
> economy, animations or polished art yet. Keep it a clean, playable prototype — don't add polish
> systems unless asked.

- **Editor:** Unity 6000.0.84f1 (LTS), URP, new Input System, AI Navigation (NavMesh), Cinemachine.

---

## Golden rules (read before writing code)

1. **Every behaviour is its own component.** One responsibility per `MonoBehaviour`. No god
   classes. If a class does two things (e.g. move *and* decide a target), split it.
2. **Composition over inheritance.** Use a base class only to remove genuine shared setup/state;
   never inherit just to share a helper — put that in a component or a static utility.
3. **Depend on abstractions.** Cross-system contact goes through interfaces (`IMotor`) or a shared
   container (`Health`), never incidental concrete types. An `Enemy` damages a `Health`; a `Trap`
   retunes an `IMotor` — neither reaches into unrelated internals.
4. **Decouple with events, don't poll.** State owners raise C# events (`Health.Died`,
   `Health.Damaged`, `Enemy.Died`); consumers subscribe in `OnEnable`/`Start` or `Awake` and
   **always unsubscribe** in `OnDisable`/`OnDestroy`.
5. **One-directional flow.** Input → gameplay → state → UI. UI reads game state and never mutates
   gameplay. Gameplay never reaches into UI. `GameManager` owns game-wide state/flow.
6. **Data lives in configs, not code.** Tunable numbers a designer would change belong in
   `[SerializeField]` fields (or a `ScriptableObject` when shared across many systems — see
   `MapConfig`), never as hard-coded literals in logic. The map's size is authored in `MapConfig`,
   not duplicated across the ground, camera and placer.

---

## C# best practices

- **Naming:** `PascalCase` types/methods/properties/events; `camelCase` locals/params;
  `_camelCase` private fields; `PascalCase` `const`. Interfaces start with `I`.
- **Access:** fields `private` by default; expose via properties. Serialize privates with
  `[SerializeField]` rather than making them `public`.
- **Immutability:** prefer `readonly`, expression-bodied getters, and `{ get; private set; }`.
- **No allocations in hot paths.** No LINQ / `new` in `Update`/`FixedUpdate`; cache arrays and
  component lookups. Cache `GetComponent` results in `Awake`. (The horde runs many `Enemy.Update`
  loops at once — keep per-frame work allocation-free; use `OverlapSphereNonAlloc`-style APIs.)
- **Null-safety:** guard external references; fail loud once with `Debug.LogError` in `Awake`
  when a required reference is missing, then no-op — don't spam per frame.
- **Events:** invoke with `?.Invoke(...)`. Unsubscribe symmetrically. No public fields for events.
- **Small methods, early returns.** XML `///` summaries on every public type and non-obvious member.

## Unity architecture best practices

- **Lifecycle:** cache refs in `Awake`; find *other* objects / register with singletons in
  `Start`; subscribe in `OnEnable`, unsubscribe in `OnDisable`. Input reads in `Update`.
- **`[RequireComponent]`** for every hard dependency so components can't be misconfigured.
- **Prefabs are the source of truth** for spawned/placed objects (Enemy, Wall, Trap). Edit the
  prefab, not scene instances. Spawners and the placer reference prefabs via `[SerializeField]`.
- **Layers:** gameplay filtering uses the `Ground` and `Building` layers (created by the scene
  builder). The breach AI only considers the `Building` layer; keep the list small.
- **No `GameObject.Find` in hot paths.** Resolve once in `Start`/`Awake` or inject via serialized
  refs. Singletons (`GameManager.Instance`, `GridSystem.Instance`) are set in `Awake`.
- **NavMesh is the pathing substrate.** Walls carry a carving `NavMeshObstacle` so the horde
  reroutes automatically; the "maze" emerges from pathfinding, not scripted lanes. Enemies breach
  a structure **only when no complete path to the HQ exists.**
- **Every `.cs`/asset is committed with its `.meta`.** Never commit `Library/`, `Temp/`, `obj/`.

## Folder / namespace layout

All gameplay code is namespace `Nightwall`, mirrored by folders under `Assets/Scripts/`:

```
Core/      GameManager, Hq, IMotor                 (game-wide flow + shared contracts)
Grid/      MapConfig (ScriptableObject), GridSystem (spatial source of truth)
Combat/    Health                                   (shared hit-point container)
Building/  Buildable, BuildingPlacer, Trap          (defensive architecture + placement)
Enemies/   Enemy, NavAgentMotor, WaveSpawner        (the horde + spawning)
CameraRig/ IsoCameraController                       (orthographic iso pan/zoom)
```

Editor-only scaffolding lives in `Assets/Editor/ProjectBootstrap/` (`SceneBuilder`,
`ProjectConfig`, ...) and is **not** gameplay code. Shared ScriptableObject assets live in
`Assets/ScriptableObjects/`.

## Architecture flow (who talks to whom)

```
BuildingPlacer (input) ─▶ GridSystem (snap/validate/occupy) ─▶ Instantiate Buildable   (building)
WaveSpawner ─▶ Enemy ─▶ NavAgentMotor (IMotor) ─▶ NavMeshAgent                          (the horde)
Enemy ─▶ Health.TakeDamage on HQ / a blocking Buildable (only when walled out)          (breach)
Trap ─▶ IMotor.MoveSpeed (slow while crossing)                                          (path shaping)
Health.Died ─▶ Enemy despawn · Buildable destroy (frees grid) · Hq ─▶ GameManager.OnHqDestroyed
```

- **Never** let a lower layer reach up (no gameplay code touching HUD; no UI mutating gameplay).
- New cross-system dependency? Add/extend an **interface** or an **event** — don't hard-reference.

## Base classes & reuse (concrete)

- `Health` — the one hit-point container (HQ, walls, traps, enemies). Exposes `TakeDamage`/`Heal`
  and `Damaged`/`Died` events. **Reuse it**; never reimplement HP.
- `IMotor` / `NavAgentMotor` — NavMesh locomotion. Enemies **use** it; traps retune `MoveSpeed`
  through it. Don't drive agents directly from AI code.
- `MapConfig` + `GridSystem` — the spatial source of truth. **All** placement, snapping, bounds
  and occupancy go through `GridSystem`; map dimensions come from `MapConfig`, nowhere else.
- `Buildable` — ties a placed structure's lifetime to grid occupancy. Every new buildable type
  gets this component (and a `Health`).

## Working with the scene (headless + live Editor)

The scene, prefabs and `MapConfig` are generated by `ProjectBootstrap.SceneBuilder` so the setup
is reproducible. When you change scripts/prefabs/scene wiring, regenerate rather than hand-editing
`.unity`/`.prefab` YAML:

- **Editor open:** run **Nightwall ▸ Rebuild Prototype Scene** from the menu (the builder only
  calls `EditorApplication.Exit` in batch mode, so it's safe in-Editor).
- **Headless:** you **cannot** run a second Unity instance while an Editor holds the project —
  close it first, then:
  ```
  <UnityEditor> -batchmode -nographics -quit -projectPath <proj> \
    -executeMethod ProjectBootstrap.SceneBuilder.Build -logFile <log>
  ```
- **Known quirk:** despite the project's ForceText setting, `SaveScene` currently writes
  `Assets/Scenes/Nightwall.unity` as **binary** in batch mode (prefabs/assets stay text). The
  scene is regenerated from code, so this isn't blocking, but scene diffs won't be reviewable.

## Definition of done for a change

Compiles clean (no errors) · follows the golden rules · new files in the right folder/namespace ·
events unsubscribed · committed with `.meta` · scene/prefabs regenerated via `SceneBuilder` when
their wiring changed · **one commit per prompt that produced changes.**
