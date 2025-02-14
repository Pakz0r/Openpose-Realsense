using System.Collections.Generic;
using System;

public enum OpenPoseBone {
    Head = 0,
    UpperChest = 1, // more torso ?
    RightShoulder = 2,
    RightLowerArm = 3,
    RightHand = 4,
    LeftShoulder = 5,
    LeftLowerArm = 6,
    LeftHand = 7,
    Hips = 8,
    RightUpperLeg = 9,
    RightLowerLeg = 10,
    RightFoot = 11,
    LeftUpperLeg = 12,
    LeftLowerLeg = 13,
    LeftFoot = 14,
    RightEye = 15,
    LeftEye = 16,
    Unk0 = 17, // Right side nearby ear
    Unk1 = 18, // Left side nearby ear
    Unk2 = 19, // Left side nearby foot
    Unk3 = 20, // Left side nearby foot
    Unk4 = 21, // Left side nearby foot
    Unk5 = 22, // Right side nearby foot
    Unk6 = 23, // Right side nearby foot
    Unk7 = 24, // Right side nearby foot
    Invalid = -1,
}

public enum OpenPoseBodyKeypoint
{
    Nose = 0, 
    Neck = 1, 
    RShoulder = 2, 
    RElbow = 3, 
    RWrist = 4,
    LShoulder = 5, 
    LElbow = 6, 
    LWrist = 7, 
    MidHip = 8, 
    RHip = 9,
    RKnee = 10, 
    RAnkle = 11, 
    LHip = 12, 
    LKnee = 13, 
    LAnkle = 14,
    REye = 15, 
    LEye = 16, 
    REar = 17, 
    LEar = 18, 
    LBigToe = 19,
    LSmallToe = 20, 
    LHeel = 21, 
    RBigToe = 22, 
    RSmallToe = 23, 
    RHeel = 24,
    Background = 25
}

public static class OpenPoseBoneUtilities
{
    private static readonly Type openPoseBoneType = typeof(OpenPoseBone);

    private static readonly Dictionary<OpenPoseBone, OpenPoseBone> lookAtDict = new()
    {
        [OpenPoseBone.RightShoulder] = OpenPoseBone.RightLowerArm,
        [OpenPoseBone.RightLowerArm] = OpenPoseBone.RightHand,
        [OpenPoseBone.LeftShoulder] = OpenPoseBone.LeftLowerArm,
        [OpenPoseBone.LeftLowerArm] = OpenPoseBone.LeftHand,
        [OpenPoseBone.RightUpperLeg] = OpenPoseBone.RightLowerLeg,
        [OpenPoseBone.RightLowerLeg] = OpenPoseBone.RightFoot,
        [OpenPoseBone.LeftUpperLeg] = OpenPoseBone.LeftLowerLeg,
        [OpenPoseBone.LeftLowerLeg] = OpenPoseBone.LeftFoot,
    };

    private static readonly Dictionary<OpenPoseBone, OpenPoseBone> followDirectionOfDict = new()
    {
        [OpenPoseBone.LeftHand] = OpenPoseBone.LeftLowerArm,
        [OpenPoseBone.RightHand] = OpenPoseBone.RightLowerArm,
    };

    public static OpenPoseBone GetLookAtBoneFrom(this OpenPoseBone bone)
    {
        return lookAtDict.GetValueOrDefault(bone, OpenPoseBone.Invalid);
    }

    public static OpenPoseBone GetBoneToFollow(this OpenPoseBone bone)
    {
        return followDirectionOfDict.GetValueOrDefault(bone, OpenPoseBone.Invalid);
    }

    public static string GetBoneName(this OpenPoseBone bone)
    {
        return Enum.GetName(openPoseBoneType, bone);
    }
}