using OpenPose;
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Utilities.Parser;

public class SingleCameraFrameDrawer : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    private string frameFileName = "frame_skeletonsPoints3D.json";
    [SerializeField]
    private GameObject personPrefab;
    [SerializeField]
    private Transform skeletonRoot;
    [SerializeField]
    private FrameSkeletonsPoints3D currentFrame;
    [SerializeField]
    private float minConfidence = 0.75f;
    #endregion

    #region Unity Lifecylce
    private async void OnEnable()
    {
        var filePath = Path.Combine(Application.streamingAssetsPath, frameFileName);
        currentFrame = await filePath.ParseFromFileAsync<FrameSkeletonsPoints3D>();

        var personData = currentFrame.People.First();

        if (personData != null && personPrefab != null)
        {
            // create person from prefab
            var personObject = GameObject.Instantiate(personPrefab);
            personObject.name = $"Person {personData.personID}";

            // set person object parent root
            var personTransform = personObject.transform;
            personTransform.parent = this.skeletonRoot;

            var hipBoneData = personData.skeleton.FirstOrDefault(bone => bone.pointID == (int)OpenPoseBone.Hips);
            var rigPosition = new Vector3(hipBoneData.x, hipBoneData.y, hipBoneData.z);

            // eval rig rotation from face rotation
            var angle = Mathf.Rad2Deg * personData.face_rotation.yaw; // to degree
            var rigRotation = Quaternion.Euler(0.0f, angle, 0.0f);

            // update person transform
            personTransform.SetLocalPositionAndRotation(rigPosition, rigRotation);
            personTransform.localScale = Vector3.one;

            if (personObject == null)
            {
                Debug.LogError("Cannot find person object");
                return;
            }

            Rig personRig = personObject.GetComponentInChildren<Rig>();

            if (personRig == null)
            {
                Debug.LogError("Invalid person rig");
                return;
            }

            // update rig position constraints
            for (var childId = 0; childId < personRig.transform.childCount; childId++)
            {
                var boneObject = personRig.transform.GetChild(childId);

                // get constraint gameobject
                if (!boneObject.TryGetComponent<OverrideTransform>(out var constraint))
                    continue;

                // query skeleton data
                foreach (var boneData in personData.skeleton)
                {
                    var boneId = (OpenPoseBone)boneData.pointID;
                    var boneName = boneId.GetBoneName();

                    if (boneObject.name == boneName)
                    {
                        constraint.weight = boneData.confidence;
                        constraint.data.sourceObject.localPosition = new Vector3(boneData.x, boneData.y, boneData.z) - rigPosition;
                        break;
                    }
                }
            }

            // update rig rotation constraints
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

                var boneTargetName = boneTargetId.GetBoneName();

                // query rig childs transforms
                for (var targetId = 0; targetId < personRig.transform.childCount; targetId++)
                {
                    var boneTargetObject = personRig.transform.GetChild(targetId);
                    
                    if(boneTargetObject.name == boneTargetName)
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
    }
    #endregion
}