using UnityEngine;
using UnityEngine.InputSystem;

namespace RateworksPrototype
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class EngineerController : MonoBehaviour
    {
        public Camera view;
        public FactoryGame game;
        public Transform lookTarget;
        CharacterController body;
        float pitch = 25f, yaw, vertical;
        public bool CursorMode { get; private set; } = true;
        void Awake() { body = GetComponent<CharacterController>(); SetCursor(true); }
        public void SetCursor(bool free)
        {
            CursorMode = free;
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }
        public void ResetPosition()
        {
            if (body == null) body = GetComponent<CharacterController>();
            body.enabled = false; transform.position = new Vector3(0, 0.1f, -7.5f); body.enabled = true;
            yaw = 0; pitch = 25; vertical = 0; transform.rotation = Quaternion.identity;
            (lookTarget != null ? lookTarget : view.transform).localRotation = Quaternion.Euler(pitch, 0, 0);
            if (game.presentation != null) game.presentation.ResetCameraTracking();
        }
        void Update()
        {
            if (game == null || !game.Playing || game.ResultsVisible) { if (!CursorMode) SetCursor(true); return; }
            var k = Keyboard.current; var m = Mouse.current;
            if (k == null || m == null) return;
            if (k.tabKey.wasPressedThisFrame) SetCursor(!CursorMode);
            if (k.escapeKey.wasPressedThisFrame) { game.CancelTool(); SetCursor(true); }
            Vector3 move = Vector3.zero;
            if (!CursorMode)
            {
                var delta = m.delta.ReadValue(); yaw += delta.x * 0.1f; pitch = Mathf.Clamp(pitch - delta.y * 0.1f, -75, 80);
                transform.rotation = Quaternion.Euler(0, yaw, 0);
                (lookTarget != null ? lookTarget : view.transform).localRotation = Quaternion.Euler(pitch, 0, 0);
                move = new Vector3((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0), 0, (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0));
                move = transform.TransformDirection(Vector3.ClampMagnitude(move, 1)) * 4.5f;
            }
            // Releasing the cursor must not suspend gravity in mid-air.
            vertical = body.isGrounded ? -2 : Mathf.Max(-30, vertical - 20 * Time.deltaTime);
            body.Move((move + Vector3.up * vertical) * Time.deltaTime);
        }
        void OnDisable() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}
