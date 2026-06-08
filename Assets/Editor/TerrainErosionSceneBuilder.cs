using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public static class TerrainErosionSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/TerrainErosionDemo.unity";
    private const string MaterialPath = "Assets/Scenes/TerrainErosionTerrain.mat";
    private const string ComputeShaderPath = "Assets/Shaders/ThermalErosion.compute";

    [MenuItem("Tools/Terrain Erosion/Create Demo Scene")]
    public static void CreateDemoScene()
    {
        EnsureFolder("Assets/Scenes");

        // Создаем чистую сцену, чтобы в ней были только нужные учебные объекты.
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "TerrainErosionDemo";

        Material terrainMaterial = CreateTerrainMaterial();
        CreateTerrain(terrainMaterial, out TerrainGenerator generator, out ErosionController erosionController);
        CreateCamera();
        CreateDirectionalLight();
        CreateDemoUI(generator, erosionController);

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

    private static void CreateTerrain(Material terrainMaterial, out TerrainGenerator generator, out ErosionController erosionController)
    {
        GameObject terrainObject = new GameObject("TerrainDemo");
        terrainObject.transform.position = Vector3.zero;

        terrainObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = terrainObject.AddComponent<MeshRenderer>();
        terrainObject.AddComponent<MeshCollider>();
        generator = terrainObject.AddComponent<TerrainGenerator>();
        erosionController = terrainObject.AddComponent<ErosionController>();

        generator.resolution = 128;
        generator.terrainSize = 100f;
        generator.heightMultiplier = 24f;
        generator.noiseScale = 28f;
        generator.octaves = 5;

        meshRenderer.sharedMaterial = terrainMaterial;
        erosionController.terrainGenerator = generator;
        erosionController.thermalErosionShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputeShaderPath);
        erosionController.iterations = 40;
        erosionController.talusThreshold = 0.015f;
        erosionController.erosionStrength = 0.4f;

        // Compute shader подключается как обычный Unity-asset без сторонних плагинов.
        if (erosionController.thermalErosionShader == null)
        {
            Debug.LogWarning("TerrainErosionSceneBuilder: не удалось автоматически подключить ThermalErosion.compute. Перетащите Assets/Shaders/ThermalErosion.compute в поле thermalErosionShader вручную.");
        }

        // После добавления компонента сразу генерируем heightmap и mesh.
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

    private static void CreateDemoUI(TerrainGenerator generator, ErosionController erosionController)
    {
        GameObject canvasObject = new GameObject("Demo UI");

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        TerrainErosionUI ui = canvasObject.AddComponent<TerrainErosionUI>();
        ui.terrainGenerator = generator;
        ui.erosionController = erosionController;

        Font font = GetDefaultFont();

        GameObject panel = CreateUIObject("Demo Panel", canvasObject.transform, new Vector2(24f, -24f), new Vector2(560f, 380f));
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.68f);

        CreateText(panel.transform, "Title Text", "Terrain Erosion Demo", new Vector2(16f, -14f), new Vector2(528f, 28f), 18, TextAnchor.MiddleLeft, font);

        // UI показывает pipeline проекта прямо во время демонстрации.
        ui.pipelineText = CreateText(
            panel.transform,
            "Pipeline Text",
            "Perlin Noise → Heightmap → Mesh → Compute Shader Erosion → Updated Mesh",
            new Vector2(16f, -48f),
            new Vector2(528f, 40f),
            14,
            TextAnchor.UpperLeft,
            font);

        ui.generateButton = CreateButton(panel.transform, "Generate Button", "Generate Terrain", new Vector2(16f, -98f), new Vector2(165f, 38f), font);
        ui.erosionButton = CreateButton(panel.transform, "Erosion Button", "Run Compute Erosion", new Vector2(197f, -98f), new Vector2(165f, 38f), font);
        ui.resetButton = CreateButton(panel.transform, "Reset Button", "Reset Terrain", new Vector2(378f, -98f), new Vector2(165f, 38f), font);

        ui.parametersText = CreateText(panel.transform, "Parameters Text", "", new Vector2(16f, -150f), new Vector2(528f, 120f), 15, TextAnchor.UpperLeft, font);
        ui.statusText = CreateText(panel.transform, "Status Text", "Status: Terrain generated", new Vector2(16f, -282f), new Vector2(528f, 28f), 14, TextAnchor.MiddleLeft, font);
        ui.hintText = CreateText(panel.transform, "Hint Text", "G — Generate, E — Run Compute Erosion, R — Reset", new Vector2(16f, -326f), new Vector2(528f, 28f), 14, TextAnchor.MiddleLeft, font);

        CreateEventSystem();
    }

    private static GameObject CreateUIObject(string name, Transform parent, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);

        RectTransform rectTransform = gameObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;

        return gameObject;
    }

    private static Text CreateText(Transform parent, string name, string textValue, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment, Font font)
    {
        GameObject textObject = CreateUIObject(name, parent, anchoredPosition, size);
        Text text = textObject.AddComponent<Text>();
        text.text = textValue;
        text.font = font;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        return text;
    }

    private static Button CreateButton(Transform parent, string name, string buttonText, Vector2 anchoredPosition, Vector2 size, Font font)
    {
        GameObject buttonObject = CreateUIObject(name, parent, anchoredPosition, size);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.18f, 0.32f, 0.2f, 0.95f);

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.18f, 0.32f, 0.2f, 0.95f);
        colors.highlightedColor = new Color(0.28f, 0.48f, 0.3f, 1f);
        colors.pressedColor = new Color(0.12f, 0.22f, 0.14f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        Text label = CreateText(buttonObject.transform, "Text", buttonText, Vector2.zero, size, 13, TextAnchor.MiddleCenter, font);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = Vector2.zero;

        return button;
    }

    private static void CreateEventSystem()
    {
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }

    private static Font GetDefaultFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return font;
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

        // Материал простой, однотонный, без текстур и сторонних ассетов.
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
