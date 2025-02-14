using System.Collections.Generic;
using System.Linq;
using System.IO;
using Utilities.Parser;
using UnityEngine;
using OpenPose;
using Cysharp.Threading.Tasks;
using System;

public class RoomFrameWatcher : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    private string roomFilePath = "C:\\Users\\franc\\Desktop\\Tesi\\project\\_rooms";
    [SerializeField]
    private GameObject roomPrefab;
    [SerializeField]
    private int maxRoomGridRowSize = 5;
    [SerializeField]
    private Vector2 roomGridOffset;
    [SerializeField]
    private Transform roomRoot;
    #endregion

    #region Private Fields
    private FileSystemWatcher watcher;
    private static Dictionary<string, FrameSkeletonsPoints3D> roomFrames = new();
    private static Dictionary<string, Transform> roomTransform = new();
    private static int roomGridRowIndex = -1;
    private static int roomGridColumnIndex;
    #endregion

    #region Unity Lifecycle
    private async void Awake()
    {
        if (string.IsNullOrEmpty(roomFilePath))
        {
            Debug.LogError("Room directory root is invalid");
            return;
        }

        if (!Directory.Exists(roomFilePath))
        {
            Debug.LogError("Room directory root does not exists");
            return;
        }

        watcher = new FileSystemWatcher(roomFilePath);

        watcher.Created += OnCreated;
        watcher.Error += OnError;

        watcher.Filter = "*.json";
        watcher.IncludeSubdirectories = true;
        watcher.EnableRaisingEvents = true;

        if (roomPrefab == null)
        {
            Debug.LogError("Room prefab not found");
            return;
        }

        // Reset for unity editor
        roomGridRowIndex = -1;
        roomGridColumnIndex = 0;

        var directories = Directory.GetDirectories(roomFilePath);

        foreach (var directory in directories)
        {
            var directoryInfo = new DirectoryInfo(directory);
            var roomName = directory.Split('\\').LastOrDefault();
            CreateNewRoomInstance(roomName);

            var files = directoryInfo.GetFiles();

            if (files.Length > 0)
            {
                var fileInfo = files.OrderByDescending(f => f.LastWriteTime).First();
                roomFrames[roomName] = await fileInfo.FullName.ParseFromFileAsync<FrameSkeletonsPoints3D>();
                UpdateFrame(roomName);
            }
        }
    }

    private void OnDestroy()
    {
        watcher?.Dispose();
    }
    #endregion

    #region Private Methods
    private async void OnCreated(object sender, FileSystemEventArgs e)
    {
        await UniTask.SwitchToMainThread(); // force task to be executed on main thread

        var fileInfo = new FileInfo(e.FullPath);
        var directoryPath = fileInfo.DirectoryName;
        var roomName = directoryPath.Split('\\').LastOrDefault();

        if (!ExistRoomInstance(roomName))
        {
            CreateNewRoomInstance(roomName);
        }

        roomFrames[roomName] = await e.FullPath.ParseFromFileAsync<FrameSkeletonsPoints3D>();
        UpdateFrame(roomName);

        Debug.Log($"Updated FrameSkeletonPoints for: {roomName}");
    }

    private async void OnError(object sender, ErrorEventArgs e)
    {
        await UniTask.SwitchToMainThread(); // force task to be executed on main thread

        var ex = e.GetException();

        if (ex != null)
        {
            Debug.LogException(ex);
        }
    }

    private void CreateNewRoomInstance(string roomName)
    {
        roomGridRowIndex++;
        if (roomGridRowIndex > 0 && roomGridRowIndex % maxRoomGridRowSize == 0) roomGridColumnIndex++;
        InstantiateRoom(roomName);
    }

    private void InstantiateRoom(string roomName)
    {

        var roomObject = GameObject.Instantiate(roomPrefab);
        roomObject.transform.position = new Vector3(roomGridOffset.x * (roomGridRowIndex % maxRoomGridRowSize), 0, roomGridOffset.y * roomGridColumnIndex);
        roomObject.transform.parent = roomRoot;
        roomObject.name = roomName;

        roomTransform[roomName] = roomObject.transform;

        Debug.Log($"Created room: {roomName}");
    }

    private bool ExistRoomInstance(string roomName)
    {
        return roomTransform.ContainsKey(roomName);
    }

    private void UpdateFrame(string roomName)
    {
        for (int i = 0; i < roomTransform[roomName].childCount; i++)
        {
            Destroy(roomTransform[roomName].GetChild(i).gameObject);
        }

        foreach (var personData in roomFrames[roomName].People)
        {
            var personObject = new GameObject();
            personObject.name = $"Person {personData.personID}";
            personObject.transform.parent = roomTransform[roomName];
            personObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            personObject.transform.localScale = Vector3.one;

            foreach (var boneData in personData.skeleton)
            {
                if (boneData.confidence > 0)
                {
                    var boneId = (OpenPoseBone)boneData.pointID;
                    var boneObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    boneObject.name = boneId.GetBoneName(); //$"Bone {boneData.pointID}";
                    boneObject.transform.parent = personObject.transform;
                    boneObject.transform.SetLocalPositionAndRotation(new Vector3(boneData.x, boneData.y, boneData.z), Quaternion.identity);
                    boneObject.transform.localScale = Vector3.one * 0.05f;
                }
            }
        }
    }
    #endregion
}
