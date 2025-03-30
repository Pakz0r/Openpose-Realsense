using Unity.Netcode.Transports.UTP;
using Unity.Netcode;
using UnityEngine.Animations.Rigging;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using UniRx;
using OpenPose;

public class ApplicationServerLogic : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    private GameObject personPrefab;
    #endregion

    #region Private Fields
    private NetworkManager networkManager;
    private Dictionary<string, GameObject> peoples = new();
    private PersonPoseGraphOptimizator poseGraphOptimizator;
    private NetworkRoomBehaviour networkRoom;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        poseGraphOptimizator = new PersonPoseGraphOptimizator(personPrefab);
        networkRoom = GameObject.FindAnyObjectByType<NetworkRoomBehaviour>();
    }

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
        SensorWatcher.FrameReaded.AddListener(DestroyNonUpdatedPerson);
    }

    private void OnDisable()
    {
        SensorWatcher.PersonUpdated.RemoveAllListeners();
        SensorWatcher.FrameReaded.RemoveAllListeners();
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

        if (avgConfidence < ApplicationConfig.Instance.MinConfidence)
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
        var personObject = CreatePersonIfNotExists(personData.personID, sender.transform);

        if (personObject == null)
        {
            Debug.LogError("Cannot find person object");
            return;
        }

        // update person transform
        UpdatePersonObjectTransform(personObject, personData.skeleton, personData.face_rotation);

        // get person object rig
        Rig personRig = personObject.GetComponentInChildren<Rig>();

        if (personRig == null)
        {
            Debug.LogError("Invalid person rig");
            return;
        }

        // optimize the pose
        poseGraphOptimizator.Optimize(ref personData.skeleton, personRig.transform.position);

        // update person rig to match bone and mesh rotation (because the room may have a rotation offset)
        personRig.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(sender.Info.Offset.Rotation));

        UpdateRigPositionConstraints(personRig.transform, personData.skeleton);
        UpdateRigRotationConstraints(personRig.transform);

        if (personObject.TryGetComponent<NetworkPersonBehaviour>(out var behaviour))
        {
            // update the animator parameter "Fall" via network variable
            behaviour.SetHasFallen(personData.has_fallen);
        }
    }

    private GameObject CreatePersonIfNotExists(int personID, Transform parent)
    {
        var personName = $"Person {personID}";

        if (peoples.ContainsKey(personName))
        {
            // update person transform parent
            peoples[personName].transform.SetParent(parent);
            return peoples[personName];
        }

        var personObject = CreatePerson(personID, parent);
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

    private GameObject CreatePerson(int personID, Transform parent)
    {
        if (personPrefab == null)
            return null;

        // create person from prefab
        var personObject = GameObject.Instantiate(personPrefab, parent);
        personObject.name = $"Person {personID}";

        // spawn network object
        if (personObject.TryGetComponent<NetworkObject>(out var netObject))
            netObject.Spawn();

        return personObject;
    }

    private static void UpdatePersonObjectTransform(GameObject personObject, BoneData[] skeleton, FaceRotation rotation)
    {
        var personTransform = personObject.transform;

        // eval rig position from hip bone
        var rigPosition = personTransform.localPosition;
        var hipBoneData = skeleton.FirstOrDefault(bone => bone.pointID == (int)OpenPoseBone.Hips);
        var headBoneData = skeleton.FirstOrDefault(bone => bone.pointID == (int)OpenPoseBone.Head);

        bool useHeadData = headBoneData.confidence > hipBoneData.confidence;

        if (useHeadData)
        {
            if (headBoneData.confidence >= ApplicationConfig.Instance.MinConfidence)
            {
                // update rigPosition only if confidence is over minimum
                rigPosition = new Vector3(headBoneData.x, 1f, headBoneData.z);
            }
        }
        else if (hipBoneData.confidence >= ApplicationConfig.Instance.MinConfidence)
        {
            // update rigPosition only if confidence is over minimum
            rigPosition = new Vector3(hipBoneData.x, 1f, hipBoneData.z);
        }

        // eval rig rotation from face rotation (angles are evaluated into the direction of the camera so are 180° wrong)
        var rigRotation = Quaternion.Euler(0.0f, 180f - rotation.yaw, 0.0f);

        // update person transform
        personTransform.position = rigPosition;
        personTransform.localRotation = rigRotation;
        personTransform.localScale = Vector3.one;
    }

    private static void UpdateRigPositionConstraints(Transform parentRig, BoneData[] skeleton)
    {
        foreach (Transform boneObject in parentRig)
        {
            if (!Enum.TryParse<OpenPoseBone>(boneObject.name, out var boneId))
                continue;

            // query skeleton data
            var boneData = skeleton.FirstOrDefault(bone => bone.pointID == (int)boneId);

            // apply bone position
            boneObject.position = new Vector3(boneData.x, boneData.y, boneData.z);

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
                var boneTargetName = boneTargetId.GetBoneName();

                // query rig childs transforms to search for target bone
                foreach (Transform bone in parentRig)
                {
                    if (bone.name != boneTargetName)
                        continue;

                    // update bone rotation constraint weight based on target weight
                    if (bone.TryGetComponent<OverrideTransform>(out var constraint))
                    {
                        // use distance to prevent bone compenetration caused by wrong angle representation
                        weight = Vector3.Distance(boneObject.position, bone.position) < 0.1 ? 0f : constraint.weight;
                    }

                    break;
                }
            }

            // check for bone to follow up
            var boneFollowedId = boneId.GetBoneToFollow();

            if (boneFollowedId != OpenPoseBone.Invalid)
            {
                var boneFollowedName = boneFollowedId.GetBoneName();

                // query rig childs transforms to search for followed bone
                foreach (Transform bone in parentRig)
                {
                    if (bone.name != boneFollowedName)
                        continue;

                    // update bone rotation constraint weight based on followed weight
                    if (bone.TryGetComponent<OverrideTransform>(out var constraint))
                        weight = constraint.weight;
                }
            }

            // update network bone rotation weight
            if (boneObject.TryGetComponent<NetworkBoneSynchronization>(out var networkBone))
                networkBone.SetWeightRotation(weight);
        }
    }

    private void DestroyNonUpdatedPerson(SensorWatcher sender, FrameSkeletonsPoints3D frame)
    {
        foreach (var name in peoples.Keys.ToList())
        {
            var person = frame.People.FirstOrDefault((person) => name == $"Person {person.personID}");

            if (person == null)
            {
                Debug.Log($"{name} has been removed from the scene");
                Destroy(peoples[name]);
                peoples.Remove(name);
            }
        }

        if (networkRoom != null)
        {
            networkRoom.SetHasAnyFallen(frame.Has_Fallen);
        }
    }
    #endregion
}