using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Configures the existing Canvas/MobileHUD hierarchy. It does not create or replace the HUD artwork.
/// </summary>
[DisallowMultipleComponent]
public sealed class MobileHUDOnly : MonoBehaviour
{
    [Header("Preview")]
    [SerializeField] private bool showInEditorPlayMode = true;
    [SerializeField] private bool showInDesktopBuild;

    [Header("Existing MobileHUD references (auto-resolved by name)")]
    [SerializeField] private MobileJoystick moveJoystick;
    [SerializeField] private MobileCameraLook cameraLookArea;
    [SerializeField] private Transform attackButton;
    [SerializeField] private Transform[] skillButtons = new Transform[8];

    private MobileHUDInputBridge inputBridge;
    private float nextReferenceRefreshAt;

    public static bool IsAnyActive
    {
        get
        {
            MobileHUDOnly[] controllers = Resources.FindObjectsOfTypeAll<MobileHUDOnly>();
            foreach (MobileHUDOnly controller in controllers)
                if (controller != null && controller.gameObject.scene.IsValid() && controller.gameObject.activeInHierarchy)
                    return true;
            return false;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallForLoadedScenes()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ConfigureEveryMobileHud();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ConfigureEveryMobileHud();
    }

    private static void ConfigureEveryMobileHud()
    {
        MobileHUDOnly[] controllers = Resources.FindObjectsOfTypeAll<MobileHUDOnly>();
        foreach (MobileHUDOnly controller in controllers)
        {
            if (controller != null && controller.gameObject.scene.IsValid())
                controller.ApplyPlatformVisibilityAndConfigure();
        }

        // Older scenes contain the authored hierarchy but not always this controller.
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (Transform candidate in transforms)
        {
            if (candidate == null || candidate.name != "MobileHUD" || !candidate.gameObject.scene.IsValid())
                continue;
            if (candidate.GetComponent<MobileHUDOnly>() == null)
                candidate.gameObject.AddComponent<MobileHUDOnly>().ApplyPlatformVisibilityAndConfigure();
        }
    }

    private void Awake()
    {
        ApplyPlatformVisibilityAndConfigure();
    }

    private void OnEnable()
    {
        ConfigureExistingHierarchy();
    }

    private void Update()
    {
        if (!Application.isPlaying || Time.unscaledTime < nextReferenceRefreshAt)
            return;
        nextReferenceRefreshAt = Time.unscaledTime + 1f;
        WireLegacyPlayerReferences();
    }

    private void ApplyPlatformVisibilityAndConfigure()
    {
        bool shouldShow = ShouldShowHud();
        if (gameObject.activeSelf != shouldShow)
            gameObject.SetActive(shouldShow);
        if (shouldShow)
            ConfigureExistingHierarchy();
    }

    private bool ShouldShowHud()
    {
#if UNITY_EDITOR
        return showInEditorPlayMode;
#elif UNITY_ANDROID || UNITY_IOS
        return true;
#else
        return showInDesktopBuild;
#endif
    }

    private void ConfigureExistingHierarchy()
    {
        if (!gameObject.activeInHierarchy)
            return;

        EnsureCanvasCanReceivePointers();
        EnsureInputSystemEventSystem();

        moveJoystick = ResolveComponent(moveJoystick, "MoveJoystick");
        cameraLookArea = ResolveComponent(cameraLookArea, "CameraLookArea");
        attackButton = ResolveTransform(attackButton, "AttackButton");

        if (inputBridge == null)
            inputBridge = GetComponent<MobileHUDInputBridge>();
        if (inputBridge == null)
            inputBridge = gameObject.AddComponent<MobileHUDInputBridge>();
        inputBridge.Configure(moveJoystick, cameraLookArea);

        ConfigureAction(attackButton, MobileHUDActionButton.Action.Attack, 0);
        if (skillButtons == null || skillButtons.Length != 8)
            skillButtons = new Transform[8];
        for (int index = 0; index < skillButtons.Length; index++)
        {
            skillButtons[index] = ResolveTransform(skillButtons[index], "Skill" + (index + 1));
            ConfigureAction(skillButtons[index], MobileHUDActionButton.Action.QuickSlot, index);
        }

        WireLegacyPlayerReferences();
        DisableGeneratedReplacementHud();
    }

    private void EnsureCanvasCanReceivePointers()
    {
        Canvas canvas = GetComponentInParent<Canvas>(true);
        if (canvas == null)
            return;
        GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
            raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
        raycaster.enabled = true;

        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.interactable = true;
            group.blocksRaycasts = true;
        }
    }

