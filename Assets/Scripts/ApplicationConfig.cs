using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Utilities.Parser;

#if UNITY_EDITOR
using System.Net;
using System.Linq;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
#endif

[Serializable]
[CreateAssetMenu(fileName = "config", menuName = "AIWatch/ApplicationConfig", order = 1)]
public class ApplicationConfig : ScriptableObject
#if UNITY_EDITOR
    , IPreprocessBuildWithReport
#endif
{
    #region Internal Config Class
    [Serializable]
    private class Config
    {
        public string Mode;
        public float MinConfidence;
        public string EnvironmentScene;
        public bool DisableEnvironmentScene;
        public string ServerAddress;
        public ushort ServerPort;
    }
    #endregion

    #region Serialized Fields
    [field: SerializeField]
    public string Mode { get; private set; }

    [field: SerializeField]
    public float MinConfidence { get; private set; }

    [field: SerializeField]
    public string EnvironmentScene { get; private set; }

    [field: SerializeField]
    public bool DisableEnvironmentScene { get; private set; }

    [field: SerializeField]
    public string ServerAddress { get; private set; }

    [field: SerializeField]
    public ushort ServerPort { get; private set; }
    #endregion

    #region Public Fields
    public string FilePath { get; private set; }
    public static ApplicationConfig Instance { get; private set; }

#if UNITY_EDITOR
    public int callbackOrder => 0;
#endif
    #endregion

    #region Public Methods
    public async Task ParseFromFile(string filePath)
    {
        Instance = this;
        FilePath = filePath;

        var configFileExists = File.Exists(filePath);

        if (configFileExists)
        {
            // json cannot serialize ScriptableObjects
            var temp = await JSON.ParseFromFileAsync<Config>(filePath);

            if (temp != null)
            {
                if (!string.IsNullOrEmpty(temp.Mode))
                    Mode = temp.Mode;

                if (temp.MinConfidence <= 0f)
                    MinConfidence = temp.MinConfidence;

                if (!string.IsNullOrEmpty(temp.EnvironmentScene))
                    EnvironmentScene = temp.EnvironmentScene;

                if (!string.IsNullOrEmpty(temp.ServerAddress))
                    ServerAddress = temp.ServerAddress;

                if (temp.ServerPort <= 0)
                    ServerPort = temp.ServerPort;
            }
        }

        if (ServerPort == 0)
            ServerPort = 7777; // default server port

#if UNITY_ANDROID && !UNITY_EDITOR
        Mode = "Client"; // android has only client mode
#endif

        if (!configFileExists)
            SaveToFile(); // on android devices save config on first time load (to simplify testing) 
    }

    public async void SaveToFile() => await JSON.ComposeToFileAsync(
        new Config()
        {
            Mode = Mode,
            MinConfidence = MinConfidence,
            EnvironmentScene = EnvironmentScene,
            ServerAddress = ServerAddress,
            ServerPort = ServerPort
        }, FilePath, true);

#if UNITY_EDITOR
    public void OnPreprocessBuild(BuildReport report)
    {
        // automatic update the server address on build to simplify testing and 
        IPHostEntry localhost = Dns.GetHostEntry(Dns.GetHostName());
        var address = NetworkInterface.GetAllNetworkInterfaces()
                                .Where(i => i.OperationalStatus == OperationalStatus.Up)
                                .SelectMany(i => i.GetIPProperties().UnicastAddresses)
                                .Where(i => i.Address.AddressFamily == AddressFamily.InterNetwork)
                                .Select(i => i.Address)
                                .FirstOrDefault();

        var config = AssetDatabase.LoadAssetAtPath<ApplicationConfig>("Assets/config.asset");
        config.ServerAddress = address.ToString();

#if UNITY_ANDROID
        config.Mode = "Client"; // android has only client mode
#endif

        AssetDatabase.SaveAssets();
    }
#endif

    #endregion
}
