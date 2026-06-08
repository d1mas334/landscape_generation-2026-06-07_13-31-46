using UnityEngine;

public class ErosionController : MonoBehaviour
{
    [Header("Связи")]
    public TerrainGenerator terrainGenerator;
    public ComputeShader thermalErosionShader;

    [Header("Параметры эрозии")]
    [Min(1)]
    public int iterations = 40;

    [Min(0f)]
    public float talusThreshold = 0.015f;

    [Range(0f, 1f)]
    public float erosionStrength = 0.4f;

    [ContextMenu("Run Compute Erosion")]
    public void RunComputeErosion()
    {
        RunComputeErosion(iterations);
    }

    [ContextMenu("Run Single Compute Iteration")]
    public void RunSingleComputeIteration()
    {
        RunComputeErosion(1);
    }

    [ContextMenu("Reset Terrain")]
    public void ResetTerrain()
    {
        if (terrainGenerator == null)
        {
            terrainGenerator = GetComponent<TerrainGenerator>();
        }

        if (terrainGenerator == null)
        {
            Debug.LogError("ErosionController: не назначен TerrainGenerator, сброс невозможен.");
            return;
        }

        terrainGenerator.ResetTerrain();
    }

    public bool CanRunComputeErosion(out string statusMessage)
    {
        if (terrainGenerator == null)
        {
            terrainGenerator = GetComponent<TerrainGenerator>();
        }

        if (terrainGenerator == null)
        {
            statusMessage = "TerrainGenerator is not assigned";
            return false;
        }

        if (thermalErosionShader == null)
        {
            statusMessage = "Compute shader is not assigned";
            return false;
        }

        if (!SystemInfo.supportsComputeShaders)
        {
            statusMessage = "Compute shaders are not supported on this device";
            return false;
        }

        statusMessage = "";
        return true;
    }

    private void RunComputeErosion(int iterationCount)
    {
        if (!CanRunComputeErosion(out string statusMessage))
        {
            Debug.LogError("ErosionController: " + statusMessage);
            return;
        }

        iterationCount = Mathf.Max(1, iterationCount);
        int resolution = terrainGenerator.GetResolution();
        float[] heights = terrainGenerator.GetHeightMapCopy();

        int kernel = thermalErosionShader.FindKernel("CSMain");
        int threadGroups = Mathf.CeilToInt(resolution / 8f);

        ComputeBuffer inputBuffer = null;
        ComputeBuffer outputBuffer = null;

        try
        {
            // Создаем ComputeBuffer, чтобы хранить heightmap в памяти GPU.
            inputBuffer = new ComputeBuffer(heights.Length, sizeof(float));
            outputBuffer = new ComputeBuffer(heights.Length, sizeof(float));

            // Отправляем исходную heightmap на GPU через SetData.
            inputBuffer.SetData(heights);
            outputBuffer.SetData(heights);

            // Задаем параметры compute shader один раз перед итерациями.
            thermalErosionShader.SetInt("Resolution", resolution);
            thermalErosionShader.SetFloat("TalusThreshold", Mathf.Max(0f, talusThreshold));
            thermalErosionShader.SetFloat("ErosionStrength", Mathf.Clamp01(erosionStrength));

            for (int i = 0; i < iterationCount; i++)
            {
                thermalErosionShader.SetBuffer(kernel, "InputHeights", inputBuffer);
                thermalErosionShader.SetBuffer(kernel, "OutputHeights", outputBuffer);

                // Dispatch запускает группы GPU-потоков, которые обрабатывают точки heightmap.
                thermalErosionShader.Dispatch(kernel, threadGroups, threadGroups, 1);

                // Ping-pong смена буферов: новый результат становится входом следующей итерации.
                ComputeBuffer temp = inputBuffer;
                inputBuffer = outputBuffer;
                outputBuffer = temp;
            }

            // После последней итерации возвращаем heightmap из GPU обратно в C#.
            inputBuffer.GetData(heights);
        }
        finally
        {
            // Освобождаем GPU-буферы, чтобы не оставлять память занятой.
            if (inputBuffer != null)
            {
                inputBuffer.Release();
            }

            if (outputBuffer != null)
            {
                outputBuffer.Release();
            }
        }

        // Передаем измененную heightmap в TerrainGenerator, он пересобирает mesh.
        terrainGenerator.SetHeightMap(heights);
    }

    private void OnValidate()
    {
        iterations = Mathf.Max(1, iterations);
        talusThreshold = Mathf.Max(0f, talusThreshold);
        erosionStrength = Mathf.Clamp01(erosionStrength);
    }
}