    private static void EnsureInputSystemEventSystem()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            EventSystem[] systems = Resources.FindObjectsOfTypeAll<EventSystem>();
            foreach (EventSystem candidate in systems)
            {
                if (candidate != null && candidate.gameObject.scene.IsValid())
                {
                    eventSystem = candidate;
                    break;
                }
            }
        }

        if (eventSystem == null)
        {
            GameObject eventObject = new GameObject("EventSystem", typeof(EventSystem));
            UnityEngine.Object.DontDestroyOnLoad(eventObject);
            eventSystem = eventObject.GetComponent<EventSystem>();
        }

        eventSystem.gameObject.SetActive(true);
        eventSystem.enabled = true;

        StandaloneInputModule oldModule = eventSystem.GetComponent<StandaloneInputModule>();
        if (oldModule != null)
            oldModule.enabled = false;

        InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (inputModule == null)
            inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        inputModule.enabled = true;
        if (inputModule.actionsAsset == null)
            inputModule.AssignDefaultActions();
    }

    private T ResolveComponent<T>(T current, string objectName) where T : Component
    {
        if (current != null)
            return current;
        Transform target = FindDescendant(transform, objectName);
        if (target == null)
            return null;
        T component = target.GetComponent<T>();
        return component != null ? component : target.gameObject.AddComponent<T>();
    }

    private Transform ResolveTransform(Transform current, string objectName)
    {
        return current != null ? current : FindDescendant(transform, objectName);
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        if (root == null)
            return null;
        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform candidate in descendants)
            if (candidate != null && string.Equals(candidate.name, objectName, StringComparison.OrdinalIgnoreCase))
                return candidate;
        return null;
    }

    private void ConfigureAction(Transform target, MobileHUDActionButton.Action action, int quickSlot)
    {
        if (target == null)
            return;

        Graphic hitGraphic = target.GetComponent<Graphic>();
        if (hitGraphic == null)
        {
            Image transparentHitArea = target.gameObject.AddComponent<Image>();
            transparentHitArea.color = new Color(1f, 1f, 1f, 0.001f);
            hitGraphic = transparentHitArea;
        }
        hitGraphic.raycastTarget = true;

        MobileHUDActionButton button = target.GetComponent<MobileHUDActionButton>();
        if (button == null)
            button = target.gameObject.AddComponent<MobileHUDActionButton>();
        button.Configure(inputBridge, action, quickSlot);
    }

    private void WireLegacyPlayerReferences()
    {
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null || behaviour.GetType().Name != "PlayerMovement")
                continue;
            SetCompatibleField(behaviour, "moveJoystick", moveJoystick);
            SetCompatibleField(behaviour, "mobileCameraLook", cameraLookArea);
        }
    }

    private static void SetCompatibleField(object target, string fieldName, Component value)
    {
        if (target == null || value == null)
            return;
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && field.FieldType.IsInstanceOfType(value))
            field.SetValue(target, value);
    }

    private void DisableGeneratedReplacementHud()
    {
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null || behaviour.transform.IsChildOf(transform))
                continue;
            if (behaviour.GetType().FullName == "Metin2Dev.Gameplay.Metin2MobileGameplayUI")
                behaviour.gameObject.SetActive(false);
        }
    }
}

[DisallowMultipleComponent]
public sealed class MobileHUDActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public enum Action { Attack, QuickSlot }

    [SerializeField] private Action action;
    [SerializeField, Range(0, 7)] private int quickSlot;
    private MobileHUDInputBridge inputBridge;

    public void Configure(MobileHUDInputBridge bridge, Action configuredAction, int slot)
    {
        inputBridge = bridge;
        action = configuredAction;
        quickSlot = Mathf.Clamp(slot, 0, 7);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (inputBridge == null)
            inputBridge = GetComponentInParent<MobileHUDInputBridge>();
        if (action == Action.Attack)
            inputBridge?.SetAttackHeld(true);
        else
            inputBridge?.ActivateQuickSlot(quickSlot);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (action == Action.Attack)
            inputBridge?.SetAttackHeld(false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (action == Action.Attack)
            inputBridge?.SetAttackHeld(false);
    }

    private void OnDisable()
    {
        if (action == Action.Attack)
            inputBridge?.SetAttackHeld(false);
    }
}

[DisallowMultipleComponent]
public sealed class MobileHUDInputBridge : MonoBehaviour
{
    private static MobileHUDInputBridge activeBridge;

