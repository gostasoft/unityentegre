using UnityEngine;
using UnityEngine.InputSystem;

namespace Metin2Dev.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class Metin2GameplayCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 7.5f;
        public float height = 2.1f;
        public float yaw = 180f;
        public float pitch = 24f;
        public float rotationSpeed = 0.18f;
        public float zoomSpeed = 0.012f;
        public float smoothing = 14f;

        void LateUpdate()
        {
            if (target == null) return;
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * rotationSpeed;
                pitch = Mathf.Clamp(pitch - delta.y * rotationSpeed, 8f, 58f);
            }
            if (mouse != null)
                distance = Mathf.Clamp(distance - mouse.scroll.ReadValue().y * zoomSpeed, 3.5f, 14f);

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 focus = target.position + Vector3.up * height;
            Vector3 desired = focus - rotation * Vector3.forward * distance;
            if (Physics.Linecast(focus, desired, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) &&
                !hit.transform.IsChildOf(target))
                desired = hit.point + hit.normal * 0.18f;
            float t = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, t);
        }
    }
}
