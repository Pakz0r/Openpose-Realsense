using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utilities.Parser;
using Tymski;
using Unity.Netcode;

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
    #endregion

    #region Unity Lifecycle
    private async void OnEnable()
    {
        // setup scene loading
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

        if (!string.IsNullOrEmpty(Config.EnvironmentScene))
        {
            if (!IsSceneLoaded(Config.EnvironmentScene))
                SceneManager.LoadScene(Config.EnvironmentScene, LoadSceneMode.Additive);
        }

        switch (Mode)
        {
            case ApplicationMode.Server:
                if (!IsSceneLoaded(serverLogicScene))
                    SceneManager.LoadScene(serverLogicScene, LoadSceneMode.Additive);

                if (networkManager != null)
                    networkManager.StartServer();

                break;
            case ApplicationMode.Client:
                if (!IsSceneLoaded(clientLogicScene))
                    SceneManager.LoadScene(clientLogicScene, LoadSceneMode.Additive);

                if (networkManager != null)
                    networkManager.StartClient();

                break;
        }
    }

    private void OnDisable()
    {
        if (networkManager != null)
            networkManager.Shutdown();
    }
    #endregion

    #region Private Methods
    private void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        if (scene.name.Equals(Config.EnvironmentScene))
        {
            SceneManager.SetActiveScene(scene);
        }
    }

    bool IsSceneLoaded(string scenePath)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (scene.path == scenePath)
            {
                return scene.isLoaded;
            }
        }

        return false;
    }
    #endregion
}