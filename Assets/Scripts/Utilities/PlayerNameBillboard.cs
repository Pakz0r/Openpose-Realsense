using TMPro;
using Unity.Netcode;
using UnityEngine;
using Unity.Collections;

public class PlayerNameBillboard : NetworkBehaviour
{
    public enum BillboardType { LookAtCamera, CameraForward };

    #region Serialized Fields
    [SerializeField]
    private BillboardType billboardType;

    [SerializeField]
    private Camera cameraReference;

    [SerializeField]
    private TMP_Text playerLabel;

    [Header("Lock Rotation")]
    [SerializeField]
    private bool lockX;
    [SerializeField]
    private bool lockY;
    [SerializeField]
    private bool lockZ;
    #endregion

    #region Private Fields
    private Vector3 originalRotation;
    private NetworkVariable<FixedString128Bytes> playerName = new();
    #endregion

    #region Unity Lifecycle
    public override void OnNetworkSpawn()
    {
        originalRotation = transform.rotation.eulerAngles;

        if (this.playerLabel != null)
        {
            this.playerLabel.text = playerName.Value.ToString();
        }

        playerName.OnValueChanged -= OnNetworkPlayerNameChange;
        playerName.OnValueChanged += OnNetworkPlayerNameChange;
    }

    public override void OnNetworkDespawn()
    {
        playerName.OnValueChanged -= OnNetworkPlayerNameChange;
    }

    void LateUpdate()
    {
        if (cameraReference == null)
        {
            cameraReference = Camera.main;

            if (cameraReference == null)
                cameraReference = GameObject.FindAnyObjectByType<Camera>();
        }

        switch (billboardType)
        {
            case BillboardType.LookAtCamera:
                transform.LookAt(cameraReference.transform.position, Vector3.up);
                break;
            case BillboardType.CameraForward:
                transform.forward = cameraReference.transform.forward;
                break;
            default:
                break;
        }

        Vector3 rotation = transform.rotation.eulerAngles;

        if (lockX) 
            rotation.x = originalRotation.x;

        if (lockY)
            rotation.y = originalRotation.y;

        if (lockZ)
            rotation.z = originalRotation.z;

        transform.rotation = Quaternion.Euler(rotation);
    }
    #endregion

    #region Public Methods
    public void SetPlayerName(string label)
    {
        playerName.Value = label;
    }
    #endregion

    #region Private Methods
    private void OnNetworkPlayerNameChange(FixedString128Bytes previous, FixedString128Bytes current)
    {
        Debug.Log($"Detected NetworkVariable 'playerName' Change: Previous: {previous} | Current: {current}");

        if (this.playerLabel != null)
        {
            this.playerLabel.text = current.ConvertToString();
        }
    }
    #endregion
}