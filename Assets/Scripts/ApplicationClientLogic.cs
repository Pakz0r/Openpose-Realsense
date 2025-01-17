using System.Collections;
using Unity.Netcode.Transports.UTP;
using Unity.Netcode;
using UnityEngine;

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
    #endregion

    #region Unity Lifecycle
    void OnEnable()
    {
        networkManager = ApplicationLogic.GetNetworkManager();

        if (networkManager == null)
            return;

        if (networkManager.gameObject.TryGetComponent(out networkTransport))
        {
            var config = ApplicationLogic.Config;

            if (config.ServerPort > 0)
            {
                networkTransport.ConnectionData.Port = config.ServerPort;
            }

            if (!string.IsNullOrEmpty(config.ServerAddress))
            {
                networkTransport.ConnectionData.Address = config.ServerAddress;
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