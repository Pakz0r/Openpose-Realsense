using UnityEngine.Animations.Rigging;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(OverrideTransform))]
public class NetworkBoneSynchronization : NetworkBehaviour
{
    private OverrideTransform constraint;
    private NetworkVariable<float> positionWeight = new();
    private NetworkVariable<float> rotationWeight = new();

    #region Unity Lifecycle
    public override void OnNetworkSpawn()
    {
        constraint = this.GetComponent<OverrideTransform>();

        if (IsServer)
        {
            // sync variables with clients
            SetWeightPosition(constraint.weight);
            SetWeightRotation(constraint.data.rotationWeight);
            return;
        }

        // only if application is running on client register handlers

        positionWeight.OnValueChanged -= OnNetworkBonePositionWeightChange;
        positionWeight.OnValueChanged += OnNetworkBonePositionWeightChange;

        rotationWeight.OnValueChanged -= OnNetworkBoneRotationWeightChange;
        rotationWeight.OnValueChanged += OnNetworkBoneRotationWeightChange;
    }

    public override void OnNetworkDespawn()
    {
        positionWeight.OnValueChanged -= OnNetworkBonePositionWeightChange;
        rotationWeight.OnValueChanged -= OnNetworkBonePositionWeightChange;
    }
    #endregion

    #region Public Methods
    public void SetWeightPosition(float value)
    {
        if (!IsServer) return; // this method can only be called on server
        positionWeight.Value = value;
        constraint.weight = value;
    }

    public void SetWeightRotation(float value)
    {
        if (!IsServer) return; // this method can only be called on server
        rotationWeight.Value = value;
        constraint.data.rotationWeight = value;
    }
    #endregion

    #region Private Methods
    private void OnNetworkBonePositionWeightChange(float previous, float current)
    {
        Debug.Log($"Detected NetworkVariable 'positionWeight' Change: Previous: {previous} | Current: {current}");
        constraint.weight = current;
    }

    private void OnNetworkBoneRotationWeightChange(float previous, float current)
    {
        Debug.Log($"Detected NetworkVariable 'rotationWeight' Change: Previous: {previous} | Current: {current}");
        constraint.data.rotationWeight = current;
    }
    #endregion
}
