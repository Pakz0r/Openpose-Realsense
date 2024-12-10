using UnityEngine.Animations.Rigging;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(OverrideTransform))]
public class NetworkBoneSynchronization : NetworkBehaviour
{
    private OverrideTransform constraint;
    private NetworkVariable<float> positionWeight = new();
    private NetworkVariable<float> rotationWeight = new();

    public void SetWeightPosition(float value)
    {
        positionWeight.Value = value;
    }

    public void SetWeightRotation(float value)
    {
        rotationWeight.Value = value;
    }

    public override void OnNetworkSpawn()
    {
        constraint = this.GetComponent<OverrideTransform>();

        if (IsServer)
        {
            // init variables
            SetWeightPosition(constraint.weight);
            SetWeightRotation(constraint.data.rotationWeight);
            return;
        }

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
}
