using OpenPose;
using System;
using System.Linq;
using System.Collections;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Utilities.Parser;
using System.IO;

public class MultipleCameraFrameInterpDrawer : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    private GameObject personPrefab;
    [SerializeField]
    private Transform skeletonRoot;
    [SerializeField]
    private float minConfidence = 0.75f;
    [SerializeField]
    private FrameSkeletonsPoints3D currentFrame;
    #endregion

    #region Private Fields
    private Dictionary<string, GameObject> peoples = new();
    #endregion

    #region Unity Lifecycle
    private void OnEnable()
    {
        StartCoroutine(ReadFrames());
    }
    #endregion

    #region Private Lifecycle
    private IEnumerator ReadFrames()
    {
        for (int i = 0; i < 34; i++)
        {
            var filePath = Path.Combine(Application.streamingAssetsPath, $"frame{i}_skeletonsPoints3D.json");
            _ = DrawPeoplesFromFrame(filePath);
            yield return new WaitForSeconds(2f);
        }
    }

    private async Task DrawPeoplesFromFrame(string filePath)
    {
        // read current frame from file path
        currentFrame = await filePath.ParseFromFileAsync<FrameSkeletonsPoints3D>();

        // retrieve person data from frame
        DrawPerson(currentFrame.People.First());
    }

    private void DrawPerson(PeopleData personData)
    {
        if (personData == null)
        {
            Debug.LogError("Cannot find person data");
            return;
        }

        // create person if not exists
        var personObject = CreatePersonIfNotExists(personData.personID);

        if (personObject == null)
        {
            Debug.LogError("Cannot find person object");
            return;
        }

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

    private void UpdateRigRotationConstraints(Rig personRig)
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
                var boneTargetObject = personRig.transform.GetChild(targetId);

                if (boneTargetObject.name == boneTargetName)
                {
                    constraint.data.sourceObject.LookAt(boneTargetObject);

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

                    break;
                }
            }
        }
    }

    private void UpdateRigPositionConstraints(Rig personRig, BoneData[] skeleton, Vector3 rigPosition)
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
                    constraint.weight = boneData.confidence;
                    constraint.data.sourceObject.localPosition = new Vector3(boneData.x, boneData.y, boneData.z) - rigPosition;
                    break;
                }
            }
        }
    }

    private void UpdatePersonObjectTransform(GameObject personObject, BoneData[] skeleton, FaceRotation rotation)
    {
        var personTransform = personObject.transform;

        // eval rig position from hip bone
        var hipBoneData = skeleton.FirstOrDefault(bone => bone.pointID == (int)OpenPoseBone.Hips);
        var rigPosition = personTransform.localPosition;

        // update rigPosition only if confidence is over minimum
        if (hipBoneData != null && hipBoneData.confidence > minConfidence)
        {
            rigPosition = new Vector3(hipBoneData.x, hipBoneData.y, hipBoneData.z);
        }
        else
        {
            // try eval rig position from head bone
            var headBoneData = skeleton.FirstOrDefault(bone => bone.pointID == (int)OpenPoseBone.Head);

            if (headBoneData != null && headBoneData.confidence > minConfidence)
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

    private GameObject CreatePersonIfNotExists(int personID)
    {
        var personName = name = $"Person {personID}";

        if (peoples.ContainsKey(personName))
            return peoples[personName];

        var personObject = CreatePerson(personID);
        peoples[personName] = personObject;
        return personObject;
    }

    private GameObject CreatePerson(int personID)
    {
        if (personPrefab == null) 
            return null;

        // create person from prefab
        var personObject = GameObject.Instantiate(personPrefab);
        personObject.name = $"Person {personID}";

        // set person object parent root
        personObject.transform.parent = this.skeletonRoot;
        return personObject;
    }
    #endregion
}
