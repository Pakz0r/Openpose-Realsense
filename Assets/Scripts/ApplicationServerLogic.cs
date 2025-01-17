using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine.Animations.Rigging;
using UnityEngine;
using OpenPose;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

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
            var config = ApplicationLogic.Config;

            if (config.ServerPort > 0)
            {
                networkTransport.ConnectionData.Port = config.ServerPort;
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
        UpdatePersonObjectTransform(personObject, personData.skeleton, personData.face_rotation);

        // get person object rig
        Rig personRig = personObject.GetComponentInChildren<Rig>();

        if (personRig == null)
        {
            Debug.LogError("Invalid person rig");
            return;
        }

        UpdateRigPositionConstraints(personRig, personData.skeleton, personObject.transform.localPosition);
        UpdateRigRotationConstraints(personRig);
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

    private static void UpdatePersonObjectTransform(GameObject personObject, BoneData[] skeleton, FaceRotation rotation)
    {
        var personTransform = personObject.transform;

        // eval rig position from hip bone
        var hipBoneData = skeleton.FirstOrDefault(bone => bone.pointID == (int)OpenPoseBone.Hips);
        var rigPosition = personTransform.localPosition;

        // update rigPosition only if confidence is over minimum
        if (hipBoneData != null && hipBoneData.confidence > ApplicationLogic.Config.MinConfidence)
        {
            rigPosition = new Vector3(hipBoneData.x, hipBoneData.y, hipBoneData.z);
        }
        else
        {
            // try eval rig position from head bone
            var headBoneData = skeleton.FirstOrDefault(bone => bone.pointID == (int)OpenPoseBone.Head);

            if (headBoneData != null && headBoneData.confidence > ApplicationLogic.Config.MinConfidence)
            {
                rigPosition = new Vector3(headBoneData.x, 0.0f, headBoneData.z);
            }
        }

        // eval rig rotation from face rotation
        var angle = Mathf.Rad2Deg * rotation.yaw; // to degree
        var rigRotation = Quaternion.Euler(0.0f, angle, 0.0f);

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
                    constraint.weight = EvalWeight(constraint.weight, boneData.confidence);
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

            // check for bone to look at
            var boneTargetId = boneId.GetLookAtBoneFrom();

            if (boneTargetId == OpenPoseBone.Invalid)
                continue;

            // get constraint gameobject
            if (!boneObject.TryGetComponent<OverrideTransform>(out var constraint))
                continue;

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

    // Approccio Bayesiano (aggiornamento con prior)
    private static float EvalWeight(float currentWeight, float confidence)
    {
        //Un approccio elegante per aggiornare il peso potrebbe essere quello di usare un metodo Bayesiano.
        //Considera il peso al tempo (t-1) come un prior(una stima iniziale) e aggiorna il peso in base alla nuova confidence.
        //Questo approccio aggiorna il peso combinando l'informazione precedente (prior) e quella nuova (likelihood) in maniera ottimale.
        //Se la confidence corrente � alta, il peso si aggiorna verso l'alto; se � bassa, il peso si riduce.
        //Al passo t0 (sul prefab) il peso � 0.5.
        //return (currentWeight * confidence) / (currentWeight * confidence + (1 - currentWeight) * (1 - confidence));

        return confidence;
    }
    #endregion
}
