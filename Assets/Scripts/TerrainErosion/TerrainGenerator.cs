using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class TerrainGenerator : MonoBehaviour
{
    [Header("Размер ландшафта")]
    [Min(2)]
    public int resolution = 128;

    [Min(1f)]
    public float terrainSize = 100f;

    [Min(0f)]
    public float heightMultiplier = 24f;

    [Header("Шум Перлина")]
    [Min(0.001f)]
    public float noiseScale = 28f;

    [Min(1)]
    public int octaves = 5;

    [Range(0f, 1f)]
    public float persistence = 0.5f;

    [Min(1f)]
    public float lacunarity = 2f;

    public int seed = 12345;

    // HeightMap хранит высоту каждой точки сетки ландшафта.
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

        // Сначала создаем карту высот, затем по ней строим геометрию.
        heightMap = GenerateHeightMap();
        UpdateMeshFromHeightMap();
    }

    public float[,] GenerateHeightMap()
    {
        float[,] newHeightMap = new float[resolution, resolution];

        // Heightmap создается как таблица высот для всех точек ландшафта.
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                newHeightMap[x, z] = GeneratePerlinHeight(x, z);
            }
        }

        return newHeightMap;
    }

    public float[] GetHeightMapCopy()
    {
        ClampSettings();

        if (heightMap == null || heightMap.GetLength(0) != resolution || heightMap.GetLength(1) != resolution)
        {
            heightMap = GenerateHeightMap();
        }

        float[] heights = new float[resolution * resolution];

        // Compute shader получает heightmap как одномерный массив float.
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int index = z * resolution + x;
                heights[index] = heightMap[x, z];
            }
        }

        return heights;
    }

    public void SetHeightMap(float[] newHeights)
    {
        CacheComponents();
        ClampSettings();

        if (newHeights == null || newHeights.Length != resolution * resolution)
        {
            Debug.LogError("TerrainGenerator: размер новой heightmap не совпадает с resolution.");
            return;
        }

        heightMap = new float[resolution, resolution];

        // Результат GPU-эрозии возвращается из одномерного массива обратно в heightMap.
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int index = z * resolution + x;
                heightMap[x, z] = Mathf.Clamp01(newHeights[index]);
            }
        }

        // После изменения heightmap пересобираем mesh, чтобы увидеть эрозию на сцене.
        UpdateMeshFromHeightMap();
    }

    public int GetResolution()
    {
        ClampSettings();
        return resolution;
    }

    public float GeneratePerlinHeight(int x, int z)
    {
        float noiseHeight = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float maxPossibleHeight = 0f;

        // Seed сдвигает координаты шума, поэтому при другом seed получается другой рельеф.
        float seedOffsetX = seed * 12.9898f;
        float seedOffsetZ = seed * 78.233f;

        // Perlin noise используется для получения плавных природных высот.
        // Несколько октав складывают крупные и мелкие детали рельефа.
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
        if (heightMap == null || heightMap.GetLength(0) != resolution || heightMap.GetLength(1) != resolution)
        {
            heightMap = GenerateHeightMap();
        }

        return BuildMesh(heightMap);
    }

    public Mesh BuildMesh(float[,] sourceHeightMap)
    {
        // Mesh generation превращает heightmap в вершины, треугольники и UV.
        Vector3[] vertices = new Vector3[resolution * resolution];
        int[] triangles = new int[(resolution - 1) * (resolution - 1) * 6];
        Vector2[] uvs = new Vector2[vertices.Length];

        float step = terrainSize / (resolution - 1);
        float halfSize = terrainSize * 0.5f;

        // Vertices - это точки mesh, их высота берется из heightmap.
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

        // Triangles задают, какие три вершины образуют каждый треугольник поверхности.
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

        // RecalculateNormals нужен, чтобы Unity правильно осветила склоны.
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    public void UpdateMeshFromHeightMap()
    {
        CacheComponents();
        ClampSettings();

        if (heightMap == null || heightMap.GetLength(0) != resolution || heightMap.GetLength(1) != resolution)
        {
            heightMap = GenerateHeightMap();
        }

        Mesh mesh = BuildMesh(heightMap);

        // Mesh update заменяет геометрию после генерации или GPU-эрозии.
        // Один и тот же mesh передается в отображение и в коллайдер.
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
        resolution = 128;
        terrainSize = 100f;
        heightMultiplier = 24f;
        noiseScale = 28f;
        octaves = 5;
        persistence = 0.5f;
        lacunarity = 2f;
        seed = 12345;

        // Сброс параметров сразу пересоздает heightmap и mesh.
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
        // Создаем простой материал для ландшафта на стандартном URP-шейдере.
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

        // Задаем обычный зеленый цвет без текстур.
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
