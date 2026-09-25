using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using BubbleFruitLoop.Managers;
using BubbleFruitLoop.UI;

namespace BubbleFruitLoop.Gameplay
{
    [DefaultExecutionOrder(200)]
    public sealed class EditableBoxBoardController : MonoBehaviour
    {
        private sealed class Column
        {
            public readonly Queue<BoxView> Queue = new();
            public readonly List<Vector3> RowPositions = new();
            public BoxView Active;
            public Vector3 ActivePosition;
            public float PickupDistance;
        }

        private readonly List<FruitActor> loopFruits = new(40);
        private readonly Dictionary<FruitActor, float> previousDistances = new(40);
        private readonly Dictionary<BoxRuntime, BoxView> views = new();
        private static Material fruitFlightTrailMaterial;
        private const float PickupCatchDistance = 0.24f;
        private const int FlightFruitOrder = 80;
        private const float AuthoredColumnSpacing = 1.65f;
        private const float AuthoredFirstRowY = -2.45f;
        private const float AuthoredRowSpacing = 1.72f;
        private static Material celebrationTrailMaterial;
        private EditableFruitLoopController loop;
        private Column[] columns;
        private bool winCelebrated;
        private bool gameEnded;
        private float lossCheckElapsed;

        [Header("Editable scene layout")]
        [SerializeField] private BoxAssignmentTable assignmentTable;
        [SerializeField] private BoxView[] layoutBoxes;
        [SerializeField] private Transform[] columnPickupPoints;

        [Header("Win celebration assets")]
        [SerializeField] private GameObject winBurstPrefab;
        [SerializeField] private GameObject winDirectionalConfettiPrefab;
        [SerializeField] private GameObject winFlashPrefab;
        [SerializeField, Min(0.1f)] private float winFxScale = 1.35f;

        public void ConfigureSceneLayout(BoxView[] boxes) => layoutBoxes = boxes;
        public void ConfigureSceneLayout(BoxView[] boxes, Transform[] pickupPoints)
        {
            layoutBoxes = boxes;
            columnPickupPoints = pickupPoints;
        }
        public void ConfigureAssignmentTable(BoxAssignmentTable table) => assignmentTable = table;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForEditableLoop()
        {
            EditableFruitLoopController editable = FindFirstObjectByType<EditableFruitLoopController>();
            if (editable == null || editable.GetComponent<EditableBoxBoardController>() != null) return;
            editable.gameObject.AddComponent<EditableBoxBoardController>();
        }

        private void Awake() => loop = GetComponent<EditableFruitLoopController>();

        private IEnumerator Start()
        {
            // Allow the editable path to finish rebuilding before pickup distances are sampled.
            yield return null;
            if (loop == null || loop.PathLength <= 0f) yield break;
            BuildBoard();
        }

        private void Update()
        {
            if (columns == null || loop == null || gameEnded) return;
            loop.CopyLoopFruits(loopFruits);
            for (int fruitIndex = 0; fruitIndex < loopFruits.Count; fruitIndex++)
            {
                FruitActor fruit = loopFruits[fruitIndex];
                float current = fruit.PathDistance;
                float previous = previousDistances.TryGetValue(fruit, out float value) ? value : current;

                // Resolve exactly one destination. Scanning from right to left
                // makes the rightmost matching active box authoritative; a full
                // or closing right box also blocks matching boxes to its left
                // until its replacement has been promoted.
                int targetColumnIndex = FindPriorityColumn(fruit.Type);
                if (targetColumnIndex >= 0)
                {
                    Column targetColumn = columns[targetColumnIndex];
                    if (ReachedPickup(previous, current, targetColumn.PickupDistance))
                        TryCollect(fruit, targetColumn, targetColumnIndex);
                }

                if (fruit.State == FruitState.OnLoop) previousDistances[fruit] = current;
                else previousDistances.Remove(fruit);
            }

            UpdateLossCondition();
        }

        private void UpdateLossCondition()
        {
            if (!loop.IsFull || loopFruits.Count != loop.Count)
            {
                lossCheckElapsed = 0f;
                return;
            }

            for (int index = 0; index < loopFruits.Count; index++)
                if (FindPriorityColumn(loopFruits[index].Type) >= 0)
                {
                    lossCheckElapsed = 0f;
                    return;
                }

            lossCheckElapsed += Time.deltaTime;
            if (lossCheckElapsed < 0.45f) return;
            gameEnded = true;
            UICanvasLose.Show();
        }

