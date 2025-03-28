using System.IO;
using System;
using UnityEngine;
using UnityEngine.Events;
using Utilities.Parser;
using System.Linq;

public class SensorsManager : MonoBehaviour
{
    #region Classes
    [Serializable]
    public class RootObject
    {
        public RoomSensors[] Rooms;
    }

    [Serializable]
    public class RoomSensors
    {
        public string ID;
        public SensorInfo[] Sensors;
    }

    [Serializable]
    public class SensorInfo
    {
        public string ID;
        public string Folder;
        public WorldPoint Transform;
        public WorldPoint Offset;

        [Serializable]
        public class WorldPoint
        {
            public Vector3 Position;
            public Vector3 Rotation;
        }
    }
    #endregion

    #region Public Fields
    public static UnityEvent<SensorInfo> SensorInitialized = new();
    public static UnityEvent<SensorInfo> SensorCreated = new();
    #endregion

    #region Unity Lifecycle
    private async void OnEnable()
    {
        string filePath = Path.Combine(Application.dataPath, "../sensors.json");

        if (!File.Exists(filePath))
        {
            Debug.LogError("Sensors file not found");
            return;
        }

        var root = await JSON.ParseFromFileAsync<RootObject>(filePath);

        var currentRoom = ApplicationConfig.Instance.EnvironmentScene;
        var room = root.Rooms.FirstOrDefault(x => x.ID.Equals(currentRoom));

        foreach (var sensor in room.Sensors)
        {
            try
            {
                // create sensor object in scene
                var sensorObject = new GameObject($"DepthCamera_{sensor.ID}");
                sensorObject.transform.SetParent(this.transform);
                sensorObject.transform.SetLocalPositionAndRotation(
                    new Vector3(sensor.Transform.Position.x, sensor.Transform.Position.y, sensor.Transform.Position.z),
                    Quaternion.Euler(new Vector3(sensor.Transform.Rotation.x, sensor.Transform.Rotation.y, sensor.Transform.Rotation.z))
                );

                // add camera to check frames result
                var camera = sensorObject.AddComponent<Camera>();
                camera.fieldOfView = 58; // Intel Realsense D435i has 58° of vertical fov
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 10.0f;

                // apply sensor offset
                var sensorOffset = new GameObject("Offset");
                sensorOffset.transform.SetParent(sensorObject.transform);
                sensorOffset.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                sensorOffset.transform.localScale = Vector3.one;

                // invoke sensor created event
                SensorCreated?.Invoke(sensor);

                // setup camera watcher
                var watcher = sensorOffset.AddComponent<SensorWatcher>();
                watcher.SetupWatcher(sensor);

                // invoke sensor initialized event
                SensorInitialized?.Invoke(sensor);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
    }
    #endregion
}
