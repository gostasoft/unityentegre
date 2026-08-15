using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Metin2Dev.Gameplay
{
    public sealed class Metin2GameplayBootstrap : MonoBehaviour
    {
        const string DatabaseResource = "Metin2GameplayDatabase";
        static Metin2GameplayBootstrap instance;

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
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Metin2_Intro" || FindAnyObjectByType<Metin2PlayerController>() != null) return;
            StartCoroutine(SpawnWhenReady(scene));
        }

        IEnumerator SpawnWhenReady(Scene scene)
        {
            yield return null;
            Metin2GameplayDatabase database = Resources.Load<Metin2GameplayDatabase>(DatabaseResource);
            if (database == null) yield break;
            Metin2GameplaySession.UseEditorDefault();
            Metin2RaceMotionSet set = database.Find(Metin2GameplaySession.CharacterClass, Metin2GameplaySession.Gender);
            if (set == null || set.playerPrefab == null || set.animatorController == null)
            {
                Debug.LogError("Metin2 gameplay race data is missing. Run Tools > Metin2 > Build Player Gameplay.");
                yield break;
            }

            Vector3 spawn = FindSpawnPosition(scene);
            GameObject player = new GameObject("Player - " + Metin2GameplaySession.CharacterName);
            player.transform.position = spawn;
            SceneManager.MoveGameObjectToScene(player, scene);
            CharacterController capsule = player.AddComponent<CharacterController>();

            GameObject visual = Instantiate(set.playerPrefab, player.transform);
            visual.name = "Character Visual";
            visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (Camera nestedCamera in visual.GetComponentsInChildren<Camera>(true)) nestedCamera.enabled = false;
            foreach (Light nestedLight in visual.GetComponentsInChildren<Light>(true)) nestedLight.enabled = false;
            Animator animator = visual.GetComponentInChildren<Animator>(true);
            if (animator == null) animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = set.animatorController;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

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
            cameraController.target = player.transform;

            Metin2PlayerController controller = player.AddComponent<Metin2PlayerController>();
            controller.Initialize(set, animator, camera, Metin2GameplaySession.CharacterName);
            capsule.enabled = true;
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
