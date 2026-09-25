using System;
using System.Collections;
using System.Collections.Generic;
using BubbleFruitLoop.Core;
using BubbleFruitLoop.Pooling;
using DG.Tweening;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BubbleFruitLoop.Gameplay
{
    public sealed partial class BoxView : MonoBehaviour, IPoolable
    {
        private const int ContactShadowOrder = 0;
        private const int DepthOrder = 5;
        private const int CavityOrder = 15;
        private const int FruitShadowOrder = 18;
        private const int FruitOrder = 20;
        private const int DividerOrder = 22;
        private const int FrontWallOrder = 30;
        private const int DoorShadowOrder = 35;
        private const int DoorOrder = 40;
        private const int DoorHighlightOrder = 41;
        [SerializeField] private Renderer boxRenderer;
        [SerializeField] private Transform[] fruitSlots;
        [SerializeField] private ParticleSystem fullVfx;
        [SerializeField] private GameObject labelFireworkPrefab;
        [SerializeField] private Transform leftLid;
        [SerializeField] private Transform rightLid;
        [SerializeField] private Transform innerTray;
        [SerializeField] private Transform backShadow;
        [SerializeField] private SpriteRenderer openArtwork;
        [SerializeField] private SpriteRenderer closedArtwork;
        
        [Header("Animation")]
        [SerializeField, Min(0.05f)] private float slideDuration = 0.28f;
        [SerializeField, Range(60f, 88f)] private float doorOpenAngle = 78f;
        [SerializeField, Min(0.05f)] private float lidDuration = 0.23f;
        [SerializeField, Min(0.05f)] private float closeLidDuration = 0.20f;
        [SerializeField, Range(0f, 0.06f)] private float rightDoorDelay = 0.03f;
        [SerializeField, Min(0.05f)] private float exitDuration = 0.42f;

        private readonly List<FruitActor> collectedFruits = new(6);
        private Vector3 baseScale;
        private Vector3 leftLidOpenPosition;
        private Vector3 rightLidOpenPosition;
        private Vector3 openArtworkScale;
        private Vector3 closedArtworkScale;
        private Vector3 openArtworkPosition;
        private Vector3 closedArtworkPosition;
        private SpriteRenderer frontLipArtwork;
        private SpriteMask frontLipMask;
        private SpriteRenderer contactShadowArtwork;
        private SpriteRenderer liftGroundShadow;
        private float liftShadowRestingAlpha = 0.56f;
        private bool liftShadowGroundLocked;
        private float liftShadowGroundY;
        private static readonly Vector3 LiftShadowOffset = new(-0.45f, -0.025f, 0.12f);
        private SpriteRenderer depthArtwork;
        private SpriteRenderer leftDoorShadow;
        private SpriteRenderer rightDoorShadow;
        private Transform closedDepthRoot;
        private SpriteRenderer closedDepthBack;
        private SpriteRenderer closedBottomBevel;
        private SpriteRenderer closedRightBevel;
        private static Sprite lipMaskSprite;
        private static Sprite softRoundedSprite;
        private static Sprite parallelogramShadowSprite;
        private static Material runtime3DMaterial;
        private static Material runtimeTrailMaterial;
        private static Material spriteSilhouetteMaterial;
        private static Mesh roundedDoorMesh;
        private static MaterialPropertyBlock colorPropertyBlock;
        private Coroutine motionRoutine;
        private Sequence motionSequence;
        private Color configuredColor = Color.white;
        private bool hasConfiguredColor;
        private bool hidePackedFruitsOnClose;

        public BoxRuntime Runtime { get; private set; }
        public FruitType editorFruitType;
        public int editorCapacity = 4;
        public int Capacity => editorCapacity;

        public void Initialize(Renderer renderer) => boxRenderer = renderer;
        public void SetSharedMaterial(Material material) { if (boxRenderer != null) boxRenderer.sharedMaterial = material; }

        public void Configure(FruitType type, int capacity, Color color)
        {
            // LevelLoader may resize the instance after Awake. Animation must
            // return to that authored/runtime size, never the prefab's old size.
            baseScale = transform.localScale;
            editorFruitType = type;
            editorCapacity = 4;
            configuredColor = color;
            hasConfiguredColor = true;
            hidePackedFruitsOnClose = false;
            NormalizeArtworkDimensions();
            ApplyColor(color);
            if (Application.isPlaying) EnsureLiftGroundShadow(transform.position);
        }


        public float GetOpenTopOffsetWorld() => 0.56f * Mathf.Abs(transform.lossyScale.y);

        public float GetOpenHeightWorld() => 1.12f * Mathf.Abs(transform.lossyScale.y);

        public float GetClosedHeightWorld() => 1.12f * Mathf.Abs(transform.lossyScale.y);

        private float GetArtworkHeightWorld(SpriteRenderer artwork, float fallback)
        {
            if (artwork == null || artwork.sprite == null) return fallback * Mathf.Abs(transform.lossyScale.y);
            return artwork.sprite.bounds.size.y * Mathf.Abs(artwork.transform.localScale.y)
                * Mathf.Abs(transform.lossyScale.y);
        }

        private void Awake()
        {
            EnsureRuntime25DModel();
            if (baseScale == Vector3.zero) baseScale = transform.localScale;
            EnsureFrontLip();
            Cache25DReferences();
        }


        public void SetupRuntime()
        {
            if (Runtime != null) return;
            Runtime = new BoxRuntime();
            Runtime.Configure(editorFruitType, editorCapacity);
            Bind(Runtime);
        }

        public void Bind(BoxRuntime runtime)
        {
            Unbind();
            Runtime = runtime;
            Runtime.OnFruitFilled += PlayFillAnimation;
            Runtime.OnBoxFull += PlayCloseAnimation;
            ApplyColor(hasConfiguredColor ? configuredColor : ColorFor(Runtime.FruitType));
        }

        public void Unbind()
        {
            if (Runtime == null) return;
            Runtime.OnFruitFilled -= PlayFillAnimation;
            Runtime.OnBoxFull -= PlayCloseAnimation;
            Runtime = null;
        }


        public void OnSpawned() 
        { 
            hidePackedFruitsOnClose = false;
            if (baseScale == Vector3.zero) baseScale = transform.localScale;
            EnsureFrontLip();
            transform.localScale = baseScale; 
            if (leftLid != null && rightLid != null)
            {
                leftLidOpenPosition = leftLid.localPosition;
                rightLidOpenPosition = rightLid.localPosition;
            }
            SetLidProgress(0f); 
        }
        
        public void OnDespawned()
        {
            StopMotion();
            ReleaseLiftGroundShadow();
            ReleaseCollectedFruits(null);
            Unbind();
        }
    }
}

