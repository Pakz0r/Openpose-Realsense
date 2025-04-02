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
        (OpenPoseBone.Hips, OpenPoseBone.Head),
        (OpenPoseBone.Hips, OpenPoseBone.UpperChest),
        (OpenPoseBone.Hips, OpenPoseBone.LeftShoulder),
        (OpenPoseBone.Hips, OpenPoseBone.LeftLowerArm),
        (OpenPoseBone.Hips, OpenPoseBone.LeftHand),
        (OpenPoseBone.Hips, OpenPoseBone.RightShoulder),
        (OpenPoseBone.Hips, OpenPoseBone.RightLowerArm),
        (OpenPoseBone.Hips, OpenPoseBone.RightHand),
        (OpenPoseBone.Hips, OpenPoseBone.RightUpperLeg),
        (OpenPoseBone.Hips, OpenPoseBone.RightLowerLeg),
        (OpenPoseBone.Hips, OpenPoseBone.RightFoot),
        (OpenPoseBone.Hips, OpenPoseBone.LeftUpperLeg),
        (OpenPoseBone.Hips, OpenPoseBone.LeftLowerLeg),
        (OpenPoseBone.Hips, OpenPoseBone.LeftFoot),
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

    private const int limbSizeArray = 15;
    private readonly float defaultUpperBodyConfidence = 0.75f;
    private readonly float defaultLowerBodyConfidence = 0.75f;
    private readonly float confidenceTreshold = 0.2f;
    private readonly double toleranceFactor = 0.05; // Tolleranza sulla lunghezza dell'arto
    private readonly List<PoseConstraint> constraints = new();
    private readonly LevenbergMarquardtMinimizer optimizer = new();

    public PersonPoseGraphOptimizator(GameObject personPrefab, float boneConfidenceThreshold = 0.2f, 
        float newPoseUpperBodyConfidence = 0.75f, float newPoseLowerBodyConfidence = 0.75f,
        double toleranceFactor = 0.05)
    {
        if (personPrefab == null)
            throw new Exception("Invalid person prefab");

        if (!personPrefab.TryGetComponent<Animator>(out var animator))
            throw new Exception("Animator object not found");

        // Imposta le variabili per l'ottimizzatore
        this.confidenceTreshold = boneConfidenceThreshold;
        this.defaultUpperBodyConfidence = newPoseUpperBodyConfidence;
        this.defaultLowerBodyConfidence = newPoseLowerBodyConfidence;
        this.toleranceFactor = toleranceFactor;

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
        foreach (var limbs in openposeLimbDiffKeyId)
        {
            var distance = Vector3.Distance(bodyParts[(int)limbs.Item1].position, bodyParts[(int)limbs.Item2].position);
            var constraint = new PoseConstraint((int)limbs.Item1, (int)limbs.Item2, distance);
            constraints.Add(constraint);
        }
    }

    public void Optimize(ref BoneData[] skeleton, Vector3 initPosition)
    {
        // Costruzione della soluzione iniziale (posa ricevuta da openpose)
        var initialGuess = Vector<double>.Build.Dense(limbSizeArray * 3);

        for (int i = 0; i < limbSizeArray; i++)
        {
            var bone = new double[] { initPosition.x, 0f, initPosition.z };

            if (skeleton[i].confidence > confidenceTreshold)
            {
                bone[0] = skeleton[i].x;
                bone[1] = skeleton[i].y;
                bone[2] = skeleton[i].z;
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
        var result = optimizer.FindMinimum(objectiveFunction, initialGuess);

        // Visualizziamo i risultati
        for (int i = 0; i < limbSizeArray; i++)
        {
            var bone = skeleton[i];
            var position = result.MinimizingPoint.SubVector(i * 3, 3);
            bone.x = (float)position[0];
            bone.y = (float)position[1];
            bone.z = (float)position[2];

            // Impongo la nuova confidence di default per le ossa
            bone.confidence = (bone.pointID < (int)OpenPoseBone.Hips) ? defaultUpperBodyConfidence : defaultLowerBodyConfidence;
        }
    }

    private Vector<double> ComputeResiduals(Vector<double> x)
    {
        var residuals = Vector<double>.Build.Dense(constraints.Count);

        for (int i = 0; i < constraints.Count; i++)
        {
            int fromIndex = constraints[i].FromIndex;
            int toIndex = constraints[i].ToIndex;

            var fromPose = x.SubVector(fromIndex * 3, 3);
            var toPose = x.SubVector(toIndex * 3, 3);

            double currentDistance = (fromPose - toPose).L2Norm();
            double targetDistance = constraints[i].Distance;
            double tolerance = targetDistance * toleranceFactor;

            // Penalizzazione morbida se la distanza eccede la tolleranza
            double penalty = Math.Max(0, Math.Abs(currentDistance - targetDistance) - tolerance);

            residuals[i] = currentDistance - targetDistance + penalty;
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
