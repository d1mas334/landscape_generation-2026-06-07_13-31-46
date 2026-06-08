using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class TerrainGenerator : MonoBehaviour
{
    [Header("Размер ландшафта")]
    [Min(2)]
    public int resolution = 129;

    [Min(1f)]
    public float terrainSize = 100f;

    [Min(0f)]
    public float heightMultiplier = 18f;

    [Header("Шум Перлина")]
    [Min(0.001f)]
    public float noiseScale = 35f;

    [Min(1)]
    public int octaves = 4;

    [Range(0f, 1f)]
    public float persistence = 0.5f;

    [Min(1f)]
    public float lacunarity = 2f;

    public int seed = 12345;

    // ДЛЯ ЗАЩИТЫ: heightMap хранит высоту каждой точки сетки ландшафта.
    private float[,] heightMap;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;

    private void Awake()
    {
        CacheComponents();
    }

    private void Start()
    {
        GenerateTerrain();
    }

    [ContextMenu("Generate Terrain")]
    public void GenerateTerrain()
    {
        CacheComponents();
        ClampSettings();

        // ДЛЯ ЗАЩИТЫ: сначала создаем карту высот, затем по ней строим геометрию.
        heightMap = GenerateHeightMap();
        UpdateMeshFromHeightMap();
    }

    public float[,] GenerateHeightMap()
    {
        float[,] newHeightMap = new float[resolution, resolution];

        // ДЛЯ ЗАЩИТЫ: проходим по каждой точке сетки и вычисляем высоту через шум Перлина.
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                newHeightMap[x, z] = GeneratePerlinHeight(x, z);
            }
        }

        return newHeightMap;
    }

    public float GeneratePerlinHeight(int x, int z)
    {
        float noiseHeight = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float maxPossibleHeight = 0f;

        // ДЛЯ ЗАЩИТЫ: seed сдвигает координаты шума, поэтому при другом seed получается другой рельеф.
        float seedOffsetX = seed * 12.9898f;
        float seedOffsetZ = seed * 78.233f;

        // ДЛЯ ЗАЩИТЫ: несколько октав складывают крупные и мелкие детали рельефа.
        for (int octave = 0; octave < octaves; octave++)
        {
            float sampleX = ((float)x / (resolution - 1) * terrainSize + seedOffsetX) / noiseScale * frequency;
            float sampleZ = ((float)z / (resolution - 1) * terrainSize + seedOffsetZ) / noiseScale * frequency;

            float perlinValue = Mathf.PerlinNoise(sampleX, sampleZ);

            noiseHeight += perlinValue * amplitude;
            maxPossibleHeight += amplitude;

            amplitude *= persistence;
            frequency *= lacunarity;
        }

        if (maxPossibleHeight <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(noiseHeight / maxPossibleHeight);
    }

    public Mesh BuildMesh()
    {
        if (heightMap == null)
        {
            heightMap = GenerateHeightMap();
        }

        return BuildMesh(heightMap);
    }

    public Mesh BuildMesh(float[,] sourceHeightMap)
    {
        Vector3[] vertices = new Vector3[resolution * resolution];
        int[] triangles = new int[(resolution - 1) * (resolution - 1) * 6];
        Vector2[] uvs = new Vector2[vertices.Length];

        float step = terrainSize / (resolution - 1);
        float halfSize = terrainSize * 0.5f;

        // ДЛЯ ЗАЩИТЫ: vertices - это точки mesh, их высота берется из heightmap.
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int index = z * resolution + x;

                float xPosition = x * step - halfSize;
                float zPosition = z * step - halfSize;
                float yPosition = sourceHeightMap[x, z] * heightMultiplier;

                vertices[index] = new Vector3(xPosition, yPosition, zPosition);
                uvs[index] = new Vector2((float)x / (resolution - 1), (float)z / (resolution - 1));
            }
        }

        int triangleIndex = 0;

        // ДЛЯ ЗАЩИТЫ: triangles задают, какие три вершины образуют каждый треугольник поверхности.
        for (int z = 0; z < resolution - 1; z++)
        {
            for (int x = 0; x < resolution - 1; x++)
            {
                int vertexIndex = z * resolution + x;

                triangles[triangleIndex] = vertexIndex;
                triangles[triangleIndex + 1] = vertexIndex + resolution;
                triangles[triangleIndex + 2] = vertexIndex + 1;

                triangles[triangleIndex + 3] = vertexIndex + 1;
                triangles[triangleIndex + 4] = vertexIndex + resolution;
                triangles[triangleIndex + 5] = vertexIndex + resolution + 1;

                triangleIndex += 6;
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "Generated Perlin Terrain";

        if (vertices.Length > 65535)
        {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;

        // ДЛЯ ЗАЩИТЫ: RecalculateNormals нужен, чтобы Unity правильно осветила склоны.
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    public void UpdateMeshFromHeightMap()
    {
        if (heightMap == null)
        {
            heightMap = GenerateHeightMap();
        }

        Mesh mesh = BuildMesh(heightMap);

        // ДЛЯ ЗАЩИТЫ: один и тот же mesh передается в отображение и в коллайдер.
        meshFilter.sharedMesh = mesh;
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = mesh;

        if (meshRenderer.sharedMaterial == null)
        {
            meshRenderer.sharedMaterial = CreateDefaultMaterial();
        }
    }

    [ContextMenu("Reset Terrain")]
    public void ResetTerrain()
    {
        resolution = 129;
        terrainSize = 100f;
        heightMultiplier = 18f;
        noiseScale = 35f;
        octaves = 4;
        persistence = 0.5f;
        lacunarity = 2f;
        seed = 12345;

        // ДЛЯ ЗАЩИТЫ: сброс параметров сразу пересоздает heightmap и mesh.
        GenerateTerrain();
    }

    private void CacheComponents()
    {
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }

        if (meshCollider == null)
        {
            meshCollider = GetComponent<MeshCollider>();
        }
    }

    private void ClampSettings()
    {
        resolution = Mathf.Max(2, resolution);
        terrainSize = Mathf.Max(1f, terrainSize);
        heightMultiplier = Mathf.Max(0f, heightMultiplier);
        noiseScale = Mathf.Max(0.001f, noiseScale);
        octaves = Mathf.Max(1, octaves);
        lacunarity = Mathf.Max(1f, lacunarity);
    }

    private Material CreateDefaultMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            shader = Shader.Find("Diffuse");
        }

        Material material = new Material(shader);
        material.name = "Generated Terrain Material";

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", new Color(0.28f, 0.55f, 0.24f));
        }
        else if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", new Color(0.28f, 0.55f, 0.24f));
        }

        return material;
    }
}
