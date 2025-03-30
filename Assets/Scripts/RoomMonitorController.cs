using UnityEngine;

public class RoomMonitorController : MonoBehaviour
{
    [SerializeField]
    private GameObject normalState;

    [SerializeField]
    private GameObject fallenState;

    private void Awake()
    {
        normalState.SetActive(true);
        fallenState.SetActive(false);
    }

    private void OnEnable()
    {
        EventManager.Subscribe("RoomMonitorFallenState", UpdateRoomMonitorFallenState);
    }

    private void OnDisable()
    {
        EventManager.Unsubscribe("RoomMonitorFallenState", UpdateRoomMonitorFallenState);
    }

    private void UpdateRoomMonitorFallenState(object value)
    {
        bool hasAnyFallen = (bool)value;
        normalState.SetActive(!hasAnyFallen);
        fallenState.SetActive(hasAnyFallen);
    }
}
