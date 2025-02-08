using Cysharp.Threading.Tasks;
using UnityEngine.Animations.Rigging;
using UnityEngine;
using Unity.Netcode;
using System;

[RequireComponent(typeof(OverrideTransform))]
public class NetworkBoneSynchronization : NetworkBehaviour
{
    #region Serialized Fields
    [SerializeField]
    private OpenPoseBone boneId;
    [SerializeField]
    private Transform targetBone;
    #endregion

    #region Private Fields
    private OverrideTransform constraint;
    private NetworkVariable<float> positionWeight = new();
    private NetworkVariable<float> rotationWeight = new();
    #endregion

    #region Unity Lifecycle
    public void Awake()
    {
        if (Enum.TryParse<OpenPoseBone>(this.name, true, out var id))
            boneId = id;

        constraint = this.GetComponent<OverrideTransform>();
        constraint.data.sourceObject = null; // doesn't need source object
        constraint.data.space = OverrideTransformData.Space.World; // update work only in world space

        // we are working only with rotations so ignore positions (except for the hips)
        constraint.data.positionWeight = this.name.Equals("Hips") ? 1f : 0f;

        // check for bone to look at
        var boneTargetId = boneId.GetLookAtBoneFrom();

        if (boneTargetId != OpenPoseBone.Invalid)
        {
            var boneTargetName = Enum.GetName(typeof(OpenPoseBone), boneTargetId);

            // query rig childs transforms to search for target bone
            foreach (Transform bone in this.transform.parent)
            {
                if (bone.name == boneTargetName)
                {
                    targetBone = bone;
                }
            }
        }
    }

    public override void OnNetworkSpawn()
    {
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
        constraint.weight = positionWeight.Value; // update constraint value on spawn

        rotationWeight.OnValueChanged -= OnNetworkBoneRotationWeightChange;
        rotationWeight.OnValueChanged += OnNetworkBoneRotationWeightChange;
        constraint.data.rotationWeight = rotationWeight.Value; // update constraint value on spawn
    }

    public override void OnNetworkDespawn()
    {
        positionWeight.OnValueChanged -= OnNetworkBonePositionWeightChange;
        rotationWeight.OnValueChanged -= OnNetworkBonePositionWeightChange;
    }

    public void LateUpdate()
    {
        if (targetBone != null)
        {
            var direction = (targetBone.position - this.transform.position).normalized; // eval direction from bone to target
            this.transform.up = direction; // update the bone forward (up) to point at target bone
        }

        if (constraint != null)
        {
            constraint.data.position = this.transform.position; // update bone rotations
            constraint.data.rotation = this.transform.rotation.eulerAngles; // update bone rotations
        }
    }
    #endregion

    #region Public Methods
    public void SetWeightPosition(float value)
    {
        if (!IsServer) return; // this method can only be called on server
        positionWeight.Value = value + 0.1f; // force value change event
        positionWeight.Value = value;
        constraint.weight = value;
    }

    public void SetWeightRotation(float value)
    {
        if (!IsServer) return; // this method can only be called on server
        rotationWeight.Value = value + 0.1f; // force value change event
        rotationWeight.Value = value;
        constraint.data.rotationWeight = value;
    }
    #endregion

    #region Private Methods
    // Control the bone position weight parameter (to be executed on main thread cause unity JOB system)
    private async void OnNetworkBonePositionWeightChange(float previous, float current)
    {
        await UniTask.Yield();
        Debug.Log($"Detected NetworkVariable 'positionWeight' change for '{this.name}' (Prev: {previous} | Current: {current})");
        constraint.weight = current;
    }

    // Control the bone rotation weight parameter (to be executed on main thread cause unity JOB system)
    private async void OnNetworkBoneRotationWeightChange(float previous, float current)
    {
        await UniTask.Yield();
        Debug.Log($"Detected NetworkVariable 'rotationWeight' change for '{this.name}' (Prev: {previous} | Current: {current})");
        constraint.data.rotationWeight = current;
    }
    #endregion
}
