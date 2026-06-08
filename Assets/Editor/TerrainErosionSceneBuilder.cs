using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TerrainErosionSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/TerrainErosionDemo.unity";
    private const string MaterialPath = "Assets/Scenes/TerrainErosionTerrain.mat";

    [MenuItem("Tools/Terrain Erosion/Create Demo Scene")]
    public static void CreateDemoScene()
    {
        EnsureFolder("Assets/Scenes");

        // ДЛЯ ЗАЩИТЫ: создаем чистую сцену, чтобы в ней были только нужные учебные объекты.
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "TerrainErosionDemo";

        Material terrainMaterial = CreateTerrainMaterial();
        CreateTerrain(terrainMaterial);
        CreateCamera();
        CreateDirectionalLight();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog(
                "Terrain Erosion",
                "Демо-сцена создана: " + ScenePath,
                "OK");
        }
    }

    private static void CreateTerrain(Material terrainMaterial)
    {
        GameObject terrainObject = new GameObject("TerrainDemo");
        terrainObject.transform.position = Vector3.zero;

        terrainObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = terrainObject.AddComponent<MeshRenderer>();
        terrainObject.AddComponent<MeshCollider>();
        TerrainGenerator generator = terrainObject.AddComponent<TerrainGenerator>();

        meshRenderer.sharedMaterial = terrainMaterial;

        // ДЛЯ ЗАЩИТЫ: после добавления компонента сразу генерируем heightmap и mesh.
        generator.GenerateTerrain();

        Selection.activeGameObject = terrainObject;
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 35f, -75f);
        cameraObject.transform.rotation = Quaternion.Euler(25f, 0f, 0f);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = 60f;
        camera.farClipPlane = 1000f;

        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<TerrainCameraController>();
    }

    private static void CreateDirectionalLight()
    {
        GameObject lightObject = new GameObject("Directional Light");
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2f;
        light.color = Color.white;

        RenderSettings.sun = light;
    }

    private static Material CreateTerrainMaterial()
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

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

        if (material == null)
        {
            material = new Material(shader);
            material.name = "TerrainErosionTerrain";
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        // ДЛЯ ЗАЩИТЫ: материал простой, однотонный, без текстур и сторонних ассетов.
        Color terrainColor = new Color(0.28f, 0.55f, 0.24f);

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", terrainColor);
        }
        else if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", terrainColor);
        }

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();

        return material;
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }
    }
}
