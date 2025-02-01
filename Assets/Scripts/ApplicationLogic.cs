using System;
using System.IO;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Tymski;
using Cysharp.Threading.Tasks;

public class ApplicationLogic : MonoBehaviour
{
    #region Config Classes
    public enum ApplicationMode
    {
        Server,
        Client,
    }

    public enum SupportedHeadset
    {
        None,
        Quest1,
        Quest2,
        QuestPro,
        Quest3
    }
    #endregion

    #region Serialize Fieldsù
    [Header("Application Setup")]
    [SerializeField]
    private ApplicationConfig config;

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
    public static SupportedHeadset CurrentHMD { get; private set; }
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
#if UNITY_ANDROID && !UNITY_EDITOR
        string filePath = Path.Combine(Application.persistentDataPath, "config.json");
#else
        string filePath = Path.Combine(Application.dataPath, "../config.json");
#endif

        await config.ParseFromFile(filePath);

        if (Enum.TryParse(config.Mode, true, out ApplicationMode applicationMode))
        {
            Mode = applicationMode;
        }

        if (!config.DisableEnvironmentScene && !string.IsNullOrEmpty(config.EnvironmentScene) && !IsSceneLoaded(config.EnvironmentScene))
            SceneManager.LoadScene(config.EnvironmentScene, LoadSceneMode.Additive);

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
            string cameraLogicScene = desktopCameraScene;

            if (IsRunningOnHMD(out var hmd))
            {
                Debug.Log("Dispositivo HMD rilevato. Avvio in modalità VR.");
                cameraLogicScene = vrCameraScene;
                CurrentHMD = hmd;
            }
            else
            {
                Debug.Log("Nessun dispositivo HMD rilevato. Avvio in modalità desktop.");
                DeinitializeXR().Forget();
            }

            if (!IsSceneLoaded(cameraLogicScene))
                SceneManager.LoadScene(cameraLogicScene, LoadSceneMode.Additive);
        }
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
    private void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        if (scene.name.Equals(config.EnvironmentScene))
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

    private async UniTask DeinitializeXR()
    {
        var xrManager = XRGeneralSettings.Instance.Manager;

        if (xrManager != null && xrManager.activeLoader != null)
        {
            await UniTask.WaitUntil(() => xrManager.isInitializationComplete);
            xrManager.StopSubsystems();
            xrManager.DeinitializeLoader();
        }
    }

    private static bool IsRunningOnHMD(out SupportedHeadset headset)
    {
#if UNITY_ANDROID
        using var build = new AndroidJavaClass("android.os.Build");
        string device = build.GetStatic<string>("DEVICE");

        headset = device switch
        {
            "miramar" => SupportedHeadset.Quest1,
            "hollywood" => SupportedHeadset.Quest2,
            "seacliff" => SupportedHeadset.QuestPro,
            "eureka" => SupportedHeadset.Quest3,
            _ => SupportedHeadset.None
        };
#else
        headset = SupportedHeadset.None;
#endif
        return headset != SupportedHeadset.None;
    }
    #endregion
}