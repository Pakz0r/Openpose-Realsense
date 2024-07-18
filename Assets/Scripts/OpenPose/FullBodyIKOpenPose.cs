using UnityEngine;
using SA;

[System.Serializable]
public class FullBodyIKOpenPose : FullBodyIK
{
    Vector3 _headBoneLossyScale = Vector3.one;
    bool _isHeadBoneLossyScaleFuzzyIdentity = true;

    Quaternion _headToLeftEyeRotation = Quaternion.identity;
    Quaternion _headToRightEyeRotation = Quaternion.identity;

    Vector3 _leftEyeDefaultPosition = Vector3.zero;
    Vector3 _rightEyeDefaultPosition = Vector3.zero;

    static Vector3 _leftEyeDefaultLocalPosition = new Vector3(-0.042531f + 0.024f, 0.048524f, 0.047682f - 0.02f);
    static Vector3 _rightEyeDefaultLocalPosition = new Vector3(0.042531f - 0.024f, 0.048524f, 0.047682f - 0.02f);

    static readonly float _eyesHorzLimitTheta = Mathf.Sin(40.0f * Mathf.Deg2Rad);
    static readonly float _eyesVertLimitTheta = Mathf.Sin(4.5f * Mathf.Deg2Rad);
    const float _eyesYawRate = 0.796f;
    const float _eyesPitchRate = 0.28f;
    const float _eyesYawOuterRate = 0.096f;
    const float _eyesYawInnerRate = 0.065f;
    const float _eyesMoveXInnerRate = 0.063f * 0.1f;
    const float _eyesMoveXOuterRate = 0.063f * 0.1f;

    public override bool IsHiddenCustomEyes()
    {
        return true;
    }

    public override bool PrepareCustomEyes(ref Quaternion headToLeftEyeRotation, ref Quaternion headToRightEyeRotation)
    {
        Bone headBone = headBones?.head;
        Bone leftEyeBone = headBones?.leftEye;
        Bone rightEyeBone = headBones?.rightEye;

        if (headBone != null && headBone.TransformIsAlive &&
            leftEyeBone != null && leftEyeBone.TransformIsAlive &&
            rightEyeBone != null && rightEyeBone.TransformIsAlive)
        {
            _headToLeftEyeRotation = headToLeftEyeRotation;
            _headToRightEyeRotation = headToRightEyeRotation;

            SAFBIKMatMultVec(out Vector3 leftPos, ref internalValues.defaultRootBasis, ref _leftEyeDefaultLocalPosition);
            SAFBIKMatMultVec(out Vector3 rightPos, ref internalValues.defaultRootBasis, ref _rightEyeDefaultLocalPosition);

            _headBoneLossyScale = headBone.transform.lossyScale;
            _isHeadBoneLossyScaleFuzzyIdentity = IsFuzzy(_headBoneLossyScale, Vector3.one);

            if (!_isHeadBoneLossyScaleFuzzyIdentity)
            {
                leftPos = Scale(ref leftPos, ref _headBoneLossyScale);
                rightPos = Scale(ref rightPos, ref _headBoneLossyScale);
            }

            _leftEyeDefaultPosition = headBone._defaultPosition + leftPos;
            _rightEyeDefaultPosition = headBone._defaultPosition + rightPos;
        }

        return true;
    }

