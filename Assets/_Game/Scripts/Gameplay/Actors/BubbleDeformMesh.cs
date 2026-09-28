using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    [ExecuteAlways]
    public sealed class BubbleDeformMesh : MonoBehaviour
    {
        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private int segmentCount = 64;
        [SerializeField] private float radius = 2.55f;
        [SerializeField] private float strength = 0.075f;
        [SerializeField] private float speed = 1.1f;
        [SerializeField] private float phase = 1.7f;
        private Mesh mesh;
        private Vector3[] vertices;
        private float impactSag;
        private float impactSagVelocity;
        private Vector2 supportDirection = Vector2.down;
        private Vector2 targetSupportDirection = Vector2.down;
        private float approachPressure;
        private bool bottomSupported;

        public void Initialize(MeshFilter filter, int segments, float baseRadius, float deformStrength,
            float deformSpeed, float phaseOffset)
        {
            meshFilter = filter;
            segmentCount = Mathf.Max(64, segments);
            radius = baseRadius;
            strength = deformStrength;
            speed = deformSpeed;
            phase = phaseOffset;
            BuildMesh();
        }

        private void Awake()
        {
            if (mesh == null) BuildMesh();
        }

        private void OnEnable()
        {
            // The level-design tool is used outside Play Mode. The prefab stores no
            // generated Mesh asset, so its bubble shell must also be built in Edit Mode.
            if (mesh == null || meshFilter == null || meshFilter.sharedMesh == null)
                BuildMesh();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            segmentCount = Mathf.Max(64, segmentCount);
            if (!isActiveAndEnabled || meshFilter == null) return;
            BuildMesh();
        }
#endif

        private void LateUpdate()
        {
            if (mesh == null) return;
            UpdateImpactSag(Time.deltaTime);
            UpdateSupportDirection(Time.deltaTime);
            UpdateVertices(Time.time * speed + phase, impactSag);
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        // Start the gradual squash only after a lower-edge collision begins.
        public void SetBottomPressure(bool supported, Vector2 localContactDirection)
        {
            if (localContactDirection.sqrMagnitude > 0.001f)
                targetSupportDirection = localContactDirection.normalized;
            bottomSupported = supported;
            if (supported) approachPressure = 0f;
        }

        public void SetApproachPressure(float pressure, Vector2 localContactDirection)
        {
            approachPressure = Mathf.Clamp01(pressure);
            if (localContactDirection.sqrMagnitude > 0.001f)
                targetSupportDirection = localContactDirection.normalized;
        }

        // Ease into and out of the supported shape without a sudden displacement.
        private void UpdateImpactSag(float deltaTime)
        {
            // Treat the membrane as a soft, damped spring: gradual sag under load,
            // then a tiny inward recoil as it releases instead of a sharp snap.
            float restingSag = bottomSupported ? 0.13f : approachPressure * 0.05f;
            const float spring = 20f;
            const float damping = 7f;
            impactSagVelocity += ((restingSag - impactSag) * spring
                - impactSagVelocity * damping) * deltaTime;
            impactSag += impactSagVelocity * deltaTime;
            impactSag = Mathf.Clamp(impactSag, -0.025f, 0.15f);
            if (Mathf.Abs(impactSag) <= 0.0005f && !bottomSupported)
            {
                impactSag = 0f;
                impactSagVelocity = 0f;
            }
        }

        // Smooth small contact-point changes so the dent does not flicker.
        private void UpdateSupportDirection(float deltaTime)
        {
            float blend = 1f - Mathf.Exp(-deltaTime / 0.22f);
            supportDirection = Vector2.Lerp(
                supportDirection, targetSupportDirection, blend).normalized;
        }

        private void BuildMesh()
        {
            if (meshFilter == null) return;
            segmentCount = Mathf.Max(64, segmentCount);
            if (mesh == null)
            {
                mesh = new Mesh
                {
                    name = "Bubble Deform Mesh",
                    hideFlags = HideFlags.DontSave
                };
            }
            else
            {
                mesh.Clear();
            }
            vertices = new Vector3[segmentCount + 1];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[segmentCount * 3];
            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);
            for (int index = 0; index < segmentCount; index++)
            {
                float angle = index / (float)segmentCount * Mathf.PI * 2f;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                vertices[index + 1] = direction * radius;
                uvs[index + 1] = new Vector2(0.5f + direction.x * 0.5f, 0.5f + direction.y * 0.5f);
                int triangle = index * 3;
                triangles[triangle] = 0;
                triangles[triangle + 1] = index + 1;
                triangles[triangle + 2] = index == segmentCount - 1 ? 1 : index + 2;
            }
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            meshFilter.sharedMesh = mesh;
        }

        private void UpdateVertices(float time, float bottomSag)
        {
            vertices[0] = Vector3.zero;
            float breathing = Mathf.Sin(time * 0.55f) * strength * 0.12f;
            for (int index = 0; index < segmentCount; index++)
            {
                float angle = index / (float)segmentCount * Mathf.PI * 2f;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                float supportWeight = Mathf.Pow(
                    Mathf.Clamp01(Vector2.Dot(direction, supportDirection)), 10f);
                float freeMembraneWeight = 1f - supportWeight;
                float waveA = Mathf.Sin(angle * 2f + time) * strength * freeMembraneWeight;
                float waveB = Mathf.Sin(angle * 5f - time * 1.37f + 0.8f)
                    * strength * 0.48f * freeMembraneWeight;
                float waveC = Mathf.Sin(angle * 7f + time * 0.73f + 2.1f)
                    * strength * 0.24f * freeMembraneWeight;
                float deformedRadius = radius + waveA + waveB + waveC + breathing;
                float supportedRadius = deformedRadius + bottomSag * supportWeight;
                vertices[index + 1] = direction * supportedRadius;
            }
        }
    }
}

