using System.Collections;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class TerrainErosionUI : MonoBehaviour
{
    [Header("Связи")]
    public TerrainGenerator terrainGenerator;
    public ErosionController erosionController;

    [Header("Кнопки")]
    public Button generateButton;
    public Button erosionButton;
    public Button resetButton;

    [Header("Тексты")]
    public Text parametersText;
    public Text pipelineText;
    public Text hintText;
    public Text statusText;

    private bool erosionIsRunning;

    private void Awake()
    {
        FindSceneObjectsIfNeeded();
    }

    private void OnEnable()
    {
        FindSceneObjectsIfNeeded();

        if (generateButton != null)
        {
            generateButton.onClick.AddListener(GenerateTerrain);
        }

        if (erosionButton != null)
        {
            erosionButton.onClick.AddListener(RunComputeErosion);
        }

        if (resetButton != null)
        {
            resetButton.onClick.AddListener(ResetTerrain);
        }

        UpdateStaticTexts();
        UpdateParametersText();
        SetStatus("Terrain generated");
    }

    private void OnDisable()
    {
        if (generateButton != null)
        {
            generateButton.onClick.RemoveListener(GenerateTerrain);
        }

        if (erosionButton != null)
        {
            erosionButton.onClick.RemoveListener(RunComputeErosion);
        }

        if (resetButton != null)
        {
            resetButton.onClick.RemoveListener(ResetTerrain);
        }
    }

    private void Update()
    {
        HandleHotkeys();
        UpdateParametersText();
    }

    // Кнопка Generate Terrain запускает новую генерацию heightmap и mesh.
    public void GenerateTerrain()
    {
        if (terrainGenerator == null)
        {
            Debug.LogError("TerrainErosionUI: не назначен TerrainGenerator.");
            return;
        }

        terrainGenerator.GenerateTerrain();
        SetStatus("Terrain generated");
        UpdateParametersText();
    }

    // Кнопка Run Compute Erosion запускает эрозию только через compute shader.
    public void RunComputeErosion()
    {
        if (!erosionIsRunning)
        {
            StartCoroutine(RunComputeErosionRoutine());
        }
    }

    private IEnumerator RunComputeErosionRoutine()
    {
        if (erosionController == null)
        {
            Debug.LogError("TerrainErosionUI: не назначен ErosionController.");
            yield break;
        }

        if (!erosionController.CanRunComputeErosion(out string statusMessage))
        {
            SetStatus(statusMessage);
            yield break;
        }

        erosionIsRunning = true;
        SetStatus("Compute erosion started");
        yield return null;

        // Запуск расчета эрозии передается в ErosionController, где работает compute shader.
        erosionController.RunComputeErosion();
        SetStatus("Compute erosion completed");
        UpdateParametersText();

        erosionIsRunning = false;
    }

    public void ResetTerrain()
    {
        if (erosionController != null)
        {
            erosionController.ResetTerrain();
        }
        else if (terrainGenerator != null)
        {
            terrainGenerator.ResetTerrain();
        }
        else
        {
            Debug.LogError("TerrainErosionUI: не назначены TerrainGenerator и ErosionController.");
        }

        SetStatus("Terrain generated");
        UpdateParametersText();
    }

    private void HandleHotkeys()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        if (keyboard.gKey.wasPressedThisFrame)
        {
            GenerateTerrain();
        }

        if (keyboard.eKey.wasPressedThisFrame)
        {
            RunComputeErosion();
        }

        if (keyboard.rKey.wasPressedThisFrame)
        {
            ResetTerrain();
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.G))
        {
            GenerateTerrain();
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            RunComputeErosion();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetTerrain();
        }
#endif
    }

    private void UpdateStaticTexts()
    {
        // Здесь отображается pipeline проекта от шума Перлина до обновленного mesh.
        if (pipelineText != null)
        {
            pipelineText.text = "Perlin Noise → Heightmap → Mesh → Compute Shader Erosion → Updated Mesh";
        }

        if (hintText != null)
        {
            hintText.text = "G — Generate, E — Run Compute Erosion, R — Reset";
        }
    }

    private void SetStatus(string status)
    {
        if (statusText != null)
        {
            statusText.text = "Status: " + status;
        }
    }

    private void UpdateParametersText()
    {
        if (parametersText == null || terrainGenerator == null || erosionController == null)
        {
            return;
        }

        parametersText.text =
            "resolution: " + terrainGenerator.resolution + "\n" +
            "noiseScale: " + terrainGenerator.noiseScale.ToString("0.###") + "\n" +
            "heightMultiplier: " + terrainGenerator.heightMultiplier.ToString("0.###") + "\n" +
            "iterations: " + erosionController.iterations + "\n" +
            "talusThreshold: " + erosionController.talusThreshold.ToString("0.###") + "\n" +
            "erosionStrength: " + erosionController.erosionStrength.ToString("0.###");
    }

    private void FindSceneObjectsIfNeeded()
    {
        if (terrainGenerator == null)
        {
            terrainGenerator = FindFirstObjectByType<TerrainGenerator>();
        }

        if (erosionController == null)
        {
            erosionController = FindFirstObjectByType<ErosionController>();
        }
    }
}
