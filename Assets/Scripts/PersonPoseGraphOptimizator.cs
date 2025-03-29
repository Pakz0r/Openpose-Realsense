using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Optimization;
using OpenPose;
using System;
using System.Collections.Generic;
using UnityEngine;

public class PersonPoseGraphOptimizator
{
    private readonly static (OpenPoseBone, OpenPoseBone)[] openposeLimbDiffKeyId =
    {
        (OpenPoseBone.Head, OpenPoseBone.UpperChest),
        (OpenPoseBone.UpperChest, OpenPoseBone.Hips),

        (OpenPoseBone.UpperChest, OpenPoseBone.RightShoulder),
        (OpenPoseBone.RightShoulder, OpenPoseBone.RightLowerArm),
        (OpenPoseBone.RightLowerArm, OpenPoseBone.RightHand),

        (OpenPoseBone.UpperChest, OpenPoseBone.LeftShoulder),
        (OpenPoseBone.LeftShoulder, OpenPoseBone.LeftLowerArm),
        (OpenPoseBone.LeftLowerArm, OpenPoseBone.LeftHand),
        
        (OpenPoseBone.RightShoulder, OpenPoseBone.LeftShoulder),
        (OpenPoseBone.RightShoulder, OpenPoseBone.RightUpperLeg),
        (OpenPoseBone.LeftShoulder, OpenPoseBone.LeftLowerLeg),

        (OpenPoseBone.Hips, OpenPoseBone.RightUpperLeg),
        (OpenPoseBone.RightUpperLeg, OpenPoseBone.RightLowerLeg),
        (OpenPoseBone.RightLowerLeg, OpenPoseBone.RightFoot),

        (OpenPoseBone.Hips, OpenPoseBone.LeftUpperLeg),
        (OpenPoseBone.LeftUpperLeg, OpenPoseBone.LeftLowerLeg),
        (OpenPoseBone.LeftLowerLeg, OpenPoseBone.LeftFoot),
    };

    public class PoseConstraint
    {
        public int FromIndex { get; set; }
        public int ToIndex { get; set; }
        public double Distance { get; set; }

        public PoseConstraint(int fromIndex, int toIndex, double distance)
        {
            FromIndex = fromIndex;
            ToIndex = toIndex;
            Distance = distance;
        }
    }

    private const int LIMB_ARRAY_SIZE = 15;
    private List<PoseConstraint> constraints = new();

    public PersonPoseGraphOptimizator(GameObject personPrefab)
    {
        if (personPrefab == null)
            throw new Exception("Invalid person prefab");

        if (!personPrefab.TryGetComponent<Animator>(out var animator))
            throw new Exception("Animator object not found");

        Transform[] bodyParts = new Transform[LIMB_ARRAY_SIZE];

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

        foreach (var limbs in openposeLimbDiffKeyId)
        {
            var distance = Vector3.Distance(bodyParts[(int)limbs.Item1].position, bodyParts[(int)limbs.Item2].position);
            var constraint = new PoseConstraint((int)limbs.Item1, (int)limbs.Item2, distance);
            constraints.Add(constraint);
        }
    }

    public void Optimize(ref BoneData[] skeleton)
    {
        // Costruzione della soluzione iniziale
        var initialGuess = Vector<double>.Build.Dense(LIMB_ARRAY_SIZE * 3);

        for (int i = 0; i < LIMB_ARRAY_SIZE; i++)
        {
            var bone = new double[] { 0f, 0f, 0f };

            if (skeleton[i].confidence > 0.2f)
            {
                bone = new double[] { skeleton[i].x, skeleton[i].y, skeleton[i].z };
            }

            var guess = Vector<double>.Build.DenseOfArray(bone);
            initialGuess.SetSubVector(i * 3, 3, guess);
        }

        // Definizione della funzione obiettivo
        var objectiveFunction = ObjectiveFunction.NonlinearModel(
            (x, yObserved) => ComputeResiduals(x), // Funzione errore
            (x, yObserved) => ComputeJacobian(x),  // Jacobiano
            Vector<double>.Build.Dense(constraints.Count), // Valori osservati X
            Vector<double>.Build.Dense(constraints.Count) // Valori osservati Y
        );

        // Creazione del solver Levenberg-Marquardt
        var optimizer = new LevenbergMarquardtMinimizer();
        var result = optimizer.FindMinimum(objectiveFunction, initialGuess);

        // Visualizziamo i risultati
        for (int i = 0; i < LIMB_ARRAY_SIZE; i++)
        {
            var bone = skeleton[i];
            var position = result.MinimizingPoint.SubVector(i * 3, 3);
            bone.x = (float)position[0];
            bone.y = (float)position[1];
            bone.z = (float)position[2];
            bone.confidence = Mathf.Clamp01(bone.confidence + 0.5f);
        }
    }

    private Vector<double> ComputeResiduals(Vector<double> x)
    {
        double tolerance = 0.01f;
        var residuals = Vector<double>.Build.Dense(constraints.Count);

        for (int i = 0; i < constraints.Count; i++)
        {
            int fromIndex = constraints[i].FromIndex;
            int toIndex = constraints[i].ToIndex;

            var fromPose = x.SubVector(fromIndex * 3, 3);
            var toPose = x.SubVector(toIndex * 3, 3);

            double currentDistance = (fromPose - toPose).L2Norm();
            double diff = Math.Max(0, Math.Abs(currentDistance - constraints[i].Distance) - tolerance);
            residuals[i] = diff * diff;
        }

        return residuals;
    }

    private Matrix<double> ComputeJacobian(Vector<double> x)
    {
        var jacobian = Matrix<double>.Build.Dense(constraints.Count, x.Count);

        for (int i = 0; i < constraints.Count; i++)
        {
            int fromIndex = constraints[i].FromIndex;
            int toIndex = constraints[i].ToIndex;

            var fromPose = x.SubVector(fromIndex * 3, 3);
            var toPose = x.SubVector(toIndex * 3, 3);
            var diff = fromPose - toPose;

            double currentDistance = diff.L2Norm();
            double targetDistance = constraints[i].Distance;

            if (currentDistance < 1e-6 || Math.Abs(targetDistance) < 1e-6)
                continue;

            var grad = (diff / currentDistance) / targetDistance; // Normalizziamo rispetto alla distanza target

            jacobian.SetSubMatrix(i, fromIndex * 3, grad.ToColumnMatrix().Transpose());
            jacobian.SetSubMatrix(i, toIndex * 3, -grad.ToColumnMatrix().Transpose());
        }

        return jacobian;
    }

}
