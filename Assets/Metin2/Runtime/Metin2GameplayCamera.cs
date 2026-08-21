using UnityEngine;
using UnityEngine.InputSystem;

namespace Metin2Dev.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class Metin2GameplayCamera : MonoBehaviour
    {
        public Transform target;
        public Transform firstPersonAnchor;
        public bool firstPerson = true;
        public float distance = 7.5f;
        public float height = 1.58f;
        public float yaw = 180f;
        // First-person must start level; a positive pitch makes the camera stare at the torso.
        public float pitch = 0f;
        public float rotationSpeed = 0.18f;
        [Header("Third Person Zoom")]
        [Tooltip("Mouse wheel responsiveness in third-person view.")]
        public float zoomSpeed = 0.025f;
        [Tooltip("Closest allowed third-person camera distance.")]
        public float minThirdPersonDistance = 1.5f;
        [Tooltip("Farthest allowed third-person camera distance.")]
        public float maxThirdPersonDistance = 28f;
        public float smoothing = 14f;
        public float minFieldOfView = 35f;
        public float maxFieldOfView = 75f;
        public float fieldOfViewZoomSpeed = 0.8f;
        Renderer[] firstPersonHiddenRenderers;
        Quaternion firstPersonAnchorRestLocalRotation = Quaternion.identity;

        public void Follow(Transform value, Transform eyeAnchor = null, bool snap = true)
        {
            target = value;
            firstPersonAnchor = eyeAnchor;
            if (target != null && firstPersonAnchor != null)
                firstPersonAnchorRestLocalRotation = Quaternion.Inverse(target.rotation) * firstPersonAnchor.rotation;
            if (firstPerson) pitch = 0f;
            Camera camera = GetComponent<Camera>();
            if (firstPerson && camera != null) camera.nearClipPlane = 0.03f;
            if (snap && target != null) SnapToTarget();
        }

        public void ToggleView()
        {
            firstPerson = !firstPerson;
            Camera camera = GetComponent<Camera>();
            if (camera != null) camera.nearClipPlane = firstPerson ? 0.03f : 0.1f;
            ApplyFirstPersonRendererVisibility();
            if (target != null) SnapToTarget();
        }

        public void AdjustThirdPersonDistance(float verticalDragDelta)
        {
            if (Mathf.Approximately(verticalDragDelta, 0f)) return;
            if (firstPerson) ToggleView();
            distance = Mathf.Clamp(distance - verticalDragDelta * zoomSpeed,
                minThirdPersonDistance, maxThirdPersonDistance);
        }

        public void SetFirstPersonHiddenRenderers(Renderer[] renderers)
        {
            firstPersonHiddenRenderers = renderers;
            ApplyFirstPersonRendererVisibility();
        }

        void ApplyFirstPersonRendererVisibility()
        {
            if (firstPersonHiddenRenderers == null) return;
            foreach (Renderer renderer in firstPersonHiddenRenderers)
                if (renderer != null) renderer.enabled = !firstPerson;
        }

        void LateUpdate()
        {
            if (target == null) return;
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * rotationSpeed;
                pitch = Mathf.Clamp(pitch - delta.y * rotationSpeed, -75f, 75f);
            }
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (firstPerson)
                {
                    Camera camera = GetComponent<Camera>();
                    if (camera != null)
                        camera.fieldOfView = Mathf.Clamp(camera.fieldOfView - wheel * fieldOfViewZoomSpeed,
                            minFieldOfView, maxFieldOfView);
                }
                else
                {
                    distance = Mathf.Clamp(distance - wheel * zoomSpeed, minThirdPersonDistance, maxThirdPersonDistance);
                }
            }

            if (firstPerson)
            {
                target.rotation = Quaternion.Euler(0f, yaw, 0f);
                // Skill clips turn the animated body/head underneath the player root. Apply only that
                // animated yaw delta on top of mouse yaw, so a sword spin carries the camera with it.
                Quaternion expectedAnchorRotation = target.rotation * firstPersonAnchorRestLocalRotation;
                Quaternion animatedAnchorDelta = firstPersonAnchor != null
                    ? Quaternion.Inverse(expectedAnchorRotation) * firstPersonAnchor.rotation
                    : Quaternion.identity;
                float animatedYaw = Mathf.DeltaAngle(0f, animatedAnchorDelta.eulerAngles.y);
                // Keep this distinct from the third-person rotation variable below (C# scopes both declarations).
                Quaternion firstPersonRotation = Quaternion.Euler(pitch, yaw + animatedYaw, 0f);
                transform.SetPositionAndRotation(FirstPersonPosition(), firstPersonRotation);
                return;
            }
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 focus = target.position + Vector3.up * height;
            Vector3 desired = DesiredPosition(rotation);
            if (Physics.Linecast(focus, desired, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) &&
                !hit.transform.IsChildOf(target))
                desired = hit.point + hit.normal * 0.18f;
            float t = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, t);
        }

        void SnapToTarget()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(firstPerson ? FirstPersonPosition() : DesiredPosition(rotation), rotation);
        }

        Vector3 FirstPersonPosition()
        {
            Vector3 anchor = firstPersonAnchor != null ? firstPersonAnchor.position : target.position + Vector3.up * height;
            Quaternion horizontalRotation = Quaternion.Euler(0f, yaw, 0f);
            return anchor + Vector3.up * 0.32f + horizontalRotation * Vector3.forward * 0.18f;
        }

        Vector3 DesiredPosition(Quaternion rotation)
        {
            Vector3 focus = target.position + Vector3.up * height;
            return focus - rotation * Vector3.forward * distance;
        }
    }
}
