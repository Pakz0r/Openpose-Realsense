using System.Collections;
using Unity.Netcode.Transports.UTP;
using Unity.Netcode;
using UnityEngine;
using System.Net;

public class ApplicationClientLogic : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    private int connectRetryDelay = 5;
    #endregion

    #region Private Fields
    private NetworkManager networkManager;
    private UnityTransport networkTransport;
    private Coroutine connectCoroutine;
    private bool isRunningOnHMD;
    #endregion

    #region Unity Lifecycle
    void OnEnable()
    {
        networkManager = ApplicationLogic.GetNetworkManager();
        isRunningOnHMD = ApplicationLogic.CurrentHMD != ApplicationLogic.SupportedHeadset.None;

        if (networkManager == null)
            return;

        if (networkManager.gameObject.TryGetComponent(out networkTransport))
        {
            if (ApplicationConfig.Instance.ServerPort > 0)
            {
                networkTransport.ConnectionData.Port = ApplicationConfig.Instance.ServerPort;
            }

            if (!string.IsNullOrEmpty(ApplicationConfig.Instance.ServerAddress))
            {
                networkTransport.ConnectionData.Address = ApplicationConfig.Instance.ServerAddress;
            }

            networkManager.OnClientConnectedCallback += OnClientConnect;
            networkManager.OnClientDisconnectCallback += OnClientDisconnect;
        }

        StartClientConnection();
    }

    void OnDisable()
    {
        if (networkManager == null)
            return;

        networkManager.OnClientConnectedCallback -= OnClientConnect;
        networkManager.OnClientDisconnectCallback -= OnClientDisconnect;
    }

    private void OnGUI()
    {
        if (networkManager == null || isRunningOnHMD) return;

        GUILayout.BeginArea(new Rect(10, 10, 250, 200));

        if (networkManager.IsConnectedClient)
        {
            GUILayout.Label($"Connected to server {networkTransport.ConnectionData.Address}:{networkTransport.ConnectionData.Port}.");

            if (GUILayout.Button("Disconnect"))
            {
                networkManager.Shutdown();
                StopCoroutine(connectCoroutine);
                connectCoroutine = null;
            }
        }
        else if (connectCoroutine != null)
        {
            GUILayout.Label($"Connecting to server {networkTransport.ConnectionData.Address}:{networkTransport.ConnectionData.Port}...");
            
            if (GUILayout.Button("Disconnect"))
            {
                StopCoroutine(connectCoroutine);
                connectCoroutine = null;
            }
        }
        else
        {
            GUILayout.Label("Insert the server address:");
            var address = GUILayout.TextField(networkTransport.ConnectionData.Address);

            GUILayout.Label("Insert the server port:");
            var port = GUILayout.TextField(networkTransport.ConnectionData.Port.ToString());

            if (GUILayout.Button("Connect"))
            {
                networkTransport.ConnectionData.Address = address;
                networkTransport.ConnectionData.Port = ushort.Parse(port);
                StartClientConnection();
            }
        }

        GUILayout.EndArea();
    }
    #endregion

    #region Private Methods
    private void OnClientDisconnect(ulong clientId)
    {
        // check if network connection failed
        if (clientId == 0)
            return;

        Debug.Log($"[Network] Client {clientId} Disconnected");
        StartClientConnection();
    }

    private void OnClientConnect(ulong clientId)
    {
        Debug.Log($"[Network] Client Connected with ID {clientId}");
    }

    private void StartClientConnection()
    {
        if (connectCoroutine != null)
            StopCoroutine(connectCoroutine);

        connectCoroutine = StartCoroutine(StartClientConnectInternal());
    }

    private IEnumerator StartClientConnectInternal()
    {
        if (networkManager == null)
            yield break;

        while (true)
        {
            Debug.Log("[Network] Trying to connect to server...");

            networkManager.StartClient();
            yield return new WaitForSeconds(0.2f + networkTransport.ConnectTimeoutMS / 1000);

            if (networkManager.IsConnectedClient)
                break;

            Debug.Log($"[Network] Retry connect in {connectRetryDelay} seconds...");
            yield return new WaitForSeconds(connectRetryDelay);
        }

        connectCoroutine = null;
    }
    #endregion
}