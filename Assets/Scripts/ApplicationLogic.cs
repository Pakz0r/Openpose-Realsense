using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utilities.Parser;
using Tymski;
using Unity.Netcode;
using static SensorsManager;

public class ApplicationLogic : MonoBehaviour
{
    #region Config Classes
    public enum ApplicationMode
    {
        Server,
        Client,
    }

    [Serializable]
    public class ApplicationConfig
    {
        public string Mode;
        public float MinConfidence;
        public string EnvironmentScene;
        public bool DisableEnvironmentScene;
        public string ServerAddress;
        public ushort ServerPort;
    }
    #endregion

    #region Serialize Fields
    [SerializeField]
    private SceneReference serverLogicScene;
    [SerializeField]
    private SceneReference clientLogicScene;
    [SerializeField]
    private NetworkManager networkManager;
    #endregion

    #region Public Fields
    public static ApplicationMode Mode { get; private set; }
    public static ApplicationConfig Config { get; private set; }
    public static ApplicationLogic Instance { get; private set; }
    #endregion

    #region Unity Lifecycle
    private async void OnEnable()
    {
        if (Instance == null)
            Instance = this;

        // setup scene loading
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        // config parse
        string filePath = Path.Combine(Application.dataPath, "../config.json");

        if (!File.Exists(filePath))
        {
            Debug.LogError($"Config not found at '{filePath}'");
            return;
        }

        Config = await JSON.ParseFromFileAsync<ApplicationConfig>(filePath);

        if (Enum.TryParse(Config.Mode, true, out ApplicationMode applicationMode))
        {
            Mode = applicationMode;
        }

        if (Config.ServerPort == 0)
            Config.ServerPort = 7777; // Default server port

        if (!Config.DisableEnvironmentScene && !string.IsNullOrEmpty(Config.EnvironmentScene) && !IsSceneLoaded(Config.EnvironmentScene))
            SceneManager.LoadScene(Config.EnvironmentScene, LoadSceneMode.Additive);

        string addictionalLogicScene = Mode switch
        {
            ApplicationMode.Server => (string)serverLogicScene,
            ApplicationMode.Client => (string)clientLogicScene,
            _ => String.Empty,
        };

        if (!IsSceneLoaded(addictionalLogicScene))
            SceneManager.LoadScene(addictionalLogicScene, LoadSceneMode.Additive);

#if UNITY_EDITOR
        // setup sensor manager listner
        SensorsManager.SensorCreated.RemoveListener(CreateSimulationOnSensorCreated);
        SensorsManager.SensorCreated.AddListener(CreateSimulationOnSensorCreated);
#endif
    }

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;

        if (networkManager != null)
            networkManager.Shutdown();
    }
    #endregion

    #region Public Methods
    public static NetworkManager GetNetworkManager()
    {
        return Instance == null ? null : Instance.networkManager;
    }
    #endregion

    #region Private Methods
#if UNITY_EDITOR
    private async void CreateSimulationOnSensorCreated(Sensor sensor)
    {
        var directoryInfo = new DirectoryInfo(sensor.Folder);

        if (directoryInfo.Exists)
        {
            foreach (var file in directoryInfo.GetFiles())
            {
                file.Delete();
            }
        }

        for (int i = 0; i < 34; i++)
        {
            await Task.Delay(2000);
            var streamingAssetFile = Path.Combine(Application.streamingAssetsPath, Config.EnvironmentScene, $"frame{i}_skeletonsPoints3D.json");
            var sensorFolderFile = Path.Combine(sensor.Folder, $"frame{i}_skeletonsPoints3D.json");
            File.Copy(streamingAssetFile, sensorFolderFile);
        }
    }
#endif

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        if (scene.name.Equals(Config.EnvironmentScene))
        {
            SceneManager.SetActiveScene(scene);
        }
    }

    bool IsSceneLoaded(string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath))
            return false;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (scene.path == scenePath || scene.path.Contains(scenePath))
            {
                return scene.isLoaded;
            }
        }

        return false;
    }
#endregion
}