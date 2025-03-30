namespace OpenPose
{
    using System;

    [Serializable]
    public class FrameSkeletonsPoints3D
    {
        public int ID_Frame;
        public string thingId;
        public PersonData[] People;
        public bool Has_Fallen;
    }

    [Serializable]
    public class PersonData
    {
        public int personID;
        public bool has_fallen;
        public FaceRotation face_rotation;
        public BoneData[] skeleton;
    }

    [Serializable]
    public class FaceRotation
    {
        public float pitch;
        public float roll;
        public float yaw;
    }

    [Serializable]
    public class BoneData
    {
        public int pointID;
        public float confidence;
        public float x;
        public float y;
        public float z;
    }
}