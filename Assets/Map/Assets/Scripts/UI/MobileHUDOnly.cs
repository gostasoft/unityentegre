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

/// <summary>Keeps the authored Canvas scaler settings with the MobileHUD prefab.</summary>
[DisallowMultipleComponent]
public sealed class MobileHUDCanvasProfile : MonoBehaviour
{
    [SerializeField] private int sortingOrder = 40000;
    [SerializeField] private CanvasScaler.ScaleMode scaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField] private CanvasScaler.ScreenMatchMode screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
    [SerializeField, Range(0f, 1f)] private float matchWidthOrHeight = 0.5f;
    [SerializeField] private float scaleFactor = 1f;
    [SerializeField] private float referencePixelsPerUnit = 100f;

    public void CaptureFrom(Canvas sourceCanvas)
    {
        if (sourceCanvas == null) return;
        sortingOrder = sourceCanvas.sortingOrder;
        CanvasScaler scaler = sourceCanvas.GetComponent<CanvasScaler>();
        if (scaler == null) return;
        scaleMode = scaler.uiScaleMode;
        referenceResolution = scaler.referenceResolution;
        screenMatchMode = scaler.screenMatchMode;
        matchWidthOrHeight = scaler.matchWidthOrHeight;
        scaleFactor = scaler.scaleFactor;
        referencePixelsPerUnit = scaler.referencePixelsPerUnit;
    }

    public void ApplyTo(Canvas targetCanvas)
    {
        if (targetCanvas == null) return;
        targetCanvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = targetCanvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = targetCanvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = scaleMode;
        scaler.referenceResolution = referenceResolution;
        scaler.screenMatchMode = screenMatchMode;
        scaler.matchWidthOrHeight = matchWidthOrHeight;
        scaler.scaleFactor = scaleFactor;
        scaler.referencePixelsPerUnit = referencePixelsPerUnit;
    }
}

