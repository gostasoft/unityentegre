using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Metin2Dev.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class Metin2MobileInputDriver : MonoBehaviour
    {
        static Metin2MobileInputDriver instance;
        static Vector2 movement;
        static bool attackHeld;
        static int pendingSkill = -1;

        Keyboard mobileKeyboard;
        float nextAttackPulseAt;
        bool queuedAttackLastUpdate;

        public static void SetMovement(Vector2 value) => movement = Vector2.ClampMagnitude(value, 1f);

        public static void SetAttackHeld(bool held)
        {
            if (held && !attackHeld) instance?.ResetAttackPulse();
            attackHeld = held;
        }

        public static void ActivateSkill(int quickSlotIndex) => pendingSkill = Mathf.Clamp(quickSlotIndex, 0, 7);

        void OnEnable()
        {
            if (!Application.isPlaying || instance != null && instance != this) return;
            Metin2MobileGameplayUI mobileUi = GetComponentInParent<Metin2MobileGameplayUI>();
            if (mobileUi != null && !mobileUi.ShouldUseMobileLayout) return;
            instance = this;
            mobileKeyboard = InputSystem.AddDevice<Keyboard>("Metin2 Mobile Controls");
            InputSystem.onBeforeUpdate += QueueKeyboardState;
        }

        void OnDisable()
        {
            movement = Vector2.zero;
            pendingSkill = -1;
            attackHeld = false;
            InputSystem.onBeforeUpdate -= QueueKeyboardState;
            if (mobileKeyboard != null && mobileKeyboard.added) InputSystem.RemoveDevice(mobileKeyboard);
            mobileKeyboard = null;
            if (instance == this) instance = null;
        }

        void ResetAttackPulse()
        {
            nextAttackPulseAt = 0f;
            queuedAttackLastUpdate = false;
        }

        void QueueKeyboardState()
        {
            if (mobileKeyboard == null || !mobileKeyboard.added) return;
            List<Key> keys = new List<Key>(6);
            const float threshold = 0.18f;
            if (movement.y > threshold) keys.Add(Key.W);
            if (movement.y < -threshold) keys.Add(Key.S);
            if (movement.magnitude >= 0.72f) keys.Add(Key.LeftShift);
            int skill = pendingSkill;
            pendingSkill = -1;
            switch (skill)
            {
                case 0: keys.Add(Key.Digit1); break;
                case 1: keys.Add(Key.Digit2); break;
                case 2: keys.Add(Key.Digit3); break;
                case 3: keys.Add(Key.Digit4); break;
                case 4: keys.Add(Key.F1); break;
                case 5: keys.Add(Key.F2); break;
                case 6: keys.Add(Key.F3); break;
                case 7: keys.Add(Key.F4); break;
            }
            if (movement.x > threshold) keys.Add(Key.D);
            if (movement.x < -threshold) keys.Add(Key.A);

            bool attackPulse = attackHeld && !queuedAttackLastUpdate && Time.unscaledTime >= nextAttackPulseAt;
            if (attackPulse)
            {
                keys.Add(Key.Space);
                nextAttackPulseAt = Time.unscaledTime + 0.08f;
            }
            queuedAttackLastUpdate = attackPulse;
            InputSystem.QueueStateEvent(mobileKeyboard, new KeyboardState(keys.ToArray()));
        }
    }

    [DisallowMultipleComponent]
    public sealed class Metin2MobileJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] RectTransform background;
        [SerializeField] RectTransform handle;
        [SerializeField, Range(0f, 0.9f)] float deadZone = 0.12f;
        [SerializeField, Range(0.1f, 1f)] float handleTravel = 0.38f;

        public void Configure(RectTransform backgroundRect, RectTransform handleRect)
        {
            background = backgroundRect;
            handle = handleRect;
        }

        public void OnPointerDown(PointerEventData eventData) => OnDrag(eventData);

        public void OnDrag(PointerEventData eventData)
        {
            if (background == null || handle == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    background, eventData.position, eventData.pressEventCamera, out Vector2 local)) return;
            Vector2 halfSize = background.rect.size * 0.5f;
            float radius = Mathf.Max(1f, Mathf.Min(halfSize.x, halfSize.y));
            Vector2 normalized = Vector2.ClampMagnitude(local / radius, 1f);
            if (normalized.magnitude < deadZone) normalized = Vector2.zero;
            else normalized = normalized.normalized * Mathf.InverseLerp(deadZone, 1f, normalized.magnitude);
            handle.anchoredPosition = normalized * radius * handleTravel;
            Metin2MobileInputDriver.SetMovement(normalized);
        }

        public void OnPointerUp(PointerEventData eventData) => Release();
        void OnDisable() => Release();

        void Release()
        {
            if (handle != null) handle.anchoredPosition = Vector2.zero;
            Metin2MobileInputDriver.SetMovement(Vector2.zero);
        }
    }

    [DisallowMultipleComponent]
    public sealed class Metin2MobileActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public enum MobileAction { Attack, QuickSlot }

        [SerializeField] MobileAction action;
        [SerializeField, Range(0, 7)] int quickSlotIndex;
        [SerializeField] RawImage stateImage;
        [SerializeField] Rect normalUv = new Rect(0f, 0f, 1f, 1f);
        [SerializeField] Rect pressedUv = new Rect(0f, 0f, 1f, 1f);
        [SerializeField] Color normalColor = Color.white;
        [SerializeField] Color pressedColor = new Color(1f, 0.78f, 0.38f, 1f);

        public void Configure(MobileAction type, int slot, RawImage image, Rect normal, Rect pressed)
        {
            action = type;
            quickSlotIndex = slot;
            stateImage = image;
            normalUv = normal;
            pressedUv = pressed;
            ApplyPressed(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            ApplyPressed(true);
            if (action == MobileAction.Attack) Metin2MobileInputDriver.SetAttackHeld(true);
            else Metin2MobileInputDriver.ActivateSkill(quickSlotIndex);
        }

        public void OnPointerUp(PointerEventData eventData) => Release();
        public void OnPointerExit(PointerEventData eventData) { if (action == MobileAction.Attack) Release(); }
        void OnDisable() => Release();

        void Release()
        {
            if (action == MobileAction.Attack) Metin2MobileInputDriver.SetAttackHeld(false);
            ApplyPressed(false);
        }

        void ApplyPressed(bool pressed)
        {
            if (stateImage == null) return;
            stateImage.uvRect = pressed ? pressedUv : normalUv;
            stateImage.color = pressed ? pressedColor : normalColor;
        }
    }

    [DisallowMultipleComponent]
    public sealed class Metin2MobileCameraLookArea : MonoBehaviour, IDragHandler
    {
        [SerializeField, Min(0.01f)] float sensitivity = 1f;
        [SerializeField] float minimumPitch = -75f;
        [SerializeField] float maximumPitch = 75f;

        Metin2GameplayCamera cameraController;
        FieldInfo yawField;
        FieldInfo pitchField;
        FieldInfo rotationSpeedField;

        public void OnDrag(PointerEventData eventData)
        {
            ResolveCamera();
            if (cameraController == null || yawField == null || pitchField == null) return;
            float speed = rotationSpeedField != null ? (float)rotationSpeedField.GetValue(cameraController) : 0.15f;
            float yaw = (float)yawField.GetValue(cameraController) + eventData.delta.x * speed * sensitivity;
            float pitch = Mathf.Clamp((float)pitchField.GetValue(cameraController) - eventData.delta.y * speed * sensitivity,
                minimumPitch, maximumPitch);
            yawField.SetValue(cameraController, yaw);
            pitchField.SetValue(cameraController, pitch);
        }

        void ResolveCamera()
        {
            if (cameraController != null) return;
            cameraController = FindFirstObjectByType<Metin2GameplayCamera>();
            if (cameraController == null) return;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = typeof(Metin2GameplayCamera);
            yawField = type.GetField("yaw", flags);
            pitchField = type.GetField("pitch", flags);
            rotationSpeedField = type.GetField("rotationSpeed", flags);
        }
    }
}