    public override void ResetCustomEyes()
    {
        Bone neckBone = headBones?.neck;
        Bone headBone = headBones?.head;
        Bone leftEyeBone = headBones?.leftEye;
        Bone rightEyeBone = headBones?.rightEye;

        if (neckBone != null && neckBone.TransformIsAlive &&
            headBone != null && headBone.TransformIsAlive &&
            leftEyeBone != null && leftEyeBone.TransformIsAlive &&
            rightEyeBone != null && rightEyeBone.TransformIsAlive)
        {

            Quaternion neckWorldRotation = neckBone.WorldRotation;
            Vector3 neckWorldPosition = neckBone.WorldPosition;
            SAFBIKMatSetRotMultInv1(out Matrix3x3 neckBasis, ref neckWorldRotation, ref neckBone._defaultRotation);
            SAFBIKMatMultVecPreSubAdd(out Vector3 headWorldPosition, ref neckBasis, ref headBone._defaultPosition, ref neckBone._defaultPosition, ref neckWorldPosition);

            Quaternion headWorldRotation = headBone.WorldRotation;
            SAFBIKMatSetRotMultInv1(out Matrix3x3 headBasis, ref headWorldRotation, ref headBone._defaultRotation);


            SAFBIKMatMultVecPreSubAdd(out Vector3 worldPotision, ref headBasis, ref leftEyeBone._defaultPosition, ref headBone._defaultPosition, ref headWorldPosition);
            leftEyeBone.WorldPosition = worldPotision;
            SAFBIKQuatMult(out Quaternion worldRotation, ref headWorldRotation, ref _headToLeftEyeRotation);
            leftEyeBone.WorldRotation = worldRotation;

            SAFBIKMatMultVecPreSubAdd(out worldPotision, ref headBasis, ref rightEyeBone._defaultPosition, ref headBone._defaultPosition, ref headWorldPosition);
            rightEyeBone.WorldPosition = worldPotision;
            SAFBIKQuatMult(out worldRotation, ref headWorldRotation, ref _headToRightEyeRotation);
            rightEyeBone.WorldRotation = worldRotation;
        }
    }