/// <summary>
/// Configures the existing Canvas/MobileHUD hierarchy. It does not create or replace the HUD artwork.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class MobileHUDOnly : MonoBehaviour
{
    private const string AuthoredHudResource = "MobileHUD";

    [Header("Preview")]
    [SerializeField] private bool showInEditorPlayMode = true;
    [SerializeField] private bool showInDesktopBuild;

    [Header("Existing MobileHUD references (auto-resolved by name)")]
    [SerializeField] private MobileJoystick moveJoystick;
    [SerializeField] private MobileCameraLook cameraLookArea;
    [SerializeField] private Transform attackButton;
    [SerializeField] private Transform[] skillButtons = new Transform[8];

    [Header("Mobile menu shortcuts")]
    [SerializeField] private RectTransform mobileMenuButtons;
    [SerializeField] private Transform inventoryButton;
    [SerializeField] private Transform characterButton;
    [SerializeField] private Transform mapTestTeleportButton;
    [SerializeField] private Transform cameraViewButton;

    private MobileHUDInputBridge inputBridge;
    private float nextReferenceRefreshAt;

    public static bool IsAnyActive
    {
        get
        {
            MobileHUDOnly[] controllers = Resources.FindObjectsOfTypeAll<MobileHUDOnly>();
            foreach (MobileHUDOnly controller in controllers)
                if (controller != null && controller.gameObject.scene.IsValid()
                    && controller.GetComponentInParent<Canvas>(true) != null
                    && controller.gameObject.activeInHierarchy)
                    return true;
            return false;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void PreventDeviceSleep()
    {
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
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
        RemoveRuntimeDuplicateHuds();

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
            if (Application.isPlaying && candidate.GetComponentInParent<Canvas>(true) == null)
                continue;
            if (candidate.GetComponent<MobileHUDOnly>() == null)
                candidate.gameObject.AddComponent<MobileHUDOnly>().ApplyPlatformVisibilityAndConfigure();
        }

        EnsureAuthoredHudForActiveGameplayScene();
    }

    private static void RemoveRuntimeDuplicateHuds()
    {
        if (!Application.isPlaying)
            return;

        List<Transform> canvasHuds = new List<Transform>();
        foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (candidate == null || candidate.name != "MobileHUD" || !candidate.gameObject.scene.IsValid())
                continue;

            if (candidate.GetComponentInParent<Canvas>(true) == null)
            {
                candidate.gameObject.SetActive(false);
                Destroy(candidate.gameObject);
                continue;
            }
            canvasHuds.Add(candidate);
        }

        if (canvasHuds.Count <= 1)
            return;

        Transform keeper = canvasHuds.Find(candidate =>
            string.Equals(candidate.gameObject.scene.name, "Tapınak", StringComparison.OrdinalIgnoreCase));
        if (keeper == null)
            keeper = canvasHuds[0];

        foreach (Transform candidate in canvasHuds)
        {
            if (candidate == null || candidate == keeper)
                continue;
            candidate.gameObject.SetActive(false);
            Destroy(candidate.gameObject);
        }
    }

    private static void EnsureAuthoredHudForActiveGameplayScene()
    {
        if (!Application.isPlaying || IsAnyActive)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!IsGameplayScene(scene))
            return;

        GameObject hudPrefab = Resources.Load<GameObject>(AuthoredHudResource);
        if (hudPrefab == null)
        {
            Debug.LogError("[MobileHUD] Authored MobileHUD prefab is missing from Resources.");
            return;
        }

        GameObject canvasObject = new GameObject("Mobile Gameplay Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40000;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject hud = Instantiate(hudPrefab, canvasObject.transform, false);
        hud.name = "MobileHUD";
        hud.SetActive(true);
        MobileHUDCanvasProfile canvasProfile = hud.GetComponent<MobileHUDCanvasProfile>();
        if (canvasProfile != null) canvasProfile.ApplyTo(canvas);
        MobileHUDOnly controller = hud.GetComponent<MobileHUDOnly>();
        if (controller == null)
            controller = hud.AddComponent<MobileHUDOnly>();
        controller.ApplyPlatformVisibilityAndConfigure();
    }

    private static bool IsGameplayScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return false;
        string path = (scene.path ?? string.Empty).Replace('\\', '/');
        return path.IndexOf("/Metin2/Generated/Scenes/", StringComparison.OrdinalIgnoreCase) >= 0
            || path.IndexOf("/Map/Assets/Scenes/", StringComparison.OrdinalIgnoreCase) >= 0
            || scene.name.StartsWith("metin2_map", StringComparison.OrdinalIgnoreCase)
            || scene.name.StartsWith("map_", StringComparison.OrdinalIgnoreCase);
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
        if (attackButton == null)
            attackButton = ResolveTransform(null, "AttackButoon");

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
            Metin2Dev.Gameplay.Metin2QuickSlotView.EnsureMobile(skillButtons[index], index);
        }

        EnsureMobileMenuButtons();
        ConfigureAction(inventoryButton, MobileHUDActionButton.Action.Inventory, 0);
        ConfigureAction(characterButton, MobileHUDActionButton.Action.Character, 0);
        ConfigureAction(mapTestTeleportButton, MobileHUDActionButton.Action.MapTestTeleport, 0);
        ConfigureAction(cameraViewButton, MobileHUDActionButton.Action.CameraView, 0);

        MobileHUDStatusAndMinimap statusAndMinimap = GetComponent<MobileHUDStatusAndMinimap>();
        if (statusAndMinimap == null)
            statusAndMinimap = gameObject.AddComponent<MobileHUDStatusAndMinimap>();
        statusAndMinimap.EnsureHierarchy();

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
            if (Application.isPlaying)
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

    private void EnsureMobileMenuButtons()
    {
        if (mobileMenuButtons == null)
            mobileMenuButtons = ResolveTransform(null, "MobileMenuButtons") as RectTransform;

        Texture2D taskbar = Resources.Load<Texture2D>("Metin2UI/taskbar");
        if (taskbar == null)
        {
            Debug.LogWarning("[MobileHUD] Original taskbar texture is missing; menu shortcuts were not created.", this);
            return;
        }

        if (mobileMenuButtons == null)
        {
            GameObject menuObject = new GameObject("MobileMenuButtons", typeof(RectTransform));
            mobileMenuButtons = menuObject.GetComponent<RectTransform>();
            mobileMenuButtons.SetParent(transform, false);
            mobileMenuButtons.anchorMin = Vector2.one;
            mobileMenuButtons.anchorMax = Vector2.one;
            mobileMenuButtons.pivot = Vector2.one;
            mobileMenuButtons.anchoredPosition = new Vector2(-18f, -18f);
            mobileMenuButtons.sizeDelta = new Vector2(192f, 48f);
        }
        else
        {
            mobileMenuButtons.sizeDelta = new Vector2(Mathf.Max(192f, mobileMenuButtons.sizeDelta.x),
                Mathf.Max(48f, mobileMenuButtons.sizeDelta.y));
        }

        inventoryButton = EnsureMenuButton(inventoryButton, "InventoryButton", taskbar,
            new Rect(455f, 0f, 32f, 32f), new Vector2(-24f, -24f));
        characterButton = EnsureMenuButton(characterButton, "CharacterButton", taskbar,
            new Rect(263f, 0f, 32f, 32f), new Vector2(-72f, -24f));
        mapTestTeleportButton = EnsureMenuButton(mapTestTeleportButton, "MapTestTeleportButton", taskbar,
            new Rect(320f, 127f, 32f, 32f), new Vector2(-120f, -24f));
        // Original Metin2 mouse_button_camera_01.sub: TaskBar.tga, 424/87 - 456/119.
        cameraViewButton = EnsureMenuButton(cameraViewButton, "CameraViewButton", taskbar,
            new Rect(424f, 87f, 32f, 32f), new Vector2(-168f, -24f));
    }

    private Transform EnsureMenuButton(Transform current, string buttonName, Texture2D atlas,
        Rect sourcePixels, Vector2 position)
    {
        Transform target = current != null ? current : FindDescendant(mobileMenuButtons, buttonName);
        if (target == null)
        {
            GameObject buttonObject = new GameObject(buttonName, typeof(RectTransform), typeof(RawImage));
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(mobileMenuButtons, false);
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(42f, 42f);
            target = rect;
        }

        RawImage icon = target.GetComponent<RawImage>();
        if (icon == null)
            icon = target.gameObject.AddComponent<RawImage>();
        icon.texture = atlas;
        icon.uvRect = AtlasUv(atlas, sourcePixels);
        icon.raycastTarget = true;

        Button button = target.GetComponent<Button>();
        if (button == null)
            button = target.gameObject.AddComponent<Button>();
        button.targetGraphic = icon;
        button.transition = Selectable.Transition.ColorTint;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        return target;
    }

    private static Rect AtlasUv(Texture2D texture, Rect topLeftPixels)
    {
        return new Rect(topLeftPixels.x / texture.width,
            1f - (topLeftPixels.y + topLeftPixels.height) / texture.height,
            topLeftPixels.width / texture.width,
            topLeftPixels.height / texture.height);
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
public sealed class MobileHUDActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
    IPointerExitHandler, IDragHandler
{
    public enum Action { Attack, QuickSlot, Inventory, Character, MapTestTeleport, CameraView }

    [SerializeField] private Action action;
    [SerializeField, Range(0, 7)] private int quickSlot;
    private MobileHUDInputBridge inputBridge;
    private RawImage cameraIcon;
    private Rect cameraNormalUv;
    private Rect cameraPressedUv;
    private Vector2 cameraPointerDownPosition;
    private bool cameraGestureActive;
    private bool cameraWasDragged;
    private bool quickSlotGestureActive;
    private bool quickSlotWasDragged;

    public void Configure(MobileHUDInputBridge bridge, Action configuredAction, int slot)
    {
        inputBridge = bridge;
        action = configuredAction;
        quickSlot = Mathf.Clamp(slot, 0, 7);
        if (action == Action.CameraView)
        {
            cameraIcon = GetComponent<RawImage>();
            if (cameraIcon != null && cameraIcon.texture != null)
            {
                cameraNormalUv = cameraIcon.uvRect;
                Texture texture = cameraIcon.texture;
                cameraPressedUv = new Rect(0f,
                    1f - 159f / texture.height,
                    32f / texture.width,
                    32f / texture.height);
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (inputBridge == null)
            inputBridge = GetComponentInParent<MobileHUDInputBridge>();
        switch (action)
        {
            case Action.Attack:
                inputBridge?.SetAttackHeld(true);
                break;
            case Action.QuickSlot:
                quickSlotGestureActive = true;
                quickSlotWasDragged = false;
                break;
            case Action.Inventory:
                inputBridge?.ActivateMenu(true);
                break;
            case Action.Character:
                inputBridge?.ActivateMenu(false);
                break;
            case Action.MapTestTeleport:
                MobileMapTestTeleporter.TeleportNext();
                break;
            case Action.CameraView:
                cameraGestureActive = true;
                cameraWasDragged = false;
                cameraPointerDownPosition = eventData.position;
                SetCameraPressedVisual(true);
                break;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (action == Action.QuickSlot && quickSlotGestureActive)
        {
            quickSlotWasDragged = true;
            return;
        }
        if (action != Action.CameraView || !cameraGestureActive)
            return;
        if (!cameraWasDragged && Mathf.Abs(eventData.position.y - cameraPointerDownPosition.y) < 8f)
            return;
        cameraWasDragged = true;
        inputBridge?.AdjustCameraZoom(eventData.delta.y);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (action == Action.QuickSlot)
        {
            if (quickSlotGestureActive && !quickSlotWasDragged)
                inputBridge?.ActivateQuickSlot(quickSlot);
            quickSlotGestureActive = false;
            return;
        }
        if (action == Action.CameraView)
        {
            SetCameraPressedVisual(false);
            if (cameraGestureActive && !cameraWasDragged)
                inputBridge?.ToggleCameraView();
            cameraGestureActive = false;
            return;
        }
        if (action == Action.Attack)
            inputBridge?.SetAttackHeld(false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (action == Action.CameraView)
            SetCameraPressedVisual(false);
        if (action == Action.Attack)
            inputBridge?.SetAttackHeld(false);
    }

    private void SetCameraPressedVisual(bool pressed)
    {
        if (cameraIcon != null)
            cameraIcon.uvRect = pressed ? cameraPressedUv : cameraNormalUv;
    }

    private void OnDisable()
    {
        cameraGestureActive = false;
        cameraWasDragged = false;
        quickSlotGestureActive = false;
        quickSlotWasDragged = false;
        SetCameraPressedVisual(false);
        if (action == Action.Attack)
            inputBridge?.SetAttackHeld(false);
    }
}

/// <summary>
/// Temporary mobile-only map test helper. It prefers authored SpawnPoints in the active
/// hierarchy and otherwise advances to the next gameplay map included in Build Settings.
/// </summary>
public static class MobileMapTestTeleporter
{
    private const string SpawnRootName = "SpawnPoints";
    private static bool sceneLoadInProgress;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        sceneLoadInProgress = false;
    }

    public static void TeleportNext()
    {
        if (!Application.isPlaying || sceneLoadInProgress)
            return;

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
            return;

        if (TryTeleportToAuthoredSpawn(activeScene))
            return;

        List<int> mapBuildIndices = FindGameplayMapBuildIndices();
        if (mapBuildIndices.Count == 0)
        {
            Debug.LogWarning("[MobileHUD] No gameplay map scene is enabled in Build Settings.");
            return;
        }

        int currentListIndex = mapBuildIndices.IndexOf(activeScene.buildIndex);
        int nextListIndex = currentListIndex >= 0 ? (currentListIndex + 1) % mapBuildIndices.Count : 0;
        int nextBuildIndex = mapBuildIndices[nextListIndex];
        if (nextBuildIndex == activeScene.buildIndex && mapBuildIndices.Count == 1)
        {
            Debug.LogWarning("[MobileHUD] Only the active gameplay map is enabled in Build Settings.");
            return;
        }

        string nextPath = SceneUtility.GetScenePathByBuildIndex(nextBuildIndex);
        sceneLoadInProgress = true;
        AsyncOperation operation = SceneManager.LoadSceneAsync(nextBuildIndex, LoadSceneMode.Single);
        if (operation == null)
        {
            sceneLoadInProgress = false;
            Debug.LogWarning("[MobileHUD] Could not load test map: " + nextPath);
            return;
        }
        operation.completed += _ => sceneLoadInProgress = false;
        Debug.Log("[MobileHUD] Loading test map: " + nextPath);
    }

    private static bool TryTeleportToAuthoredSpawn(Scene scene)
    {
        Transform player = FindPlayer(scene);
        if (player == null)
            return false;

        List<Transform> spawns = new List<Transform>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform spawnRoot = FindDeep(root.transform, SpawnRootName);
            if (spawnRoot == null)
                continue;
            CollectSpawnChildren(spawnRoot, spawns);
        }
        if (spawns.Count == 0)
            return false;

        int nearestIndex = 0;
        float nearestDistance = float.PositiveInfinity;
        for (int index = 0; index < spawns.Count; index++)
        {
            float distance = (spawns[index].position - player.position).sqrMagnitude;
            if (distance >= nearestDistance)
                continue;
            nearestDistance = distance;
            nearestIndex = index;
        }

        int destinationIndex = spawns.Count > 1 ? (nearestIndex + 1) % spawns.Count : 0;
        if (spawns.Count == 1 && nearestDistance < 16f)
            return false;

        Vector3 destination = Ground(spawns[destinationIndex].position);
        CharacterController controller = player.GetComponent<CharacterController>();
        bool wasEnabled = controller != null && controller.enabled;
        if (controller != null)
            controller.enabled = false;
        player.SetPositionAndRotation(destination, spawns[destinationIndex].rotation);
        if (controller != null)
            controller.enabled = wasEnabled;
        Physics.SyncTransforms();
        Debug.Log("[MobileHUD] Teleported to hierarchy spawn: " + spawns[destinationIndex].name);
        return true;
    }

    private static List<int> FindGameplayMapBuildIndices()
    {
        List<int> result = new List<int>();
        for (int buildIndex = 0; buildIndex < SceneManager.sceneCountInBuildSettings; buildIndex++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(buildIndex).Replace('\\', '/');
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            bool generatedMap = path.IndexOf("/Metin2/Generated/Scenes/", StringComparison.OrdinalIgnoreCase) >= 0;
            bool mapScene = name.StartsWith("metin2_map", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("map_", StringComparison.OrdinalIgnoreCase);
            if (generatedMap || mapScene)
                result.Add(buildIndex);
        }
        return result;
    }

    private static Transform FindPlayer(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name.StartsWith("Player -", StringComparison.Ordinal))
                return root.transform;

        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        foreach (MonoBehaviour behaviour in behaviours)
            if (behaviour != null && behaviour.gameObject.scene == scene
                && (behaviour.GetType().Name == "Metin2PlayerController" || behaviour.GetType().Name == "PlayerMovement"))
                return behaviour.transform;
        return null;
    }

    private static void CollectSpawnChildren(Transform root, List<Transform> result)
    {
        for (int index = 0; index < root.childCount; index++)
        {
            Transform child = root.GetChild(index);
            result.Add(child);
            CollectSpawnChildren(child, result);
        }
    }

    private static Transform FindDeep(Transform root, string objectName)
    {
        if (string.Equals(root.name, objectName, StringComparison.OrdinalIgnoreCase))
            return root;
        for (int index = 0; index < root.childCount; index++)
        {
            Transform found = FindDeep(root.GetChild(index), objectName);
            if (found != null)
                return found;
        }
        return null;
    }

    private static Vector3 Ground(Vector3 point)
    {
        if (Physics.Raycast(point + Vector3.up * 5000f, Vector3.down, out RaycastHit hit, 10000f, ~0,
                QueryTriggerInteraction.Ignore))
            return hit.point + Vector3.up * 0.1f;
        return point;
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
    private Key pendingMenuKey = Key.None;
    private bool attackWasQueued;
    private float nextAttackPulse;
    private MonoBehaviour gameplayCamera;
    private FieldInfo cameraYawField;
    private FieldInfo cameraPitchField;
    private FieldInfo cameraRotationSpeedField;
    private MethodInfo cameraToggleViewMethod;
    private MethodInfo cameraAdjustZoomMethod;

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

    public void ActivateMenu(bool inventory)
    {
        if (TryInvokeGameplayMenu(inventory ? "ToggleInventory" : "ToggleCharacter"))
            return;
        pendingMenuKey = inventory ? Key.I : Key.C;
    }

    public void ToggleCameraView()
    {
        ResolveGameplayCamera();
        if (gameplayCamera != null && cameraToggleViewMethod != null)
            cameraToggleViewMethod.Invoke(gameplayCamera, null);
    }

    public void AdjustCameraZoom(float verticalDragDelta)
    {
        ResolveGameplayCamera();
        if (gameplayCamera != null && cameraAdjustZoomMethod != null)
            cameraAdjustZoomMethod.Invoke(gameplayCamera, new object[] { verticalDragDelta });
    }

    private static bool TryInvokeGameplayMenu(string methodName)
    {
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null || behaviour.GetType().FullName != "Metin2Dev.Gameplay.Metin2GameplayUI")
                continue;
            MethodInfo method = behaviour.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null);
            if (method == null)
                continue;
            method.Invoke(behaviour, null);
            return true;
        }
        return false;
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
        pendingMenuKey = Key.None;
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

        Key menuKey = pendingMenuKey;
        pendingMenuKey = Key.None;
        if (menuKey != Key.None)
            pressedKeys.Add(menuKey);

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
            cameraToggleViewMethod = type.GetMethod("ToggleView", flags, null, Type.EmptyTypes, null);
            cameraAdjustZoomMethod = type.GetMethod("AdjustThirdPersonDistance", flags, null,
                new[] { typeof(float) }, null);
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
