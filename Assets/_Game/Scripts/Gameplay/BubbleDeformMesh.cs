using UnityEngine;

namespace BubbleFruitLoop.Gameplay
{
    [ExecuteAlways]
    public sealed class BubbleDeformMesh : MonoBehaviour
    {
        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private int segmentCount = 32;
        [SerializeField] private float radius = 2.55f;
        [SerializeField] private float strength = 0.075f;
        [SerializeField] private float speed = 1.1f;
        [SerializeField] private float phase = 1.7f;
        private Mesh mesh;
        private Vector3[] vertices;

        public void Initialize(MeshFilter filter, int segments, float baseRadius, float deformStrength,
            float deformSpeed, float phaseOffset)
        {
            meshFilter = filter;
            segmentCount = Mathf.Max(24, segments);
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
            segmentCount = Mathf.Max(24, segmentCount);
            if (!isActiveAndEnabled || meshFilter == null) return;
            BuildMesh();
        }
#endif

        private void LateUpdate()
        {
            if (mesh == null) return;
            UpdateVertices(Time.time * speed + phase);
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }

        private void BuildMesh()
        {
            if (meshFilter == null) return;
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

        private void UpdateVertices(float time)
        {
            vertices[0] = Vector3.zero;
            float breathing = Mathf.Sin(time * 0.55f) * strength * 0.12f;
            for (int index = 0; index < segmentCount; index++)
            {
                float angle = index / (float)segmentCount * Mathf.PI * 2f;
                float waveA = Mathf.Sin(angle * 2f + time) * strength;
                float waveB = Mathf.Sin(angle * 5f - time * 1.37f + 0.8f) * strength * 0.48f;
                float waveC = Mathf.Sin(angle * 7f + time * 0.73f + 2.1f) * strength * 0.24f;
                float deformedRadius = radius + waveA + waveB + waveC + breathing;
                vertices[index + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * deformedRadius;
            }
        }
    }
}