        private void BuildBoard()
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            columns = new Column[3];

            if (assignmentTable != null && assignmentTable.Entries.Count > 0)
            {
                BuildFromAssignmentTable(camera);
                return;
            }

            if (layoutBoxes != null && layoutBoxes.Length >= 3)
            {
                BuildFromSceneLayout(camera);
                return;
            }

            FruitType[][] types =
            {
                new[] { FruitType.Orange, FruitType.Strawberry },
                new[] { FruitType.Strawberry, FruitType.Orange },
                new[] { FruitType.Orange, FruitType.Strawberry }
            };

            for (int columnIndex = 0; columnIndex < columns.Length; columnIndex++)
            {
                float viewportX = 0.27f + columnIndex * 0.23f;
                Vector3 active = camera.ViewportToWorldPoint(new Vector3(viewportX, 0.105f, -camera.transform.position.z));
                active.z = 0f;
                Vector3 pickupProbe = camera.ViewportToWorldPoint(new Vector3(viewportX, 0.29f, -camera.transform.position.z));
                pickupProbe.z = 0f;

                Column column = new()
                {
                    ActivePosition = active,
                    PickupDistance = loop.FindClosestPathDistance(pickupProbe)
                };
                columns[columnIndex] = column;

                for (int queueIndex = 0; queueIndex < 2; queueIndex++)
                {
                    int capacity = queueIndex == 0 ? 4 : 6;
                    BoxView view = CreateBox(types[columnIndex][queueIndex], capacity, columnIndex, queueIndex, active);
                    column.Queue.Enqueue(view);
                }
                PromoteNext(column, false);
            }
        }

        private void BuildFromAssignmentTable(Camera camera)
        {
            for (int columnIndex = 0; columnIndex < columns.Length; columnIndex++)
            {
                List<BoxAssignmentTable.Entry> entries = new();
                for (int index = 0; index < assignmentTable.Entries.Count; index++)
                {
                    BoxAssignmentTable.Entry entry = assignmentTable.Entries[index];
                    if (entry != null && entry.box != null && entry.column == columnIndex) entries.Add(entry);
                }
                entries.Sort((left, right) => left.queueOrder.CompareTo(right.queueOrder));
                if (entries.Count == 0)
                {
                    columns[columnIndex] = new Column();
                    continue;
                }

                Transform pickup = assignmentTable.GetPickupPoint(columnIndex);
                Vector3 pickupPosition = pickup != null ? pickup.position : ResolvePickupPosition(columnIndex, camera);
                Column column = new()
                {
                    ActivePosition = new Vector3(
                        (1 - columnIndex) * AuthoredColumnSpacing,
                        AuthoredFirstRowY,
                        entries[0].box.transform.position.z),
                    PickupDistance = loop.FindClosestPathDistance(pickupPosition)
                };
                columns[columnIndex] = column;

                for (int index = 0; index < entries.Count; index++)
                {
                    BoxAssignmentTable.Entry entry = entries[index];
                    BoxView view = entry.box;
                    Vector3 layoutPosition = view.transform.position;
                    layoutPosition.x = (1 - columnIndex) * AuthoredColumnSpacing;
                    layoutPosition.y = AuthoredFirstRowY - entry.queueOrder * AuthoredRowSpacing;
                    view.transform.position = layoutPosition;
                    view.Configure(entry.fruitType, view.Capacity,
                        assignmentTable.GetColor(entry.fruitType));
                    view.SetupRuntime();
                    view.SetEditorClosed(true);
                    view.Runtime.ColumnIndex = columnIndex;
                    views[view.Runtime] = view;
                    column.RowPositions.Add(view.transform.position);
                    column.Queue.Enqueue(view);
                }
                PromoteNext(column, false);
            }
        }

