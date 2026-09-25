# Bubble Fruit Loop - Prototype Foundation

This folder contains the gameplay-first foundation described in the v1 design plan.

## Authored prototype scene

- Open `Assets/Scenes/SampleScene.unity`. The complete placeholder board is visible and editable before Play Mode.
- Rebuild it from `Tools/Bubble Fruit Loop/Rebuild Prototype Scene` when the generated layout needs to be reset.
- Pool templates are stored under `Assets/_Game/Resources/Prefabs` and materials under `Assets/_Game/Materials`.
- Click a bubble to disable its collider and release all contained fruit.
- Released fruit uses Physics2D, reaches the intake, waits for capacity, stabilizes onto a cached closed loop, and is matched into active boxes.
- Boxes are independent queues. Full boxes activate the next logical box immediately.
- Loop capacity starts at 30 and exposes a guarded +5 booster API before lose.

## Architecture

- `Core`: events, clock, and explicit service registration.
- `Pooling`: generic component pool with warm-up and lifecycle callbacks.
- `Gameplay`: actors, state models, and single-purpose pickup, match, and result systems.
- `Managers`: orchestration for the fruit loop, funnel intake queue, and box board.
- `Runtime`: placeholder factory and composition root. Dependencies are injected here; systems do not search the scene.
- `Editor`: deterministic scene/prefab/material generator for the test layout.

## Rules enforced

- No `GameObject.Find`, object-finding APIs, or `GetComponent` calls.
- Input uses the Unity Input System (`Pointer.current`), matching Project Settings.
- No public mutable fields. State is private and exposed through read-only properties or guarded methods.
- Stable loop fruit is updated in one batch manager.
- Intake fruit does not consume loop capacity.
- Reservation releases capacity immediately and prevents double matching.
- Lose requires a full loop and no valid Loop-to-Box transition.
- Runtime logic does not wait for box animation.

## Next production steps

Replace prototype creation with validated `LevelData` ScriptableObjects and authoring tools, then add transient collision tuning, spline handles, pooled VFX, UI, audio, haptics, and mobile profiling.
