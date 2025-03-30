using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

public class NetworkRoomBehaviour : NetworkBehaviour
{
    private NetworkVariable<bool> hasAnyFall = new();

    #region Unity Lifecycle
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // sync variables with clients
            SetHasAnyFallen(hasAnyFall.Value);
            return;
        }

        // only if application is running on client register handlers
        hasAnyFall.OnValueChanged -= OnNetworkHasAnyFallParameterChange;
        hasAnyFall.OnValueChanged += OnNetworkHasAnyFallParameterChange;
    }

    public override void OnNetworkDespawn()
    {
        hasAnyFall.OnValueChanged -= OnNetworkHasAnyFallParameterChange;
    }
    #endregion

    #region Public Methods
    public void SetHasAnyFallen(bool value)
    {
        if (!IsServer) return; // this method can only be called on server
        hasAnyFall.Value = !hasAnyFall.Value; // force value change event
        hasAnyFall.Value = value;
        EventManager.TriggerEvent("RoomMonitorFallenState", value);
    }
    #endregion

    #region Private Methods
    private async void OnNetworkHasAnyFallParameterChange(bool previous, bool current)
    {
        await UniTask.Yield();
        Debug.Log($"Detected NetworkVariable 'hasAnyFallen' Change: Previous: {previous} | Current: {current}");
        EventManager.TriggerEvent("RoomMonitorFallenState", current);
    }
    #endregion
}
