using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine.Animations.Rigging;
using UnityEngine;
using OpenPose;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UniRx;

public class ApplicationServerLogic : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    private GameObject personPrefab;
    #endregion

    #region Private Fields
    private NetworkManager networkManager;
    private Dictionary<string, GameObject> peoples = new();
    #endregion

    #region Unity Lifecycle
    private void OnEnable()
    {
        networkManager = ApplicationLogic.GetNetworkManager();

        if (networkManager == null)
            return;

        if (networkManager.gameObject.TryGetComponent<UnityTransport>(out var networkTransport))
        {
            if (ApplicationConfig.Instance.ServerPort > 0)
            {
                networkTransport.ConnectionData.Port = ApplicationConfig.Instance.ServerPort;
            }
        }

        networkManager.StartServer();
        SensorWatcher.PersonUpdated.AddListener(DrawPerson);
    }

    private void OnDisable()
    {
        SensorWatcher.PersonUpdated.RemoveAllListeners();
    }
    #endregion

    #region Private Methods
    private void DrawPerson(SensorWatcher sender, PeopleData personData)
    {
        if (personData == null)
        {
            Debug.LogError("Cannot find person data");
            return;
        }

        // eval frame confidence
        var avgConfidence = personData.skeleton.Average((bone) => bone.confidence);
        Debug.Log($"Person {personData.personID} new frame average confidence: {avgConfidence}");

        // create person if not exists
        var personObject = CreatePersonIfNotExists(personData.personID);

        if (personObject == null)
        {
            Debug.LogError("Cannot find person object");
            return;
        }

        // update person transform parent
        personObject.transform.SetParent(sender.transform);

        // update person transform
        UpdatePersonObjectTransform(personObject, ref personData.skeleton, personData.face_rotation);

        // get person object rig
        Rig personRig = personObject.GetComponentInChildren<Rig>();

        if (personRig == null)
        {
            Debug.LogError("Invalid person rig");
            return;
        }

        // update person rig to match bone and mesh rotation (because the room may have a rotation offset)
        personRig.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(sender.Info.Offset.Rotation));

        UpdateRigPositionConstraints(personRig, personData.skeleton, personObject.transform.localPosition);
        UpdateRigRotationConstraints(personRig);

        if (personObject.TryGetComponent<NetworkPersonBehaviour>(out var behaviour))
        {
            // update the animator parameter "Fall" via network variable
            behaviour.SetHasFallen(personData.has_fallen);
        }
    }

    private GameObject CreatePersonIfNotExists(int personID)
    {
        var personName = $"Person {personID}";

        if (peoples.ContainsKey(personName))
            return peoples[personName];

        var personObject = CreatePerson(personID);
        peoples[personName] = personObject;

        var label = personObject.GetComponentInChildren<PlayerNameBillboard>();
        if (label != null) label.SetPlayerName(personName);

        return personObject;
    }

    private GameObject CreatePerson(int personID)
    {
        if (personPrefab == null)
            return null;

        // create person from prefab
        var personObject = GameObject.Instantiate(personPrefab);
        personObject.name = $"Person {personID}";

        // spawn network object
        if (personObject.TryGetComponent<NetworkObject>(out var netObject))
            netObject.Spawn();

        return personObject;
    }

    private static void UpdatePersonObjectTransform(GameObject personObject, ref BoneData[] skeleton, FaceRotation rotation)
    {
        var personTransform = personObject.transform;

        // eval rig position from hip bone
        var rigPosition = personTransform.localPosition;
        var hipBoneData = skeleton.FirstOrDefault(bone => bone.pointID == (int)OpenPoseBone.Hips);
        var headBoneData = skeleton.FirstOrDefault(bone => bone.pointID == (int)OpenPoseBone.Head);

        bool useHeadData = headBoneData.confidence > hipBoneData.confidence;

        if (useHeadData)
        {
            if (headBoneData.confidence > ApplicationConfig.Instance.MinConfidence)
            {
                // update rigPosition only if confidence is over minimum
                rigPosition = new Vector3(headBoneData.x, 0.0f, headBoneData.z);

                // is head data is used, prevent hip bone to move the body because it's the root object
                hipBoneData.confidence = 0f;
            }
        }
        else if (hipBoneData.confidence > ApplicationConfig.Instance.MinConfidence)
        {
            // update rigPosition only if confidence is over minimum
            rigPosition = new Vector3(hipBoneData.x, hipBoneData.y, hipBoneData.z);
        }

        // eval rig rotation from face rotation (angles are evaluated into the direction of the camera so are 180° wrong)
        var rigRotation = Quaternion.Euler(0.0f, rotation.yaw + 180f, 0.0f);

        // update person transform
        personTransform.SetLocalPositionAndRotation(rigPosition, rigRotation);
        personTransform.localScale = Vector3.one;
    }

    private static void UpdateRigPositionConstraints(Rig personRig, BoneData[] skeleton, Vector3 rigPosition)
    {
        for (var childId = 0; childId < personRig.transform.childCount; childId++)
        {
            var boneObject = personRig.transform.GetChild(childId);

            // get constraint gameobject
            if (!boneObject.TryGetComponent<OverrideTransform>(out var constraint))
                continue;

            // query skeleton data
            foreach (var boneData in skeleton)
            {
                var boneId = (OpenPoseBone)boneData.pointID;
                var boneName = Enum.GetName(typeof(OpenPoseBone), boneData.pointID);

                if (boneObject.name == boneName)
                {
                    // apply high pass filter for confidence on bone weight to prevent person floating in scene
                    constraint.weight = boneData.confidence >= ApplicationConfig.Instance.MinConfidence ? boneData.confidence : 0f;
                    constraint.data.sourceObject.localPosition = new Vector3(boneData.x, boneData.y, boneData.z) - rigPosition;

                    if (boneObject.TryGetComponent<NetworkBoneSynchronization>(out var networkBone))
                        networkBone.SetWeightPosition(constraint.weight);

                    break;
                }
            }
        }
    }

    private static void UpdateRigRotationConstraints(Rig personRig)
    {
        for (var childId = 0; childId < personRig.transform.childCount; childId++)
        {
            var boneObject = personRig.transform.GetChild(childId);

            if (!Enum.TryParse<OpenPoseBone>(boneObject.name, true, out var boneId))
                continue;

            // get constraint gameobject
            if (!boneObject.TryGetComponent<OverrideTransform>(out var constraint))
                continue;

            // check for bone to look at
            var boneTargetId = boneId.GetLookAtBoneFrom();

            if (boneTargetId == OpenPoseBone.Invalid)
            {
                // if bone has no rotation constraint ignore rotation
                constraint.data.rotationWeight = 0.0f;
                continue;
            }

            var boneTargetName = Enum.GetName(typeof(OpenPoseBone), boneTargetId);

            // query rig childs transforms
            for (var targetId = 0; targetId < personRig.transform.childCount; targetId++)
            {
                var targetBone = personRig.transform.GetChild(targetId);

                if (targetBone.name == boneTargetName)
                {
                    constraint.data.sourceObject.LookAt(targetBone);

                    switch (boneId)
                    {
                        case OpenPoseBone.LeftShoulder:
                            // upper left side is specular on Y asix
                            constraint.data.sourceObject.Rotate(Vector3.up, 180f);
                            break;

                        case OpenPoseBone.LeftLowerArm:
                            // upper left side is specular on Y asix
                            constraint.data.sourceObject.Rotate(Vector3.up, 180f);
                            break;

                        case OpenPoseBone.RightUpperLeg:
                        case OpenPoseBone.LeftUpperLeg:
                            // lower side is specular on X asix
                            constraint.data.sourceObject.Rotate(Vector3.right, 180f);
                            break;
                    }

                    // update bone rotation constraint weight based on target weight
                    if (targetBone.TryGetComponent<OverrideTransform>(out var targetConstraint))
                    {
                        constraint.data.rotationWeight = targetConstraint.weight;
                    }
                    else // reset bone rotation constraint weight
                    {
                        constraint.data.rotationWeight = 1.0f;
                    }

                    // update network bone rotation weight
                    if (boneObject.TryGetComponent<NetworkBoneSynchronization>(out var networkBone))
                        networkBone.SetWeightRotation(constraint.data.rotationWeight);

                    break;
                }
            }
        }
    }
    #endregion
}