        private void BuildFromSceneLayout(Camera camera)
        {
            int rowCount = layoutBoxes.Length / 3;
            for (int columnIndex = 0; columnIndex < 3; columnIndex++)
            {
                BoxView first = layoutBoxes[columnIndex];
                Vector3 pickupProbe = ResolvePickupPosition(columnIndex, camera);
                Column column = new()
                {
                    ActivePosition = first.transform.position,
                    PickupDistance = loop.FindClosestPathDistance(pickupProbe)
                };
                columns[columnIndex] = column;

                for (int row = 0; row < rowCount; row++)
                {
                    int index = row * 3 + columnIndex;
                    if (index >= layoutBoxes.Length || layoutBoxes[index] == null) continue;
                    BoxView view = layoutBoxes[index];
                    column.RowPositions.Add(view.transform.position);
                    view.SetupRuntime();
                    view.SetEditorClosed(true);
                    view.Runtime.ColumnIndex = columnIndex;
                    views[view.Runtime] = view;
                    column.Queue.Enqueue(view);
                }
                PromoteNext(column, false);
            }
        }

        private Vector3 ResolvePickupPosition(int columnIndex, Camera camera)
        {
            if (columnPickupPoints != null && columnIndex < columnPickupPoints.Length &&
                columnPickupPoints[columnIndex] != null)
                return columnPickupPoints[columnIndex].position;

            GameObject scenePoint = GameObject.Find($"P{columnIndex + 1}");
            if (scenePoint != null) return scenePoint.transform.position;

            Vector3 fallback = camera.ViewportToWorldPoint(new Vector3(
                0.27f + columnIndex * 0.23f, 0.29f, -camera.transform.position.z));
            fallback.z = 0f;
            return fallback;
        }

        private BoxView CreateBox(FruitType type, int capacity, int columnIndex, int queueIndex, Vector3 active)
        {
            BoxView prefab = Resources.Load<BoxView>(capacity == 4 ? "Box4" : "Box6");
            BoxView view;
            if (prefab != null) view = Instantiate(prefab, transform);
            else
            {
                GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Quad);
                fallback.transform.SetParent(transform, false);
                view = fallback.AddComponent<BoxView>();
                view.Initialize(fallback.GetComponent<Renderer>());
            }

