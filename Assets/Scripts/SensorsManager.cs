using System.IO;
using System;
using UnityEngine;
using UnityEngine.Events;
using Utilities.Parser;
using OpenPose;
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
        public Sensor[] Sensors;
    }

    [Serializable]
    public class Sensor
    {
        public string ID;
        public string Folder;
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Offset;
    }
    #endregion

    #region Public Fields
    public static UnityEvent<Sensor> SensorCreated = new();
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

        var currentRoom = ApplicationLogic.Config.EnvironmentScene;
        var room = root.Rooms.FirstOrDefault(x => x.ID.Equals(currentRoom));

        foreach (var sensor in room.Sensors)
        {
            try
            {
                // create sensor object in scene
                var sensorObject = new GameObject($"DepthCamera_{sensor.ID}");
                sensorObject.transform.SetParent(this.transform);
                sensorObject.transform.SetLocalPositionAndRotation(
                    new Vector3(sensor.Position.x, sensor.Position.y, sensor.Position.z),
                    Quaternion.Euler(new Vector3(sensor.Rotation.x, sensor.Rotation.y, sensor.Rotation.z))
                );

                // add camera to check frames result
                sensorObject.AddComponent<Camera>();

                // apply sensor offset
                var sensorOffset = new GameObject("Offset");
                sensorOffset.transform.SetParent(sensorObject.transform);
                sensorOffset.transform.SetLocalPositionAndRotation(
                    new Vector3(sensor.Offset.x, sensor.Offset.y, sensor.Offset.z),
                    Quaternion.identity
                );

                // setup camera watcher
                var watcher = sensorOffset.AddComponent<SensorWatcher>();
                watcher.SetupWatcher(sensor.Folder);

                // invoke sensor created event
                SensorCreated?.Invoke(sensor);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
    }
    #endregion
}
