using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class TerrainCameraController : MonoBehaviour
{
    [Header("Движение")]
    public float moveSpeed = 18f;
    public float shiftMultiplier = 3f;

    [Header("Мышь")]
    public float mouseSensitivity = 0.12f;

    private float yaw;
    private float pitch;

    private void Start()
    {
        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;

        LockCursor();
    }

    private void Update()
    {
        // ДЛЯ ЗАЩИТЫ: камера нужна только для свободного осмотра сгенерированного mesh-ландшафта.
        RotateCamera();
        MoveCamera();
        UpdateCursorLock();
    }

    private void RotateCamera()
    {
        Vector2 mouseDelta = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            mouseDelta = Mouse.current.delta.ReadValue();
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        mouseDelta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#endif

        yaw += mouseDelta.x * mouseSensitivity;
        pitch -= mouseDelta.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -89f, 89f);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void MoveCamera()
    {
        Vector3 direction = Vector3.zero;
        bool shiftPressed = false;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        if (keyboard.wKey.isPressed)
        {
            direction += transform.forward;
        }

        if (keyboard.sKey.isPressed)
        {
            direction -= transform.forward;
        }

        if (keyboard.dKey.isPressed)
        {
            direction += transform.right;
        }

        if (keyboard.aKey.isPressed)
        {
            direction -= transform.right;
        }

        shiftPressed = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.W))
        {
            direction += transform.forward;
        }

        if (Input.GetKey(KeyCode.S))
        {
            direction -= transform.forward;
        }

        if (Input.GetKey(KeyCode.D))
        {
            direction += transform.right;
        }

        if (Input.GetKey(KeyCode.A))
        {
            direction -= transform.right;
        }

        shiftPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif

        float currentSpeed = shiftPressed ? moveSpeed * shiftMultiplier : moveSpeed;
        transform.position += direction.normalized * currentSpeed * Time.deltaTime;
    }

    private void UpdateCursorLock()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            UnlockCursor();
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            LockCursor();
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            UnlockCursor();
        }

        if (Input.GetMouseButtonDown(0))
        {
            LockCursor();
        }
#endif
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
