using System.Linq;
using System.IO;
using UnityEngine.Events;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Utilities.Parser;
using OpenPose;

public class SensorWatcher : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    private FrameSkeletonsPoints3D currentFrame;
    #endregion

    #region Public Events
    public static UnityEvent<SensorWatcher, FrameSkeletonsPoints3D> OnNewFrame = new UnityEvent<SensorWatcher, FrameSkeletonsPoints3D>();
    public static UnityEvent<SensorWatcher, PeopleData> OnPersonUpdate = new UnityEvent<SensorWatcher, PeopleData>();
    #endregion

    #region Private Fields
    private FileSystemWatcher watcher;
    #endregion

    #region Unity Lifecycle
    private void OnDestroy()
    {
        watcher?.Dispose();
    }
    #endregion

    #region Public Methods
    public void SetupWatcher(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("Sensor directory root is invalid");
            return;
        }

        if (!Directory.Exists(path))
        {
            Debug.LogError("Sensor directory root does not exists");
            return;
        }

        watcher = new(path);

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
        currentFrame = await framePath.ParseFromFileAsync<FrameSkeletonsPoints3D>();
        OnNewFrame?.Invoke(this, currentFrame);

        foreach (var person in currentFrame.People)
        {
            OnPersonUpdate?.Invoke(this, person);
        }
    }
    #endregion
}
