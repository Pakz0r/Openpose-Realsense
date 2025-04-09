using OpenPose;
using System;
using System.Collections.Generic;
using UnityEngine;

public class PersonPoseGraphOptimizator
{
    private readonly static (OpenPoseBone, OpenPoseBone)[] openposeConstraints =
    {
        // spine
        (OpenPoseBone.Head, OpenPoseBone.UpperChest),
        (OpenPoseBone.UpperChest, OpenPoseBone.Hips),
        // right arm
        (OpenPoseBone.UpperChest, OpenPoseBone.RightShoulder),
        (OpenPoseBone.RightShoulder, OpenPoseBone.RightLowerArm),
        (OpenPoseBone.RightLowerArm, OpenPoseBone.RightHand),
        // left arm
        (OpenPoseBone.UpperChest, OpenPoseBone.LeftShoulder),
        (OpenPoseBone.LeftShoulder, OpenPoseBone.LeftLowerArm),
        (OpenPoseBone.LeftLowerArm, OpenPoseBone.LeftHand),
        // upper body simmetry
        (OpenPoseBone.RightShoulder, OpenPoseBone.LeftShoulder),
        (OpenPoseBone.RightShoulder, OpenPoseBone.RightUpperLeg),
        (OpenPoseBone.LeftShoulder, OpenPoseBone.LeftLowerLeg),
        // neck
        (OpenPoseBone.RightShoulder, OpenPoseBone.Head),
        (OpenPoseBone.LeftShoulder, OpenPoseBone.Head),
        // right leg
        (OpenPoseBone.Hips, OpenPoseBone.RightUpperLeg),
        (OpenPoseBone.RightUpperLeg, OpenPoseBone.RightLowerLeg),
        (OpenPoseBone.RightLowerLeg, OpenPoseBone.RightFoot),
        // left leg
        (OpenPoseBone.Hips, OpenPoseBone.LeftUpperLeg),
        (OpenPoseBone.LeftUpperLeg, OpenPoseBone.LeftLowerLeg),
        (OpenPoseBone.LeftLowerLeg, OpenPoseBone.LeftFoot),
        // lower body simmetry
        (OpenPoseBone.UpperChest, OpenPoseBone.RightUpperLeg),
        (OpenPoseBone.UpperChest, OpenPoseBone.LeftUpperLeg),
        (OpenPoseBone.RightUpperLeg, OpenPoseBone.LeftUpperLeg),
        (OpenPoseBone.RightLowerLeg, OpenPoseBone.LeftLowerLeg),
        (OpenPoseBone.RightFoot, OpenPoseBone.LeftFoot),
    };

    public class JointConstraint
    {
        public int FromIndex { get; set; }
        public int ToIndex { get; set; }
        public float Distance { get; set; }

        public JointConstraint(int fromIndex, int toIndex, float distance)
        {
            FromIndex = fromIndex;
            ToIndex = toIndex;
            Distance = distance;
        }
    }

    private const int limbSizeArray = 15;
    private readonly float defaultUpperBodyConfidence = 0.75f;
    private readonly float defaultLowerBodyConfidence = 0.75f;
    private readonly float confidenceTreshold = 0.2f;
    private readonly List<JointConstraint> jointConstraints = new();

    public PersonPoseGraphOptimizator(GameObject personPrefab, float threshold = 0.2f,
        float upperConfidence = 0.75f, float lowerConfidence = 0.75f)
    {
        if (personPrefab == null)
            throw new Exception("Invalid person prefab");

        if (!personPrefab.TryGetComponent<Animator>(out var animator))
            throw new Exception("Animator object not found");

        // Imposta le variabili per l'ottimizzatore
        this.confidenceTreshold = threshold;
        this.defaultUpperBodyConfidence = upperConfidence;
        this.defaultLowerBodyConfidence = lowerConfidence;

        // Raccolta delle informazioni iniziali sulla posizione del digital twin
        Transform[] bodyParts = new Transform[limbSizeArray];

        bodyParts[0] = animator.GetBoneTransform(HumanBodyBones.Head);
        bodyParts[1] = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        bodyParts[2] = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        bodyParts[3] = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        bodyParts[4] = animator.GetBoneTransform(HumanBodyBones.RightHand);
        bodyParts[5] = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        bodyParts[6] = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        bodyParts[7] = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        bodyParts[8] = animator.GetBoneTransform(HumanBodyBones.Hips);
        bodyParts[9] = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        bodyParts[10] = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        bodyParts[11] = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        bodyParts[12] = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        bodyParts[13] = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        bodyParts[14] = animator.GetBoneTransform(HumanBodyBones.LeftFoot);

        // Creazione dei vincoli in base alla posa iniziale del digital twin (T-Pose)
        foreach (var joints in openposeConstraints)
        {
            var distance = Vector3.Distance(bodyParts[(int)joints.Item1].position, bodyParts[(int)joints.Item2].position);
            var constraint = new JointConstraint((int)joints.Item1, (int)joints.Item2, distance);
            jointConstraints.Add(constraint);
        }
    }

