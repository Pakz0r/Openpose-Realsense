using Utilities.Parser;
using UnityEngine;
using System.Linq;
using System.IO;
using System;
using OpenPose;

public class TestDepthCamera : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    private string frameFileName = "frame_skeletonsPoints3D.json";
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

        if (personData != null)
        {
            var personObject = new GameObject();
            personObject.name = $"Person {personData.personID}";
            personObject.transform.parent = this.skeletonRoot;
            personObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            personObject.transform.localScale = Vector3.one;

            foreach (var boneData in personData.skeleton)
            {
                if (boneData.confidence > minConfidence)
                {
                    var boneObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    boneObject.name = Enum.GetName(typeof(OpenPoseBone), boneData.pointID); //$"Bone {boneData.pointID}";
                    boneObject.transform.parent = personObject.transform;
                    boneObject.transform.SetLocalPositionAndRotation(new Vector3(boneData.x, boneData.y, boneData.z), Quaternion.identity);
                    boneObject.transform.localScale = Vector3.one * 0.05f;
                }
            }
        }
    }
    #endregion
}