    public override void SolveCustomEyes(ref Matrix3x3 neckBasis, ref Matrix3x3 headBasis, ref Matrix3x3 headBaseBasis)
    {
        Bone neckBone = headBones?.neck;
        Bone headBone = headBones?.head;
        Bone leftEyeBone = headBones?.leftEye;
        Bone rightEyeBone = headBones?.rightEye;
        Effector eyesEffector = headEffectors?.eyes;

        if (neckBone != null && neckBone.TransformIsAlive &&
            headBone != null && headBone.TransformIsAlive &&
            leftEyeBone != null && leftEyeBone.TransformIsAlive &&
            rightEyeBone != null && rightEyeBone.TransformIsAlive &&
            eyesEffector != null)
        {

            Vector3 neckBoneWorldPosition = neckBone.WorldPosition;
            SAFBIKMatMultVecPreSubAdd(out Vector3 headWorldPosition, ref neckBasis, ref headBone._defaultPosition, ref neckBone._defaultPosition, ref neckBoneWorldPosition);
            SAFBIKMatMultVecPreSubAdd(out Vector3 eyesPosition, ref headBasis, ref eyesEffector._defaultPosition, ref headBone._defaultPosition, ref headWorldPosition);

            Vector3 eyesDir = eyesEffector.WorldPosition - eyesPosition;

            Matrix3x3 leftEyeBaseBasis = headBaseBasis;
            Matrix3x3 rightEyeBaseBasis = headBaseBasis;

            SAFBIKMatMultVecInv(out eyesDir, ref headBaseBasis, ref eyesDir);

            if (!SAFBIKVecNormalize(ref eyesDir))
            {
                eyesDir = new Vector3(0.0f, 0.0f, 1.0f);
            }

            if (eyesEffector.positionWeight < 1.0f - IKEpsilon)
            {
                Vector3 tempDir = Vector3.Lerp(new Vector3(0.0f, 0.0f, 1.0f), eyesDir, eyesEffector.positionWeight);
                if (SAFBIKVecNormalize(ref tempDir))
                {
                    eyesDir = tempDir;
                }
            }

            LimitSquareXY(ref eyesDir,
                _eyesHorzLimitTheta,
                _eyesHorzLimitTheta,
                _eyesVertLimitTheta,
                _eyesVertLimitTheta);

            float moveX = eyesDir.x * _eyesYawRate;
            if (moveX < -_eyesHorzLimitTheta)
            {
                moveX = -_eyesHorzLimitTheta;
            }
            else if (moveX > _eyesHorzLimitTheta)
            {
                moveX = _eyesHorzLimitTheta;
            }

            eyesDir.x *= _eyesYawRate;
            eyesDir.y *= _eyesPitchRate;
            Vector3 leftEyeDir = eyesDir;
            Vector3 rightEyeDir = eyesDir;

            if (eyesDir.x >= 0.0f)
            {
                leftEyeDir.x *= _eyesYawInnerRate;
                rightEyeDir.x *= _eyesYawOuterRate;
            }
            else
            {
                leftEyeDir.x *= _eyesYawOuterRate;
                rightEyeDir.x *= _eyesYawInnerRate;
            }

            SAFBIKVecNormalize2(ref leftEyeDir, ref rightEyeDir);

            SAFBIKMatMultVec(out leftEyeDir, ref headBaseBasis, ref leftEyeDir);
            SAFBIKMatMultVec(out rightEyeDir, ref headBaseBasis, ref rightEyeDir);

            float leftXRate = (moveX >= 0.0f) ? _eyesMoveXInnerRate : _eyesMoveXOuterRate;
            float rightXRate = (moveX >= 0.0f) ? _eyesMoveXOuterRate : _eyesMoveXInnerRate;

            SAFBIKComputeBasisLockZ(out leftEyeBaseBasis, ref headBasis.column0, ref headBasis.column1, ref leftEyeDir);
            SAFBIKComputeBasisLockZ(out rightEyeBaseBasis, ref headBasis.column0, ref headBasis.column1, ref rightEyeDir);

            Vector3 leftEyeWorldPosition = headBaseBasis.column0 * (leftXRate * moveX);
            Vector3 rightEyeWorldPosition = headBaseBasis.column0 * (rightXRate * moveX);

            if (!_isHeadBoneLossyScaleFuzzyIdentity)
            {
                leftEyeWorldPosition = Scale(ref leftEyeWorldPosition, ref _headBoneLossyScale);
                rightEyeWorldPosition = Scale(ref rightEyeWorldPosition, ref _headBoneLossyScale);
            }

            SAFBIKMatMultVecPreSubAdd(out Vector3 tempVec, ref headBasis, ref _leftEyeDefaultPosition, ref headBone._defaultPosition, ref headWorldPosition);
            leftEyeWorldPosition += tempVec;
            SAFBIKMatMultVecPreSubAdd(out tempVec, ref headBasis, ref _rightEyeDefaultPosition, ref headBone._defaultPosition, ref headWorldPosition);
            rightEyeWorldPosition += tempVec;

            SAFBIKMatMult(out Matrix3x3 leftEyeBasis, ref leftEyeBaseBasis, ref internalValues.defaultRootBasisInv);
            SAFBIKMatMult(out Matrix3x3 rightEyeBasis, ref rightEyeBaseBasis, ref internalValues.defaultRootBasisInv);


            SAFBIKMatMultVecPreSubAdd(out Vector3 worldPosition, ref leftEyeBasis, ref leftEyeBone._defaultPosition, ref _leftEyeDefaultPosition, ref leftEyeWorldPosition);
            leftEyeBone.WorldPosition = worldPosition;
            SAFBIKMatMultGetRot(out Quaternion worldRotation, ref leftEyeBaseBasis, ref leftEyeBone._baseToWorldBasis);
            leftEyeBone.WorldRotation = worldRotation;

            SAFBIKMatMultVecPreSubAdd(out worldPosition, ref rightEyeBasis, ref rightEyeBone._defaultPosition, ref _rightEyeDefaultPosition, ref rightEyeWorldPosition);
            rightEyeBone.WorldPosition = worldPosition;
            SAFBIKMatMultGetRot(out worldRotation, ref rightEyeBaseBasis, ref rightEyeBone._baseToWorldBasis);
            rightEyeBone.WorldRotation = worldRotation;
        }
    }
}