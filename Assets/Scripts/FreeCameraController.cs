using UnityEngine;
using UnityEngine.InputSystem;

public class FreeCameraController : MonoBehaviour
{
    [Header("Look Settings")]
    public float lookSpeed = 5f; // Velocità di rotazione
    public Vector2 lookLimits = new Vector2(-90, 90); // Limiti verticali

    [Header("Zoom Settings")]
    public float zoomSpeed = 10f; // Velocità di zoom
    public float minZoom = 5f; // Distanza minima
    public float maxZoom = 20f; // Distanza massima

    private InputAction lookAction;
    private InputAction zoomAction;
    private InputAction lookToggleAction;

    private float currentZoom = 10f; // Distanza corrente della telecamera
    private Vector2 currentRotation; // Rotazione corrente della telecamera
    private bool isLooking = false; // Flag per abilitare/disabilitare il look

    [SerializeField] private InputActionAsset inputActions; // Riferimento al tuo Input Action Asset

    private void Start()
    {
        // Recupera le azioni dall'InputActionAsset
        var cameraControls = inputActions.FindActionMap("CameraControls");
        lookAction = cameraControls.FindAction("Look");
        zoomAction = cameraControls.FindAction("Zoom");
        lookToggleAction = cameraControls.FindAction("LookToggle");

        // Registrazione agli eventi
        lookAction.performed += ctx => PerformLook(ctx.ReadValue<Vector2>());
        zoomAction.performed += ctx => PerformZoom(ctx.ReadValue<float>());
        zoomAction.canceled += ctx => PerformZoom(0); // Resetta lo zoom
        lookToggleAction.performed += ctx => ToggleLook(true);
        lookToggleAction.canceled += ctx => ToggleLook(false);

        // Abilita le azioni
        lookAction.Enable();
        zoomAction.Enable();
        lookToggleAction.Enable();

        currentRotation = new Vector2(transform.eulerAngles.x, transform.eulerAngles.y);
    }

    private void OnDestroy()
    {
        // Disiscrizione dagli eventi per evitare memory leak
        lookAction.performed -= ctx => PerformLook(ctx.ReadValue<Vector2>());
        zoomAction.performed -= ctx => PerformZoom(ctx.ReadValue<float>());
        zoomAction.canceled -= ctx => PerformZoom(0);
        lookToggleAction.performed -= ctx => ToggleLook(true);
        lookToggleAction.canceled -= ctx => ToggleLook(false);
    }

    private void ToggleLook(bool isActive)
    {
        isLooking = isActive;
    }

    private void PerformLook(Vector2 input)
    {
        if (!isLooking) return;

        // Gestione della rotazione (guardarsi attorno)
        currentRotation.x -= input.y * lookSpeed * Time.deltaTime; // Asse verticale
        currentRotation.y += input.x * lookSpeed * Time.deltaTime; // Asse orizzontale

        // Clamp dei limiti di rotazione verticale
        currentRotation.x = Mathf.Clamp(currentRotation.x, lookLimits.x, lookLimits.y);

        // Applicazione della rotazione
        transform.rotation = Quaternion.Euler(currentRotation.x, currentRotation.y, 0f);
    }

    private void PerformZoom(float input)
    {
        // Gestione dello zoom
        currentZoom -= input * zoomSpeed * Time.deltaTime;
        currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);

        // Applicazione dello zoom
        transform.position += transform.forward * (input * zoomSpeed * Time.deltaTime);
    }
}
