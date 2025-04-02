using System.Linq;
using System.IO;
using UnityEngine.Events;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Utilities.Parser;
using OpenPose;

using static SensorsManager;

public class SensorWatcher : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    private FrameSkeletonsPoints3D currentFrame;
    #endregion

    #region Public Events
    public static UnityEvent<SensorWatcher, FrameSkeletonsPoints3D> FrameReaded = new();
    public static UnityEvent<SensorWatcher, PersonData> PersonUpdated = new();
    #endregion

    #region Public Fields
    public SensorInfo Info { get; private set; }
    #endregion

    #region Private Fields
    private FileSystemWatcher watcher;
    private bool isProcessing;
    #endregion

    #region Unity Lifecycle
    private void OnDestroy()
    {
        watcher?.Dispose();
    }
    #endregion

    #region Public Methods
    public void SetupWatcher(SensorInfo sensor)
    {
        if (sensor == null)
        {
            Debug.LogError("Sensor data is invalid");
            return;
        }

        Info = sensor;

        if (string.IsNullOrEmpty(sensor.Folder))
        {
            Debug.LogError("Sensor directory root is invalid");
            return;
        }

        if (!Directory.Exists(sensor.Folder))
        {
            Debug.LogWarning($"Created missing sensor directory '{sensor.Folder}'");
            Directory.CreateDirectory(sensor.Folder);
        }

        watcher = new(sensor.Folder);

        watcher.Created += OnFileCreated;
        watcher.Error += OnWatcherError;

        watcher.Filter = "*.json";
        watcher.EnableRaisingEvents = true;
        //watcher.IncludeSubdirectories = true;

        var directory = new DirectoryInfo(watcher.Path);
        var lastFrame = directory.GetFiles()
             .OrderByDescending(f => f.LastWriteTime)
             .FirstOrDefault();

        if (lastFrame != null && lastFrame.Exists)
            ReadFrameFromFile(lastFrame.FullName);
    }
    #endregion

    #region Private Methods
    private async void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        await UniTask.SwitchToMainThread(); // force task to be executed on main thread
        ReadFrameFromFile(e.FullPath);
    }

    private async void OnWatcherError(object sender, ErrorEventArgs e)
    {
        await UniTask.SwitchToMainThread(); // force task to be executed on main thread
        var ex = e.GetException();

        if (ex != null)
        {
            Debug.LogException(ex);
        }
    }

    private async void ReadFrameFromFile(string framePath)
    {
        if (isProcessing)
        {
            Debug.Log("Received new frame while processing. Skipped!");
            return;
        }

        isProcessing = true;
        currentFrame = await framePath.ParseFromFileAsync<FrameSkeletonsPoints3D>();

        Debug.Log($"Frame {currentFrame.ID_Frame} readed for '{currentFrame.thingId}'");

        var sensorPosition = this.transform.position;

        foreach (var person in currentFrame.People)
        {
            foreach (var bone in person.skeleton)
            {
                var direction = sensorPosition + transform.TransformDirection(bone.x, bone.y, bone.z);
                bone.x = direction.x;
                bone.y = direction.y;
                bone.z = direction.z;
            }

            PersonUpdated?.Invoke(this, person);
        }

        FrameReaded?.Invoke(this, currentFrame);
        isProcessing = false;
    }
    #endregion
}