            view.name = $"Column {columnIndex + 1} - {type} Box {capacity}";
            view.transform.position = active + Vector3.down * (0.75f + queueIndex * 0.55f);
            view.transform.localScale *= 1.15f;
            view.Configure(type, capacity, ColorFor(type));
            view.SetupRuntime();
            view.SetEditorClosed(true);
            view.Runtime.ColumnIndex = columnIndex;
            views[view.Runtime] = view;
            return view;
        }

        private void PromoteNext(Column column, bool shiftQueue)
        {
            column.Active = column.Queue.Count > 0 ? column.Queue.Dequeue() : null;
            if (column.Active == null) return;
            column.Active.Runtime.Activate();
            column.Active.PlayPromote(column.ActivePosition);

            if (!shiftQueue) return;
            int row = 1;
            foreach (BoxView queued in column.Queue)
            {
                if (row >= column.RowPositions.Count) break;
                queued.PlayShiftTo(column.RowPositions[row]);
                row++;
            }
        }

        private bool IsHighestPriorityBox(FruitType type, int targetColumnIndex)
        {
            // In the authored layout C1/index 0 is physically on the right and
            // C3/index 2 is on the left. Lower indices therefore have priority.
            for (int i = 0; i < targetColumnIndex; i++)
            {
                BoxView box = columns[i].Active;
                if (box != null && box.Runtime.FruitType == type && box.Runtime.CanReserve) return false;
            }
            return true;
        }

        private int FindPriorityColumn(FruitType type)
        {
            // C1/index 0 is the rightmost physical column.
            for (int index = 0; index < columns.Length; index++)
            {
                BoxView box = columns[index].Active;
                if (box == null || box.Runtime == null || box.Runtime.FruitType != type) continue;
                return box.Runtime.CanReserve ? index : -1;
            }
            return -1;
        }

        private bool ReachedPickup(float previous, float current, float pickup)
        {
            if (Crossed(previous, current, pickup)) return true;
            float length = loop.PathLength;
            if (length <= 0f) return false;
            float separation = Mathf.Min(
                Mathf.Repeat(current - pickup, length),
                Mathf.Repeat(pickup - current, length));
            return separation <= PickupCatchDistance;
        }

        private bool TryCollect(FruitActor fruit, Column column, int columnIndex)
        {
            BoxView box = column.Active;
            if (box == null || !box.Runtime.TryReserve(fruit.Type, out int slotIndex)) return false;
            
            if (!IsHighestPriorityBox(fruit.Type, columnIndex))
            {
                box.Runtime.CancelReservation(slotIndex);
                return false;
            }

            if (!loop.ReserveFruitForBox(fruit))
            {
                box.Runtime.CancelReservation(slotIndex);
                return false;
            }

            SoundManager.TryPlayFX(FxID.FruitCollect);
            StartCoroutine(FlyFruitToBox(fruit, box, slotIndex, column));
            return true;
        }

        private IEnumerator FlyFruitToBox(FruitActor fruit, BoxView box, int slotIndex, Column column)
        {
            SpriteRenderer flightRenderer = fruit != null ? fruit.VisualSpriteRenderer : null;
            Sprite flightSprite = flightRenderer != null ? flightRenderer.sprite : null;
            if (fruit == null || box == null || !box.gameObject.activeInHierarchy ||
                flightRenderer == null || flightSprite == null)
            {
                if (box != null) box.Runtime.CancelReservation(slotIndex);
                yield break;
            }

            Transform target = box.GetSlotTransform(slotIndex);
            int originalSortingOrder = flightRenderer.sortingOrder;
            flightRenderer.sortingOrder = FlightFruitOrder;
            Vector3 start = fruit.CachedTransform.position;
            Vector3 startScale = fruit.CachedTransform.localScale;
            Quaternion landingRotation = target.rotation;
            Vector3 flightScale = startScale * 1.34f;
            Vector3 lifted = start + Vector3.up * 0.50f;
            TrailRenderer flightTrail = CreateFruitFlightTrail(fruit);

            // First make the selected fruit visibly pop out of the moving lane.
            const float liftDuration = 0.11f;
            float elapsed = 0f;
            while (elapsed < liftDuration && fruit != null && box != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / liftDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                fruit.CachedTransform.position = Vector3.LerpUnclamped(start, lifted, eased);
                fruit.CachedTransform.localScale = Vector3.LerpUnclamped(startScale, flightScale, eased);
                if (flightTrail != null) flightTrail.transform.position = fruit.CachedTransform.position;
                fruit.CachedTransform.Rotate(0f, 0f, 300f * Time.deltaTime);
                yield return null;
            }

            if (fruit == null || box == null)
            {
                ReleaseFruitFlightTrail(flightTrail);
                if (flightRenderer != null) flightRenderer.sortingOrder = originalSortingOrder;
                if (box != null) box.Runtime.CancelReservation(slotIndex);
                yield break;
            }
            fruit.CachedTransform.position = lifted;
            fruit.CachedTransform.localScale = flightScale;

            // Then retain that larger silhouette for the entire curved flight.
            // The apex must clear the carton lip. Use the vertical distance as
            // part of the arc so boxes at different rows still get a clean,
            // visibly elevated jump instead of cutting through the front edge.
            float verticalGap = Mathf.Abs(lifted.y - target.position.y);
            float arcHeight = Mathf.Max(1.15f, verticalGap * 0.35f + 0.75f);
            Vector3 control = (lifted + target.position) * 0.5f + Vector3.up * arcHeight;
            const float duration = 0.32f;
            elapsed = 0f;
            while (elapsed < duration && fruit != null && box != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                float inverse = 1f - eased;
                fruit.CachedTransform.position = inverse * inverse * lifted
                    + 2f * inverse * eased * control
                    + eased * eased * target.position;
                fruit.CachedTransform.localScale = flightScale;
                // Let the fruit spin during the jump, then settle into the
                // slot's authored orientation instead of stopping randomly.
                float settle = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, t));
                Quaternion spinning = fruit.CachedTransform.rotation
                    * Quaternion.Euler(0f, 0f, 420f * Time.deltaTime);
                fruit.CachedTransform.rotation = Quaternion.Slerp(spinning, landingRotation, settle);
                if (flightTrail != null) flightTrail.transform.position = fruit.CachedTransform.position;
                yield return null;
            }
            ReleaseFruitFlightTrail(flightTrail);
            if (fruit == null || box == null || !box.gameObject.activeInHierarchy)
            {
                if (flightRenderer != null) flightRenderer.sortingOrder = originalSortingOrder;
                if (box != null) box.Runtime.CancelReservation(slotIndex);
                yield break;
            }

            // Count the fruit only after it has visibly reached and occupied a slot.
            bool docked = box.DockFruit(fruit, flightRenderer, flightSprite, slotIndex);
            if (!docked)
            {
                flightRenderer.sortingOrder = originalSortingOrder;
                box.Runtime.CancelReservation(slotIndex);
                yield break;
            }
            bool full = box.Runtime.CommitReservedFruit(slotIndex);
            if (!full) yield break;

            column.Active = null;
            box.PlayCompleteAndExit(() =>
            {
                box.ReleaseCollectedFruits(DeactivateCollectedFruit);
                box.gameObject.SetActive(false);
                // The next box may move/open only after the full box has closed and exited.
                PromoteNext(column, true);
                TryPlayWinCelebration();
            });
        }

        private void TryPlayWinCelebration()
        {
            if (winCelebrated || columns == null) return;
            for (int index = 0; index < columns.Length; index++)
                if (columns[index].Active != null || columns[index].Queue.Count > 0) return;
            winCelebrated = true;
            gameEnded = true;
            StartCoroutine(PlayWinCelebration());
        }

        private IEnumerator PlayWinCelebration()
        {
            Camera camera = Camera.main;
            if (camera == null) yield break;
            CreateConfettiCannon(camera, true);
            CreateConfettiCannon(camera, false);
            for (int wave = 0; wave < 4; wave++)
            {
                float height = 0.38f + wave * 0.12f;
                StartCoroutine(LaunchCelebrationRocket(camera,
                    new Vector2(0.04f, 0.06f), new Vector2(0.22f + wave * 0.035f, height), wave));
                StartCoroutine(LaunchCelebrationRocket(camera,
                    new Vector2(0.96f, 0.06f), new Vector2(0.78f - wave * 0.035f, height), wave + 7));
                yield return new WaitForSecondsRealtime(0.24f);
            }

            // Let the imported fireworks finish, then wait for the player to press
            // Next. Level switching is owned by UICanvasWin.
            yield return new WaitForSecondsRealtime(2.7f);
            UICanvasWin.Show();
        }

        private IEnumerator LaunchCelebrationRocket(Camera camera, Vector2 startViewport,
            Vector2 targetViewport, int seed)
        {
            float depth = Mathf.Abs(camera.transform.position.z);
            Vector3 start = camera.ViewportToWorldPoint(new Vector3(startViewport.x, startViewport.y, depth));
            Vector3 target = camera.ViewportToWorldPoint(new Vector3(targetViewport.x, targetViewport.y, depth));
            start.z = target.z = -0.5f;

            GameObject rocket = new($"Win Rocket {seed}");
            rocket.transform.position = start;
            TrailRenderer trail = rocket.AddComponent<TrailRenderer>();
            if (celebrationTrailMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    celebrationTrailMaterial = new Material(shader)
                    {
                        name = "Runtime Win Rocket Trail",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
            }
            trail.sharedMaterial = celebrationTrailMaterial;
            trail.time = 0.32f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.13f), new Keyframe(1f, 0f));
            Color rocketColor = CelebrationColor(seed);
            trail.startColor = rocketColor;
            trail.endColor = new Color(rocketColor.r, rocketColor.g, rocketColor.b, 0f);
            trail.sortingOrder = 300;
            CreateRocketSparks(rocket, rocketColor);

            float elapsed = 0f;
            const float duration = 0.42f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 2f);
                Vector3 control = (start + target) * 0.5f
                    + Vector3.up * (0.55f + seed % 3 * 0.12f);
                float inverse = 1f - eased;
                rocket.transform.position = inverse * inverse * start
                    + 2f * inverse * eased * control + eased * eased * target;
                yield return null;
            }

            trail.emitting = false;
            CreateCelebrationBurst(target, seed);
            Destroy(rocket, trail.time + 0.08f);
        }

        private void CreateCelebrationBurst(Vector3 position, int seed)
        {
            bool usedImportedFx = false;
            if (winBurstPrefab != null)
            {
                SpawnImportedWinFx(winBurstPrefab, position, Quaternion.identity,
                    winFxScale, $"Win Confetti Burst {seed}", 5f);
                usedImportedFx = true;
            }
            if (winFlashPrefab != null)
            {
                SpawnImportedWinFx(winFlashPrefab, position + Vector3.back * 0.02f, Quaternion.identity,
                    winFxScale * 0.8f, $"Win Flash {seed}", 3f);
                usedImportedFx = true;
            }
            if (usedImportedFx) return;

            GameObject effect = new($"Win Firework Burst {seed}");
            effect.transform.position = position;
            ParticleSystem particles = effect.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.duration = 0.15f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.7f, 3.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4.2f, 8.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.23f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 260;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)220) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.18f;
            shape.radiusThickness = 1f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(CelebrationColor(seed), 0f),
                    new GradientColorKey(CelebrationColor(seed + 2), 0.35f),
                    new GradientColorKey(CelebrationColor(seed + 4), 0.7f),
                    new GradientColorKey(CelebrationColor(seed + 6), 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.72f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystem.RotationOverLifetimeModule rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-5f, 5f);

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 290;
            renderer.sharedMaterial = celebrationTrailMaterial;
            CreateBurstFlash(position, CelebrationColor(seed + 1));
            particles.Play();
            Destroy(effect, 3.2f);
        }

        private static void CreateRocketSparks(GameObject rocket, Color color)
        {
            ParticleSystem sparks = rocket.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = sparks.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            ParticleSystem.EmissionModule emission = sparks.emission;
            emission.rateOverTime = 34f;
            ParticleSystem.ShapeModule shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.055f;
            ParticleSystemRenderer renderer = sparks.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 305;
            renderer.sharedMaterial = celebrationTrailMaterial;
            sparks.Play();
        }

        private static void CreateBurstFlash(Vector3 position, Color color)
        {
            GameObject flashObject = new("Win Firework Flash");
            flashObject.transform.position = position;
            ParticleSystem flash = flashObject.AddComponent<ParticleSystem>();
            flash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = flash.main;
            main.loop = false;
            main.duration = 0.08f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.24f, 0.48f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.62f);
            main.startColor = Color.Lerp(color, Color.white, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = flash.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)28) });
            ParticleSystem.ShapeModule shape = flash.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.12f;
            ParticleSystemRenderer renderer = flash.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 310;
            renderer.sharedMaterial = celebrationTrailMaterial;
            flash.Play();
            Destroy(flashObject, 0.8f);
        }

        private void CreateConfettiCannon(Camera camera, bool left)
        {
            float depth = Mathf.Abs(camera.transform.position.z);
            Vector3 position = camera.ViewportToWorldPoint(new Vector3(left ? 0.035f : 0.965f, 0.04f, depth));
            position.z = -0.45f;
            if (winDirectionalConfettiPrefab != null)
            {
                GameObject imported = SpawnImportedWinFx(winDirectionalConfettiPrefab, position,
                    Quaternion.identity, winFxScale * 1.15f,
                    left ? "Left Imported Confetti Cannon" : "Right Imported Confetti Cannon", 6f);
                if (!left)
                {
                    Vector3 scale = imported.transform.localScale;
                    scale.x = -Mathf.Abs(scale.x);
                    imported.transform.localScale = scale;
                }
                return;
            }

            GameObject cannon = new(left ? "Left Win Confetti Cannon" : "Right Win Confetti Cannon");
            cannon.transform.position = position;
            ParticleSystem particles = cannon.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.duration = 0.12f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.3f, 4.0f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.18f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.55f, 1.0f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 260;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)210) });
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.16f;

            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(left ? 2.2f : -5.8f, left ? 5.8f : -2.2f);
            velocity.y = new ParticleSystem.MinMaxCurve(5.5f, 9.2f);

            ParticleSystem.RotationOverLifetimeModule rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-8f, 8f);

            ParticleSystem.ColorOverLifetimeModule colors = particles.colorOverLifetime;
            colors.enabled = true;
            Gradient rainbow = new();
            rainbow.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.red, 0f),
                    new GradientColorKey(Color.yellow, 0.2f),
                    new GradientColorKey(Color.green, 0.4f),
                    new GradientColorKey(Color.cyan, 0.6f),
                    new GradientColorKey(new Color(0.55f, 0.2f, 1f), 0.8f),
                    new GradientColorKey(Color.magenta, 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.78f), new GradientAlphaKey(0f, 1f) });
            colors.color = new ParticleSystem.MinMaxGradient(rainbow);

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1.8f;
            renderer.velocityScale = 0.22f;
            renderer.sortingOrder = 295;
            renderer.sharedMaterial = celebrationTrailMaterial;
            particles.Play();
            Destroy(cannon, 4.5f);
        }

        private static GameObject SpawnImportedWinFx(GameObject prefab, Vector3 position,
            Quaternion rotation, float scaleMultiplier, string instanceName, float lifetime)
        {
            GameObject effect = Instantiate(prefab, position, rotation);
            effect.name = instanceName;
            effect.transform.localScale *= scaleMultiplier;
            ParticleSystem[] particleSystems = effect.GetComponentsInChildren<ParticleSystem>(true);
            for (int index = 0; index < particleSystems.Length; index++)
            {
                ParticleSystemRenderer renderer = particleSystems[index].GetComponent<ParticleSystemRenderer>();
                if (renderer != null) renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, 290);
                particleSystems[index].Play(true);
            }
            Destroy(effect, lifetime);
            return effect;
        }

        private static Color CelebrationColor(int index) => (Mathf.Abs(index) % 6) switch
        {
            0 => new Color(1f, 0.18f, 0.28f),
            1 => new Color(1f, 0.82f, 0.05f),
            2 => new Color(0.12f, 0.92f, 0.48f),
            3 => new Color(0.05f, 0.82f, 1f),
            4 => new Color(0.62f, 0.22f, 1f),
            _ => new Color(1f, 0.24f, 0.72f)
        };

        private static TrailRenderer CreateFruitFlightTrail(FruitActor fruit)
        {
            if (fruit == null) return null;
            if (fruitFlightTrailMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) return null;
                fruitFlightTrailMaterial = new Material(shader)
                {
                    name = "Runtime Fruit Flight Trail",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            SpriteRenderer sprite = fruit.GetComponentInChildren<SpriteRenderer>(true);
            // Imported fruit sprites are normally rendered with a white tint, so
            // SpriteRenderer.color cannot identify the fruit hue. Drive the trail
            // from the gameplay typeâ€”the same mapping used by its target box.
            Color color = ColorFor(fruit.Type);
            GameObject trailObject = new($"{fruit.name} Flight Trail");
            trailObject.transform.position = fruit.CachedTransform.position;
            TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = fruitFlightTrailMaterial;
            trail.time = 0.24f;
            trail.minVertexDistance = 0.025f;
            trail.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.2f, 0.13f),
                new Keyframe(1f, 0.035f));
            trail.startColor = new Color(color.r, color.g, color.b, 0.8f);
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
            trail.sortingOrder = sprite != null ? sprite.sortingOrder - 1 : 19;
            trail.emitting = true;
            return trail;
        }

        private static void ReleaseFruitFlightTrail(TrailRenderer trail)
        {
            if (trail == null) return;
            trail.emitting = false;
            Destroy(trail.gameObject, trail.time + 0.05f);
        }

        private bool Crossed(float previous, float current, float pickup)
        {
            float length = loop.PathLength;
            if (length <= 0f) return false;
            
            float delta = Mathf.Repeat(current - previous + length * 0.5f, length) - length * 0.5f;
            if (delta <= 0f) return false; // Ignore backward movement from collision resolution

            float distanceToPickup = Mathf.Repeat(pickup - previous, length);
            return distanceToPickup > 0f && distanceToPickup <= delta;
        }

        private static void DeactivateCollectedFruit(FruitActor fruit)
        {
            if (fruit != null) fruit.gameObject.SetActive(false);
        }

        private static Color ColorFor(FruitType type) => type switch
        {
            FruitType.Apple => new Color(0f, 0.46160913f, 1f),
            FruitType.Orange => new Color(1f, 0.45f, 0.05f),
            FruitType.Grape => new Color(0.55f, 0.18f, 0.85f),
            FruitType.Lemon => new Color(0.95f, 0.88f, 0.08f),
            FruitType.Strawberry => new Color(1f, 0.4292453f, 0.4292453f),
            _ => Color.white
        };
    }
}

