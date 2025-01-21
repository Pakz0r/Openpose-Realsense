using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Utilities.Parser;
using Tymski;
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
    [Header("Logic Scene Setup")]
    [SerializeField]
    private SceneReference serverLogicScene;
    [SerializeField]
    private SceneReference clientLogicScene;

    [Header("Client Camera Rig Scene Setup")]
    [SerializeField]
    private SceneReference desktopCameraScene;
    [SerializeField]
    private SceneReference vrCameraScene;

    [Header("Networking")]
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

        // scene logic load
        string addictionalLogicScene = Mode switch
        {
            ApplicationMode.Server => (string)serverLogicScene,
            ApplicationMode.Client => (string)clientLogicScene,
            _ => String.Empty,
        };

        if (!IsSceneLoaded(addictionalLogicScene))
            SceneManager.LoadScene(addictionalLogicScene, LoadSceneMode.Additive);

        // client camera rig scene load
        if (Mode == ApplicationMode.Client)
        {
            string cameraLogicScene;

#if UNITY_ANDROID
            if (
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("XR_SELECTED_RUNTIME_JSON")) // Verifica presenza del simulatore meta XR
                || SystemInfo.deviceModel.Contains("Oculus") // Verifica il modello del dispositivo
                )
            {
                Debug.Log("Meta Quest rilevato. Inizializzazione XR...");
                cameraLogicScene = vrCameraScene;
                InitializeXR();
            }
            else
#endif
            {
                Debug.Log("Dispositivo non compatibile con XR. Avvio in modalità normale.");
                cameraLogicScene = desktopCameraScene;
            }

            if (!IsSceneLoaded(cameraLogicScene))
                SceneManager.LoadScene(cameraLogicScene, LoadSceneMode.Additive);
        }

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

        DeinitializeXR();
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

    private bool IsSceneLoaded(string scenePath)
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

    private void InitializeXR()
    {
        var xrManager = XRGeneralSettings.Instance.Manager;

        if (xrManager != null)
        {
            xrManager.InitializeLoaderSync();

            if (xrManager.activeLoader == null)
            {
                Debug.LogError("Errore durante l'inizializzazione di XR.");
                return;
            }

            xrManager.StartSubsystems();
            Debug.Log("XR inizializzato con successo.");
        }
    }

    private void DeinitializeXR()
    {
        var xrManager = XRGeneralSettings.Instance.Manager;

        if (xrManager != null && xrManager.activeLoader != null)
        {
            xrManager.StopSubsystems();
            xrManager.DeinitializeLoader();
        }
    }
    #endregion
}