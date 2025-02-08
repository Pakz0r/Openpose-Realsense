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
    private void DrawPerson(SensorWatcher sender, PersonData personData)
    {
        if (personData == null)
        {
            Debug.LogError("Cannot find person data");
            return;
        }

        // eval frame confidence
        var avgConfidence = personData.skeleton.Average((bone) => bone.confidence);
        Debug.Log($"Person {personData.personID} new frame average confidence: {avgConfidence}");

        if(avgConfidence < ApplicationConfig.Instance.MinConfidence)
        {
            if (ExistsPerson(personData.personID))
            {
                DestroyPersonIfExists(personData.personID);
                Debug.Log($"Person {personData.personID} has been removed from the scene");
            }
            else
            {
                Debug.Log($"Person {personData.personID} spawn abort");
            }

            return;
        }

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

        UpdateRigPositionConstraints(personRig.transform, personData.skeleton, personObject.transform.localPosition);
        UpdateRigRotationConstraints(personRig.transform);

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

    private bool ExistsPerson(int personID)
    {
        var personName = $"Person {personID}";
        return peoples.ContainsKey(personName);
    }

    private bool DestroyPersonIfExists(int personID)
    {
        var personName = $"Person {personID}";

        if (!peoples.ContainsKey(personName))
            return false;

        Destroy(peoples[personName]);
        return peoples.Remove(personName);
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

                // if head data is used, prevent hip bone to move the body because it's the root object
                hipBoneData.x = rigPosition.x; // align hip to rig position
                hipBoneData.y = rigPosition.y; // align hip to rig position
                hipBoneData.z = rigPosition.z; // align hip to rig position
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

    private static void UpdateRigPositionConstraints(Transform parentRig, BoneData[] skeleton, Vector3 rigPosition)
    {
        foreach (Transform boneObject in parentRig)
        {
            if (!Enum.TryParse<OpenPoseBone>(boneObject.name, out var boneId))
                continue;

            // query skeleton data
            var boneData = skeleton.FirstOrDefault(bone => bone.pointID == (int)boneId);

            // apply bone position
            boneObject.localPosition = new Vector3(boneData.x, boneData.y, boneData.z) - rigPosition;

            if (boneObject.TryGetComponent<NetworkBoneSynchronization>(out var networkBone))
            {
                // apply high pass filter for confidence on bone weight to prevent person floating in scene
                var weight = boneData.confidence >= ApplicationConfig.Instance.MinConfidence ? boneData.confidence : 0f;

                // set position constraint weight
                networkBone.SetWeightPosition(weight);
            }
        }
    }

    private static void UpdateRigRotationConstraints(Transform parentRig)
    {
        foreach (Transform boneObject in parentRig)
        {
            if (!Enum.TryParse<OpenPoseBone>(boneObject.name, true, out var boneId))
                continue;

            var weight = 0f; // by default ignore rotation

            // check for bone to look at
            var boneTargetId = boneId.GetLookAtBoneFrom();

            if (boneTargetId != OpenPoseBone.Invalid)
            {
                var boneTargetName = Enum.GetName(typeof(OpenPoseBone), boneTargetId);

                // query rig childs transforms to search for target bone
                foreach (Transform targetBone in parentRig)
                {
                    if (targetBone.name != boneTargetName)
                        continue;

                    // update bone rotation constraint weight based on target weight
                    if (targetBone.TryGetComponent<OverrideTransform>(out var targetConstraint))
                        weight = targetConstraint.weight;

                    break;
                }
            }

            // update network bone rotation weight
            if (boneObject.TryGetComponent<NetworkBoneSynchronization>(out var networkBone))
                networkBone.SetWeightRotation(weight);
        }
    }
    #endregion
}