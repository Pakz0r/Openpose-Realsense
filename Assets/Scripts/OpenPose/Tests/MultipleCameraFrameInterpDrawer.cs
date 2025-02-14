using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.IO;
using System;
using UnityEngine.Animations.Rigging;
using UnityEngine;
using MathNet.Numerics.LinearAlgebra;
using Utilities.Parser;
using OpenPose;

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
    private FrameSkeletonsPoints3D processedFrame = new();
    private FrameSkeletonsPoints3D lastFrame = new();
    private float interpolationTime = 1f;
    #endregion

    #region Unity Lifecycle
    private void OnEnable()
    {
        StartCoroutine(ReadFrames());
        StartCoroutine(DrawPersons());
        StartCoroutine(InterpolateFrames());
    }
    #endregion

    #region Private Lifecycle
    private IEnumerator ReadFrames()
    {
        for (int i = 0; i < 34; i++)
        {
            var filePath = Path.Combine(Application.streamingAssetsPath, $"frame{i}_skeletonsPoints3D.json");
            ReadNewFrameFromPath(filePath);
            yield return new WaitForSeconds(2f);
        }
    }

    private IEnumerator DrawPersons()
    {
        while (true)
        {
            if (processedFrame != null && processedFrame.People != null)
            {
                foreach (var person in processedFrame.People)
                {
                    // retrieve person data from processed frame
                    DrawPerson(person);
                }
            }

            yield return null;
        }
    }

    private IEnumerator InterpolateFrames()
    {
        while (true)
        {
            if (interpolationTime < 1.0f)
            {
                interpolationTime += Time.deltaTime;

                processedFrame.ID_Frame = currentFrame.ID_Frame;
                processedFrame.thingId = currentFrame.thingId;
                processedFrame.People = new PersonData[currentFrame.People.Length];

                for (int i = 0; i < currentFrame.People.Length; i++)
                {
                    processedFrame.People[i] = new PersonData();
                    var person = processedFrame.People[i];
                    var current = currentFrame.People[i];

                    // force assign data on person first time see
                    if (i >= lastFrame.People.Length)
                    {
                        processedFrame.People[i] = current;
                        continue;
                    }

                    var last = lastFrame.People[i];

                    person.personID = current.personID;

                    if (last.face_rotation != null)
                    {
                        // interpolate face rotation axis
                        person.face_rotation = new FaceRotation()
                        {
                            yaw = Mathf.Lerp(last.face_rotation.yaw, current.face_rotation.yaw, interpolationTime),
                            pitch = Mathf.Lerp(last.face_rotation.pitch, current.face_rotation.pitch, interpolationTime),
                            roll = Mathf.Lerp(last.face_rotation.roll, current.face_rotation.roll, interpolationTime),
                        };
                    }
                    else
                    {
                        person.face_rotation = current.face_rotation;
                    }


                    person.skeleton = new BoneData[current.skeleton.Length];

                    // interpolate bone positions
                    for (int j = 0; j < current.skeleton.Length; j++)
                    {
                        person.skeleton[j] = new BoneData();
                        var bone = person.skeleton[j];
                        bone.pointID = current.skeleton[j].pointID;
                        bone.confidence = Mathf.Lerp(last.skeleton[j].confidence, current.skeleton[j].confidence, interpolationTime);
                        bone.x = Mathf.Lerp(last.skeleton[j].x, current.skeleton[j].x, interpolationTime);
                        bone.y = Mathf.Lerp(last.skeleton[j].y, current.skeleton[j].y, interpolationTime);
                        bone.z = Mathf.Lerp(last.skeleton[j].z, current.skeleton[j].z, interpolationTime);
                    }
                }
            }

            yield return null;
        }
    }

    private async void ReadNewFrameFromPath(string filePath)
    {
        // save last frame for interpolation
        lastFrame = currentFrame;

        // read current frame from file path
        currentFrame = await filePath.ParseFromFileAsync<FrameSkeletonsPoints3D>();

        // force first frame to be the processed frame
        if (lastFrame == null || String.IsNullOrEmpty(lastFrame.thingId))
        {
            interpolationTime = 1.0f;
            processedFrame = currentFrame;
            return;
        }

        // reset the interpolation time
        interpolationTime = 0;
    }

    private void DrawPerson(PersonData personData)
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

        UpdatePersonObjectTransform(personObject, personData.skeleton, personData.face_rotation, minConfidence);

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

    private static void UpdateRigRotationConstraints(Rig personRig)
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

            var boneTargetName = boneTargetId.GetBoneName();

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

    private static void UpdateRigPositionConstraints(Rig personRig, BoneData[] skeleton, Vector3 rigPosition)
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
                var boneName = boneId.GetBoneName();

                if (boneObject.name == boneName)
                {
                    constraint.weight = EvalWeight(constraint.weight, boneData.confidence);
                    constraint.data.sourceObject.localPosition = new Vector3(boneData.x, boneData.y, boneData.z) - rigPosition;
                    break;
                }
            }
        }
    }

    private static void UpdatePersonObjectTransform(GameObject personObject, BoneData[] skeleton, FaceRotation rotation, float minConfidence)
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

        // eval rig rotation from plane interpolation
        // Calcola la normale del piano e il centro
        // (Vector3 normal, Vector3 centroid) = FitPlane(skeleton);
        // normal = Vector3.ProjectOnPlane(normal, Vector3.down); // force normal to point forward
        // personTransform.forward = normal;
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

    // Approccio Bayesiano (aggiornamento con prior)
    private static float EvalWeight(float currentWeight, float confidence)
    {
        //Un approccio elegante per aggiornare il peso potrebbe essere quello di usare un metodo Bayesiano.
        //Considera il peso al tempo (t-1) come un prior(una stima iniziale) e aggiorna il peso in base alla nuova confidence.
        //Questo approccio aggiorna il peso combinando l'informazione precedente (prior) e quella nuova (likelihood) in maniera ottimale.
        //Se la confidence corrente è alta, il peso si aggiorna verso l'alto; se è bassa, il peso si riduce.
        //Al passo t0 (sul prefab) il peso è 0.5.
        return (currentWeight * confidence) / (currentWeight * confidence + (1 - currentWeight) * (1 - confidence));
    }

    private static (Vector3 normal, Vector3 centroid) FitPlane(BoneData[] skeleton)
    {
        // Matrici per accumulare i valori
        float sumWeight = 0;
        float sumWX = 0, sumWY = 0, sumWZ = 0;
        float sumWXX = 0, sumWYY = 0, sumWZZ = 0;
        float sumWXY = 0, sumWXZ = 0, sumWYZ = 0;

        foreach (var bone in skeleton)
        {
            // Accumulo delle somme pesate
            sumWeight += bone.confidence;
            sumWX += bone.confidence * bone.x;
            sumWY += bone.confidence * bone.y;
            sumWZ += bone.confidence * bone.z;

            sumWXX += bone.confidence * bone.x * bone.x;
            sumWYY += bone.confidence * bone.y * bone.y;
            sumWZZ += bone.confidence * bone.z * bone.z;
            sumWXY += bone.confidence * bone.x * bone.y;
            sumWXZ += bone.confidence * bone.x * bone.z;
            sumWYZ += bone.confidence * bone.y * bone.z;
        }

        // Calcola il baricentro ponderato (centroid)
        Vector3 centroid = new(sumWX / sumWeight, sumWY / sumWeight, sumWZ / sumWeight);

        // Matrice di covarianza pesata (3x3)
        float[,] A = new float[3, 3];
        A[0, 0] = sumWXX - (sumWX * sumWX) / sumWeight;
        A[0, 1] = sumWXY - (sumWX * sumWY) / sumWeight;
        A[0, 2] = sumWXZ - (sumWX * sumWZ) / sumWeight;
        A[1, 0] = A[0, 1]; // Simmetria
        A[1, 1] = sumWYY - (sumWY * sumWY) / sumWeight;
        A[1, 2] = sumWYZ - (sumWY * sumWZ) / sumWeight;
        A[2, 0] = A[0, 2]; // Simmetria
        A[2, 1] = A[1, 2]; // Simmetria
        A[2, 2] = sumWZZ - (sumWZ * sumWZ) / sumWeight;

        // Trova la normale come l'autovettore corrispondente al più piccolo autovalore
        Vector3 normal = FindSmallestEigenVector(A);

        // Restituisci sia la normale che il baricentro
        return (normal, centroid);
    }

    // Funzione per trovare l'autovettore corrispondente al più piccolo autovalore
    private static Vector3 FindSmallestEigenVector(float[,] A)
    {
        // Converte la matrice float[,] in una matrice compatibile con MathNet
        var matrix = Matrix<float>.Build.DenseOfArray(A);

        // Calcola l'operazione di autovalore/autovettore
        var evd = matrix.Evd();

        // Trova l'autovalore più piccolo
        var eigenValues = evd.EigenValues.Real(); // Prendi solo la parte reale degli autovalori
        int minIndex = 0;

        for (int i = 1; i < eigenValues.Count; i++)
        {
            if (eigenValues[i] < eigenValues[minIndex])
                minIndex = i;
        }

        // Ottieni l'autovettore associato al più piccolo autovalore
        var smallestEigenVector = evd.EigenVectors.Column(minIndex);

        // Restituisce la normale come un Vector3
        return Vector3.Normalize(new Vector3(smallestEigenVector[0], smallestEigenVector[1], smallestEigenVector[2]));
    }
    #endregion
}
