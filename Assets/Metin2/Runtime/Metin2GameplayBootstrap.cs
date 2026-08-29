using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Metin2Dev.Gameplay
{
    public sealed class Metin2GameplayBootstrap : MonoBehaviour
    {
        const string DatabaseResource = "Metin2GameplayDatabase";
        // Converted directly from Extracted/item/ymir work/item/weapon/00010.gr2.
        const string TestSwordResource = "Metin2Sword00010";
        const int LocalPlayerLayer = 8;
        static Metin2GameplayBootstrap instance;
        bool upgradeInProgress;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (instance != null) return;
            GameObject host = new GameObject("Metin2 Gameplay Bootstrap");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<Metin2GameplayBootstrap>();
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isLoaded && activeScene.name != "Metin2_Intro" && !HasPlayer(activeScene))
                StartCoroutine(SpawnWhenReady(activeScene));
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void Update()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (upgradeInProgress || !activeScene.IsValid() || !activeScene.isLoaded || activeScene.name == "Metin2_Intro") return;
            if (FindLoadingPlayer(activeScene) != null) StartCoroutine(UpgradeLoadingPlayer(activeScene));
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Metin2_Intro" || HasPlayer(scene)) return;
            StartCoroutine(SpawnWhenReady(scene));
        }

        IEnumerator SpawnWhenReady(Scene scene)
        {
            yield return null;
            Metin2GameplayDatabase database = Resources.Load<Metin2GameplayDatabase>(DatabaseResource);
            Metin2GameplaySession.UseEditorDefault();
            if (database == null)
            {
                Debug.LogWarning("Metin2 gameplay data is still building. Spawning the selected character model until its motions are ready.");
                SpawnSelectedCharacterModel(scene);
                yield break;
            }
            Metin2RaceMotionSet set = database.Find(Metin2GameplaySession.CharacterClass, Metin2GameplaySession.Gender);
            if (set == null || set.playerPrefab == null || set.animatorController == null)
            {
                Debug.LogWarning("Metin2 gameplay race data is incomplete. Spawning the selected character model until its motions are ready.");
                SpawnSelectedCharacterModel(scene);
                yield break;
            }

            Vector3 spawn = FindSpawnPosition(scene);
            SpawnPlayer(scene, set, spawn);
        }

        static void SpawnPlayer(Scene scene, Metin2RaceMotionSet set, Vector3 spawn)
        {
            GameObject player = new GameObject("Player - " + Metin2GameplaySession.CharacterName);
            player.layer = LocalPlayerLayer;
            player.transform.position = spawn;
            player.transform.localScale = Vector3.one * 2f;
            SceneManager.MoveGameObjectToScene(player, scene);
            CharacterController capsule = player.AddComponent<CharacterController>();

            GameObject visual = Instantiate(set.playerPrefab, player.transform);
            visual.name = "Character Visual";
            visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            SetLayerRecursively(visual.transform, LocalPlayerLayer);
            GameObject hair = AttachSelectedHair(visual.transform);
            ConfigureCharacterAppearance(visual, hair != null ? hair.transform : null);
            foreach (Camera nestedCamera in visual.GetComponentsInChildren<Camera>(true)) nestedCamera.enabled = false;
            foreach (Light nestedLight in visual.GetComponentsInChildren<Light>(true)) nestedLight.enabled = false;
            Animator animator = visual.GetComponentInChildren<Animator>(true);
            if (animator == null) animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = set.animatorController;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            AttachTestSword(visual.transform);

            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                camera = cameraObject.GetComponent<Camera>();
            }
            Metin2GameplayCamera cameraController = camera.GetComponent<Metin2GameplayCamera>();
            if (cameraController == null) cameraController = camera.gameObject.AddComponent<Metin2GameplayCamera>();
            camera.cullingMask |= 1 << LocalPlayerLayer;
            cameraController.SetFirstPersonHiddenRenderers(hair != null ? hair.GetComponentsInChildren<Renderer>(true) : new Renderer[0]);
            cameraController.Follow(player.transform, FindDeep(visual.transform, "Bip01 Head"));

            Metin2PlayerController controller = player.AddComponent<Metin2PlayerController>();
            controller.Initialize(set, animator, camera, Metin2GameplaySession.CharacterName);
            player.AddComponent<Metin2PlayerState>();
            player.AddComponent<Metin2GameplayOverlay>();
            capsule.enabled = true;
        }

        IEnumerator UpgradeLoadingPlayer(Scene scene)
        {
            upgradeInProgress = true;
            yield return null;
            Metin2GameplayDatabase database = Resources.Load<Metin2GameplayDatabase>(DatabaseResource);
            Metin2RaceMotionSet set = database != null
                ? database.Find(Metin2GameplaySession.CharacterClass, Metin2GameplaySession.Gender)
                : null;
            GameObject loadingPlayer = FindLoadingPlayer(scene);
            if (set != null && set.playerPrefab != null && set.animatorController != null && loadingPlayer != null)
            {
                Vector3 spawn = loadingPlayer.transform.position;
                Destroy(loadingPlayer);
                SpawnPlayer(scene, set, spawn);
            }
            upgradeInProgress = false;
        }

        static void SpawnSelectedCharacterModel(Scene scene)
        {
            GameObject source = Metin2GameplaySession.PlayerPrefab;
            if (source == null)
            {
                Debug.LogError("No character was selected. Enter the map through the character selection screen.");
                return;
            }
            GameObject player = new GameObject("Player - " + Metin2GameplaySession.CharacterName + " (Loading)");
            player.layer = LocalPlayerLayer;
            player.transform.position = FindSpawnPosition(scene);
            player.transform.localScale = Vector3.one * 2f;
            SceneManager.MoveGameObjectToScene(player, scene);
            player.AddComponent<CharacterController>();

            GameObject visual = Instantiate(source, player.transform);
            visual.name = "Character Visual";
            visual.transform.SetParent(player.transform, false);
            SetLayerRecursively(visual.transform, LocalPlayerLayer);
            GameObject hair = AttachSelectedHair(visual.transform);
            ConfigureCharacterAppearance(visual, hair != null ? hair.transform : null);
            foreach (Camera nestedCamera in visual.GetComponentsInChildren<Camera>(true)) nestedCamera.enabled = false;
            foreach (Light nestedLight in visual.GetComponentsInChildren<Light>(true)) nestedLight.enabled = false;
            AttachTestSword(visual.transform);

            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                camera = cameraObject.GetComponent<Camera>();
            }
            Metin2GameplayCamera cameraController = camera.GetComponent<Metin2GameplayCamera>();
            if (cameraController == null) cameraController = camera.gameObject.AddComponent<Metin2GameplayCamera>();
            camera.cullingMask |= 1 << LocalPlayerLayer;
            cameraController.SetFirstPersonHiddenRenderers(hair != null ? hair.GetComponentsInChildren<Renderer>(true) : new Renderer[0]);
            cameraController.Follow(player.transform, FindDeep(visual.transform, "Bip01 Head"));
        }

        static bool HasPlayer(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name.StartsWith("Player -", System.StringComparison.Ordinal)) return true;
            return false;
        }

        static GameObject FindLoadingPlayer(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name.StartsWith("Player -", System.StringComparison.Ordinal) &&
                    root.name.EndsWith("(Loading)", System.StringComparison.Ordinal)) return root;
            return null;
        }

        static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int index = 0; index < root.childCount; index++)
                SetLayerRecursively(root.GetChild(index), layer);
        }

        static void AttachTestSword(Transform character)
        {
            // `equip_right_hand` is an effect socket used by skill MSA files.  The actual weapon must
            // follow the deforming hand bone, otherwise skill clips rotate it independently and upside-down.
            Transform rightHand = FindDeep(character, "Bip01 R Hand") ?? FindDeep(character, "equip_right_hand");
            GameObject swordPrefab = Resources.Load<GameObject>(TestSwordResource);
            if (rightHand == null || swordPrefab == null)
            {
                Debug.LogWarning("Metin2 test sword could not be attached: right hand or converted sword asset is missing.");
                return;
            }

            GameObject sword = Instantiate(swordPrefab, rightHand, false);
            sword.name = "Weapon - 00010 (Test Sword)";
            Metin2SwordAttachmentSettings settings = Resources.Load<Metin2SwordAttachmentSettings>("Metin2SwordAttachmentSettings");
            Vector3 position = settings != null ? settings.LocalPosition : Vector3.zero;
            Vector3 rotation = settings != null ? settings.LocalEulerAngles : new Vector3(0f, 0f, 90f);
            Vector3 scale = settings != null ? settings.localScale : Vector3.one;
            sword.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(rotation));
            sword.transform.localScale = scale;
            Metin2SwordAttachmentFollower follower = sword.AddComponent<Metin2SwordAttachmentFollower>();
            follower.settings = settings;
            sword.transform.localScale = Vector3.one;
            SetLayerRecursively(sword.transform, LocalPlayerLayer);
            foreach (Camera nestedCamera in sword.GetComponentsInChildren<Camera>(true)) nestedCamera.enabled = false;
            foreach (Light nestedLight in sword.GetComponentsInChildren<Light>(true)) nestedLight.enabled = false;
        }

        static GameObject AttachSelectedHair(Transform character)
        {
            GameObject hairPrefab = Metin2GameplaySession.HairPrefab;
            if (hairPrefab == null) return null;
            GameObject hair = Instantiate(hairPrefab, character, false);
            hair.name = "Character Hair";
            hair.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            hair.transform.localScale = Vector3.one;
            SetLayerRecursively(hair.transform, LocalPlayerLayer);
            Dictionary<string, Transform> characterBones = new Dictionary<string, Transform>();
            foreach (Transform bone in character.GetComponentsInChildren<Transform>(true))
                if (!characterBones.ContainsKey(bone.name)) characterBones.Add(bone.name, bone);
            foreach (SkinnedMeshRenderer renderer in hair.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Transform[] sourceBones = renderer.bones;
                Transform[] reboundBones = new Transform[sourceBones.Length];
                for (int index = 0; index < sourceBones.Length; index++)
                    reboundBones[index] = sourceBones[index] != null && characterBones.TryGetValue(sourceBones[index].name, out Transform match)
                        ? match : sourceBones[index];
                renderer.bones = reboundBones;
                if (renderer.rootBone != null && characterBones.TryGetValue(renderer.rootBone.name, out Transform rootBone))
                    renderer.rootBone = rootBone;
            }
            return hair;
        }

        static void ConfigureCharacterAppearance(GameObject visual, Transform hairRoot)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit");
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                Material[] originals = renderer.sharedMaterials;
                Material[] materials = new Material[originals.Length];
                for (int index = 0; index < originals.Length; index++)
                {
                    Material original = originals[index];
                    Material material = shader != null ? new Material(shader) : new Material(original);
                    if (material == null) continue;
                    material.name = (original != null ? original.name : renderer.name) + " (Matte High Quality)";
                    string hint = (renderer.name + " " + (original != null ? original.name : string.Empty)).ToLowerInvariant();
                    bool isHair = hairRoot != null && renderer.transform.IsChildOf(hairRoot);
                    bool isFace = !isHair && hint.Contains("face");
                    Texture fallback = original != null ? original.mainTexture : null;
                    Texture texture = isHair ? (Metin2GameplaySession.HairTexture != null ? Metin2GameplaySession.HairTexture : fallback)
                        : isFace ? (Metin2GameplaySession.FaceTexture != null ? Metin2GameplaySession.FaceTexture : fallback)
                        : (Metin2GameplaySession.BodyTexture != null ? Metin2GameplaySession.BodyTexture : fallback);
                    if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                    if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
                    if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
                    if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
                    if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.06f);
                    if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0f);
                    if (material.HasProperty("_SpecColor")) material.SetColor("_SpecColor", Color.black);
                    if (texture is Texture2D sourceTexture)
                    {
                        sourceTexture.filterMode = FilterMode.Trilinear;
                        sourceTexture.anisoLevel = 16;
                        sourceTexture.mipMapBias = -0.45f;
                    }
                    if (isHair)
                    {
                        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 1f);
                        if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", 0.34f);
                        material.EnableKeyword("_ALPHATEST_ON");
                    }
                    materials[index] = material;
                }
                renderer.sharedMaterials = materials;
            }
        }

        static Vector3 FindSpawnPosition(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform spawnRoot = FindDeep(root.transform, "SpawnPoints");
                if (spawnRoot != null && spawnRoot.childCount > 0) return Ground(spawnRoot.GetChild(0).position);
            }
            Terrain terrain = FindAnyObjectByType<Terrain>();
            if (terrain != null)
            {
                Vector3 size = terrain.terrainData.size;
                Vector3 centre = terrain.transform.position + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
                centre.y = terrain.SampleHeight(centre) + terrain.transform.position.y + 0.1f;
                return centre;
            }
            return Ground(Vector3.zero);
        }

        static Vector3 Ground(Vector3 point)
        {
            if (Physics.Raycast(point + Vector3.up * 5000f, Vector3.down, out RaycastHit hit, 10000f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.05f;
            return point;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
