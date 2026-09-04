using UnityEngine;
using UnityEngine.InputSystem;

namespace Traverser
{
    public class TraverserCameraController : MonoBehaviour
    {
        public Transform cameraTarget;
        public float topClamp = 40.0f;
        public float bottomClamp = -20.0f;

        TraverserInputController input;
        float yaw;
        float pitch;

        void Start()
        {
            input = GetComponent<TraverserInputController>();
            if (cameraTarget != null)
            {
                var euler = cameraTarget.rotation.eulerAngles;
                yaw = euler.y;
                pitch = euler.x;
                if (pitch > 180.0f)
                    pitch -= 360.0f;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void LateUpdate()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (input == null || cameraTarget == null)
                return;

            var look = input.GetInputLook();
            yaw += look.x;
            pitch += look.y;
            pitch = Mathf.Clamp(pitch, bottomClamp, topClamp);
            cameraTarget.rotation = Quaternion.Euler(pitch, yaw, 0.0f);
            input.ConsumeLook();
        }
    }
}
