using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Linq;
using System.IO;
using System;

using static SensorsManager;

public class SensorSimulator : MonoBehaviour
{
    #region Private Fields
    private string environmentScene;
    private bool sensorRegistered = false;
    private int simulatedFrameCount = 0;
    private int frameId = 0;
    #endregion

    #region Unity Lifecycle
#if UNITY_EDITOR
    private void OnEnable()
    {
        environmentScene = ApplicationConfig.Instance.EnvironmentScene;

        // setup sensor manager listner
        SensorCreated.RemoveListener(CreateSimulationOnSensorCreated);
        SensorCreated.AddListener(CreateSimulationOnSensorCreated);
    }

    private void OnDisable()
    {
        SensorCreated.RemoveListener(CreateSimulationOnSensorCreated);
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 250, 200));

        if (!sensorRegistered)
        {
            GUILayout.Label($"Waiting '{environmentScene}' sensors initialize");
        }
        else
        {
            GUILayout.Label($"Started simulation for '{environmentScene}'");
            GUILayout.Label($"Drawing frame {frameId} of {simulatedFrameCount}");

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Previus Frame") && frameId > 0)
            {
                frameId--;
            }

            if (GUILayout.Button("Next Frame") && frameId < simulatedFrameCount)
            {
                frameId++;
            }

            GUILayout.EndHorizontal();
        }

        GUILayout.EndArea();
    }
#endif
    #endregion

    #region Private Methods
    private async void CreateSimulationOnSensorCreated(SensorInfo sensor)
    {
        if (sensorRegistered)
        {
            Debug.LogError($"Simulator: Simulation already running for one sensor");
            return;
        }

        var sensorFolderInfo = new DirectoryInfo(sensor.Folder);

        if (sensorFolderInfo.Exists)
        {
            foreach (var file in sensorFolderInfo.GetFiles())
            {
                file.Delete();
            }
        }
        else
        {
            Directory.CreateDirectory(sensor.Folder);
        }

        var simulationFolder = Path.Combine(Application.streamingAssetsPath, environmentScene);
        var simulationFolderInfo = new DirectoryInfo(simulationFolder);

        if (!simulationFolderInfo.Exists)
        {
            Debug.LogError($"Simulator: Folder '{simulationFolder}' not exists");
            return;
        }

        var simulatedFiles = simulationFolderInfo.GetFiles()
            .Where((info) => !info.Name.Contains(".meta"))
            .ToList();

        if (simulatedFiles.Count == 0)
        {
            Debug.LogError($"Simulator: Folder '{simulationFolder}' has no frames");
            return;
        }

        SensorCreated.RemoveListener(CreateSimulationOnSensorCreated);
        simulatedFrameCount = simulatedFiles.Count - 1;
        sensorRegistered = true;
        var simulatedFrameId = 0;

        while (Application.isPlaying)
        {
            var currentFrameid = frameId;

            try
            {
                var frameName = $"frame{currentFrameid}_";
                var simulatedFile = simulatedFiles.FirstOrDefault((info) => info.Name.Contains(frameName));
                var streamingAssetFile = simulatedFile.FullName;
                var sensorFolderFile = Path.Combine(sensor.Folder, $"frame{simulatedFrameId}_skeletonsPoints3D.json");
                File.Copy(streamingAssetFile, sensorFolderFile);
                simulatedFrameId++;
            }
            catch (Exception ex) { Debug.LogException(ex); }

            await UniTask.WaitUntil(() => frameId != currentFrameid, cancellationToken: destroyCancellationToken);
        }
    }
    #endregion
}