    private MobileJoystick moveJoystick;
    private MobileCameraLook cameraLook;
    private Keyboard mobileKeyboard;
    private bool attackHeld;
    private int pendingQuickSlot = -1;
    private bool attackWasQueued;
    private float nextAttackPulse;
    private MonoBehaviour gameplayCamera;
    private FieldInfo cameraYawField;
    private FieldInfo cameraPitchField;
    private FieldInfo cameraRotationSpeedField;

    public void Configure(MobileJoystick joystick, MobileCameraLook lookArea)
    {
        moveJoystick = joystick;
        cameraLook = lookArea;
    }

    public void SetAttackHeld(bool held)
    {
        if (held && !attackHeld)
        {
            nextAttackPulse = 0f;
            attackWasQueued = false;
        }
        attackHeld = held;
    }

    public void ActivateQuickSlot(int index)
    {
        pendingQuickSlot = Mathf.Clamp(index, 0, 7);
    }

    private void OnEnable()
    {
        if (!Application.isPlaying || activeBridge != null && activeBridge != this)
            return;
        activeBridge = this;
        mobileKeyboard = InputSystem.AddDevice<Keyboard>("Existing MobileHUD Controls");
        InputSystem.onBeforeUpdate += QueueInputState;
    }

    private void OnDisable()
    {
        InputSystem.onBeforeUpdate -= QueueInputState;
        if (mobileKeyboard != null && mobileKeyboard.added)
            InputSystem.RemoveDevice(mobileKeyboard);
        mobileKeyboard = null;
        attackHeld = false;
        pendingQuickSlot = -1;
        if (activeBridge == this)
            activeBridge = null;
    }

    private void QueueInputState()
    {
        if (mobileKeyboard == null || !mobileKeyboard.added)
            return;

        List<Key> pressedKeys = new List<Key>(7);
        Vector2 direction = moveJoystick != null ? moveJoystick.Direction : Vector2.zero;
        const float deadZone = 0.12f;
        if (direction.y > deadZone) pressedKeys.Add(Key.W);
        if (direction.y < -deadZone) pressedKeys.Add(Key.S);
        if (direction.x > deadZone) pressedKeys.Add(Key.D);
        if (direction.x < -deadZone) pressedKeys.Add(Key.A);
        if (direction.sqrMagnitude > 0.52f) pressedKeys.Add(Key.LeftShift);

        int quickSlot = pendingQuickSlot;
        pendingQuickSlot = -1;
        Key quickSlotKey = QuickSlotKey(quickSlot);
        if (quickSlotKey != Key.None)
            pressedKeys.Add(quickSlotKey);

        bool attackPulse = attackHeld && !attackWasQueued && Time.unscaledTime >= nextAttackPulse;
        if (attackPulse)
        {
            pressedKeys.Add(Key.Space);
            nextAttackPulse = Time.unscaledTime + 0.08f;
        }
        attackWasQueued = attackPulse;

        InputSystem.QueueStateEvent(mobileKeyboard, new KeyboardState(pressedKeys.ToArray()));
    }

    private void LateUpdate()
    {
        if (cameraLook == null || cameraLook.LookDelta.sqrMagnitude < 0.0001f)
            return;
        ResolveGameplayCamera();
        if (gameplayCamera == null || cameraYawField == null || cameraPitchField == null)
            return;

        float speed = cameraRotationSpeedField != null
            ? (float)cameraRotationSpeedField.GetValue(gameplayCamera)
            : 0.18f;
        Vector2 delta = cameraLook.LookDelta;
        float yaw = (float)cameraYawField.GetValue(gameplayCamera) + delta.x * speed;
        float pitch = Mathf.Clamp((float)cameraPitchField.GetValue(gameplayCamera) - delta.y * speed, -75f, 75f);
        cameraYawField.SetValue(gameplayCamera, yaw);
        cameraPitchField.SetValue(gameplayCamera, pitch);
    }

    private void ResolveGameplayCamera()
    {
        if (gameplayCamera != null)
            return;
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null || behaviour.GetType().FullName != "Metin2Dev.Gameplay.Metin2GameplayCamera")
                continue;
            gameplayCamera = behaviour;
            Type type = behaviour.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            cameraYawField = type.GetField("yaw", flags);
            cameraPitchField = type.GetField("pitch", flags);
            cameraRotationSpeedField = type.GetField("rotationSpeed", flags);
            break;
        }
    }

    private static Key QuickSlotKey(int index)
    {
        switch (index)
        {
            case 0: return Key.Digit1;
            case 1: return Key.Digit2;
            case 2: return Key.Digit3;
            case 3: return Key.Digit4;
            case 4: return Key.F1;
            case 5: return Key.F2;
            case 6: return Key.F3;
            case 7: return Key.F4;
            default: return Key.None;
        }
    }
}