    public void Optimize(ref BoneData[] skeleton, Vector3 initPosition, int maxIterations = 150)
    {
        var joints = new Vector3[limbSizeArray];
        var confidences = new float[limbSizeArray];

        for (int i = 0; i < limbSizeArray; i++)
        {
            joints[i] = new Vector3(skeleton[i].x, skeleton[i].y, skeleton[i].z);
            confidences[i] = Mathf.Clamp(skeleton[i].confidence, 0.1f, 1f); // Evita pesi nulli

            if (confidences[i] < confidenceTreshold)
            {
                joints[i].x = initPosition.x;
                joints[i].y = 0f;
                joints[i].z = initPosition.z;
            }
        }

        if (maxIterations == -1) maxIterations = int.MaxValue;

        int iter = 0;
        float totalResidual = 0f;

        for (; iter < maxIterations; iter++)
        {
            totalResidual = ComputeAndCorrectResiduals(ref joints, confidences);

            if (totalResidual < 0.001f) // Soglia di convergenza
                break;
        }

        Debug.Log($"{iter} {totalResidual}");

        // Visualizziamo i risultati
        for (int i = 0; i < limbSizeArray; i++)
        {
            var bone = skeleton[i];
            bone.x = joints[i].x;
            bone.y = joints[i].y;
            bone.z = joints[i].z;

            // Impongo la nuova confidence di default per le ossa
            bone.confidence = (bone.pointID < (int)OpenPoseBone.Hips) ? defaultUpperBodyConfidence : defaultLowerBodyConfidence;
        }
    }

    private float ComputeAndCorrectResiduals(ref Vector3[] joints, float[] confidences)
    {
        float totalResidual = 0f;

        for (int i = 0; i < jointConstraints.Count; i++)
        {
            int fromIndex = jointConstraints[i].FromIndex;
            int toIndex = jointConstraints[i].ToIndex;
            float targetDistance = jointConstraints[i].Distance;

            Vector3 fromPose = joints[fromIndex];
            Vector3 toPose = joints[toIndex];

            float currentDistance = Vector3.Distance(fromPose, toPose);
            float error = currentDistance - targetDistance;

            totalResidual += (error * error);

            // Correzione solo se l'errore è significativo
            if (Mathf.Abs(error) > 0.001f)
            {
                Vector3 correctionDir = (fromPose - toPose).normalized;
                Vector3 correction = error * correctionDir;

                joints[fromIndex] -= correction * (1 - confidences[fromIndex]);
                joints[toIndex] += correction * (1 - confidences[toIndex]);
            }
        }

        // Correggo l'allineamento della schiena
        int headIndex = (int)OpenPoseBone.Head;
        int chestIndex = (int)OpenPoseBone.UpperChest;
        int pelvisIndex = (int)OpenPoseBone.Hips;

        Vector3 head = joints[headIndex];
        Vector3 chest = joints[chestIndex];
        Vector3 pelvis = joints[pelvisIndex];

        Vector3 neckDir = (head - chest).normalized;
        float alignmentError = Vector3.Dot(neckDir, Vector3.up);

        // Se l'errore di allineamento è alto, correggiamo la schiena (0.98 ≈ 10°)
        if (alignmentError < 0.98f)
        {
            Vector3 correction = (Vector3.up - neckDir) * 0.1f;
            joints[headIndex] += correction * (1 - confidences[headIndex]);
            joints[chestIndex] += correction * (1 - confidences[chestIndex]);
            totalResidual += (1.0f - alignmentError) * (1.0f - alignmentError);
        }

        Vector3 spineDir = (chest - pelvis).normalized;
        alignmentError = Vector3.Dot(spineDir, Vector3.up);

        // Se l'errore di allineamento è alto, correggiamo la schiena (0.98 ≈ 10°)
        if (alignmentError < 0.98f)
        {
            Vector3 correction = (Vector3.up - spineDir) * 0.1f;
            joints[chestIndex] += correction * (1 - confidences[chestIndex]);
            joints[pelvisIndex] += correction * (1 - confidences[pelvisIndex]);
            totalResidual += (1.0f - alignmentError) * (1.0f - alignmentError);
        }

        return totalResidual;
    }
}