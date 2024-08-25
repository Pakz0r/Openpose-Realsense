using Utilities.Parser;
using UnityEngine.Animations.Rigging;
using UnityEngine;
using System.Linq;
using System.IO;
using System;
using OpenPose;

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
    private async void Awake()
    {
        var filePath = Path.Combine(Application.streamingAssetsPath, frameFileName);
        currentFrame = await filePath.ParseFromFileAsync<FrameSkeletonsPoints3D>();

        var personData = currentFrame.People.First();

        if (personData != null && personPrefab != null)
        {
            // create person from prefab
            var personObject = GameObject.Instantiate(personPrefab);
            personObject.name = $"Person {personData.personID}";

            var personTransform = personObject.transform;
            personTransform.parent = this.skeletonRoot;
            personTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
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
                    var boneName = Enum.GetName(typeof(OpenPoseBone), boneData.pointID);

                    if (boneObject.name == boneName)
                    {
                        constraint.weight = boneData.confidence;
                        constraint.data.sourceObject.localPosition = new Vector3(boneData.x, boneData.y, boneData.z);

                        switch (boneId)
                        {
                            case OpenPoseBone.Hips:
                                var angle = Mathf.Rad2Deg * personData.face_rotation.yaw; // to degree
                                constraint.data.sourceObject.localRotation = Quaternion.Euler(0.0f, angle, 0.0f);
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