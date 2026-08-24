using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Metin2Dev.Frontend
{
    [DisallowMultipleComponent]
    public sealed class Metin2CharacterPreview : MonoBehaviour
    {
        const int PreviewLayer = 31;

        Camera previewCamera;
        Transform stageRoot;
        Transform modelAnchor;
        GameObject currentModel;
        GameObject currentHair;
        RenderTexture renderTexture;
        Material[] runtimeMaterials = Array.Empty<Material>();
        float modelYaw;
        bool portraitFraming;

        public void Initialize(RawImage target, int resolution = 768, bool usePortraitFraming = false)
        {
            DisposePreview();
            portraitFraming = usePortraitFraming;
            resolution = Mathf.Clamp(resolution, 128, 768);

            renderTexture = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32)
            {
                name = "Metin2 Character Preview",
                antiAliasing = 4,
                useMipMap = false,
                autoGenerateMips = false,
            };
            renderTexture.Create();
            target.texture = renderTexture;
            target.color = Color.white;

            stageRoot = new GameObject("Metin2 Character Preview Stage").transform;
            stageRoot.position = new Vector3(0f, -10000f, 0f);

            GameObject cameraObject = new GameObject("Character Preview Camera");
            cameraObject.transform.SetParent(stageRoot, false);
            previewCamera = cameraObject.AddComponent<Camera>();
            UniversalAdditionalCameraData cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = false;
            cameraData.renderShadows = true;
            previewCamera.targetTexture = renderTexture;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.fieldOfView = 30f;
            previewCamera.nearClipPlane = 0.05f;
            previewCamera.farClipPlane = 50f;
            previewCamera.allowHDR = true;
            previewCamera.allowMSAA = true;
            previewCamera.transform.localPosition = new Vector3(0f, 0.15f, -6.25f);
            previewCamera.transform.LookAt(stageRoot.TransformPoint(new Vector3(0f, 0.08f, 0f)), Vector3.up);

            modelAnchor = new GameObject("Character Model").transform;
            modelAnchor.SetParent(stageRoot, false);
            SetLayer(modelAnchor.gameObject, PreviewLayer);

            Light key = CreateLight("Key Light", new Color(1f, 0.79f, 0.58f), 2.2f);
            key.type = LightType.Directional;
            key.transform.rotation = Quaternion.Euler(32f, -42f, 0f);
            Light fill = CreateLight("Fill Light", new Color(0.42f, 0.58f, 1f), 1.25f);
            fill.type = LightType.Directional;
            fill.transform.rotation = Quaternion.Euler(18f, 142f, 0f);
            Light front = CreateLight("Front Light", new Color(1f, 0.86f, 0.72f), 5.5f);
            front.type = LightType.Point;
            front.range = 12f;
            front.shadows = LightShadows.None;
            front.transform.localPosition = new Vector3(0f, 1.1f, -3.8f);
        }

        public void Show(Metin2FrontendConfig config, Metin2CharacterClass characterClass, Metin2Gender gender)
        {
            ClearModel();
            if (config == null || modelAnchor == null) return;
            GameObject prefab = config.GetRacePrefab(characterClass, gender);
            if (prefab == null) return;

            currentModel = Instantiate(prefab, modelAnchor);
            currentModel.name = prefab.name;
            currentModel.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            currentModel.transform.localScale = Vector3.one;
            GameObject hairPrefab = config.GetHairPrefab(characterClass, gender);
            if (hairPrefab != null)
            {
                currentHair = Instantiate(hairPrefab, currentModel.transform);
                currentHair.name = hairPrefab.name;
                currentHair.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                currentHair.transform.localScale = Vector3.one;
            }
            SetLayer(currentModel, PreviewLayer);
            ApplyMaterials(
                config.previewShader,
                config.GetBodyTexture(characterClass, gender),
                config.GetFaceTexture(characterClass, gender),
                config.GetHairTexture(characterClass, gender));
            ApplyRelaxedPose();
            FitModel();
            modelYaw = 180f;
            modelAnchor.localRotation = Quaternion.Euler(0f, modelYaw, 0f);
        }

        public void Hide()
        {
            ClearModel();
        }

        void Update()
        {
            if (currentModel == null || modelAnchor == null) return;
            modelYaw = Mathf.Repeat(modelYaw + Time.unscaledDeltaTime * 5.5f, 360f);
            modelAnchor.localRotation = Quaternion.Euler(0f, modelYaw, 0f);
        }

        void FitModel()
        {
            Renderer[] renderers = currentModel.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            Bounds bounds = default;
            bool hasBounds = false;
            foreach (Renderer renderer in renderers)
            {
                Bounds worldBounds = renderer.bounds;
                Vector3 center = worldBounds.center;
                Vector3 extents = worldBounds.extents;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 worldCorner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    Vector3 localCorner = currentModel.transform.InverseTransformPoint(worldCorner);
                    if (!hasBounds)
                    {
                        bounds = new Bounds(localCorner, Vector3.zero);
                        hasBounds = true;
                    }
                    else bounds.Encapsulate(localCorner);
                }
            }
            if (!hasBounds) return;
            float scale = (portraitFraming ? 5f : 2.95f) / Mathf.Max(0.001f, bounds.size.y);
            currentModel.transform.localScale = Vector3.one * scale;
            currentModel.transform.localPosition = new Vector3(
                -bounds.center.x * scale,
                -bounds.center.y * scale - (portraitFraming ? 1.25f : 0.08f),
                -bounds.center.z * scale);
        }

        void ApplyMaterials(Shader previewShader, Texture2D body, Texture2D face, Texture2D hair)
        {
            Shader shader = previewShader ?? Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) return;

            Renderer[] renderers = currentModel.GetComponentsInChildren<Renderer>(true);
            int materialCount = 0;
            foreach (Renderer renderer in renderers) materialCount += renderer.sharedMaterials.Length;
            runtimeMaterials = new Material[materialCount];
            int materialIndex = 0;
            foreach (Renderer renderer in renderers)
            {
                Material[] source = renderer.sharedMaterials;
                Material[] replacement = new Material[source.Length];
                for (int i = 0; i < source.Length; i++)
                {
                    Material original = source[i];
                    string hint = original != null ? original.name.ToLowerInvariant() : string.Empty;
                    string rendererHint = renderer.name.ToLowerInvariant();
                    Texture fallback = original != null ? original.mainTexture : null;
                    bool isHair = currentHair != null && renderer.transform.IsChildOf(currentHair.transform);
                    bool isFace = !isHair && (rendererHint.Contains("face") || hint.Contains("face"));
                    Texture chosen = isFace
                        ? (face != null ? face : fallback)
                        : isHair
                            ? (hair != null ? hair : (body != null ? body : fallback))
                            : (body != null ? body : fallback);

                    Material material = new Material(shader)
                    {
                        name = (original != null ? original.name : "Character") + " (URP)",
                    };
                    if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", chosen);
                    if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
                    if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.16f);
                    if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.02f);
                    if (chosen != null && material.HasProperty("_EmissionMap") && material.HasProperty("_EmissionColor"))
                    {
                        material.SetTexture("_EmissionMap", chosen);
                        material.SetColor("_EmissionColor", new Color(0.48f, 0.48f, 0.48f, 1f));
                        material.EnableKeyword("_EMISSION");
                    }
                    if (isHair && chosen != null)
                    {
                        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 1f);
                        if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", 0.34f);
                        material.EnableKeyword("_ALPHATEST_ON");
                        material.renderQueue = 2450;
                    }
                    replacement[i] = material;
                    runtimeMaterials[materialIndex++] = material;
                }
                renderer.sharedMaterials = replacement;
            }
        }

        void ApplyRelaxedPose()
        {
            if (currentModel == null) return;
            Transform leftUpper = FindBone("Bip01 L UpperArm");
            Transform leftForearm = FindBone("Bip01 L Forearm");
            Transform leftHand = FindBone("Bip01 L Hand");
            Transform rightUpper = FindBone("Bip01 R UpperArm");
            Transform rightForearm = FindBone("Bip01 R Forearm");
            Transform rightHand = FindBone("Bip01 R Hand");
            PointBone(leftUpper, leftForearm, 0.46f, -0.88f, 0.08f);
            PointBone(rightUpper, rightForearm, 0.46f, -0.88f, 0.08f);
            PointBone(leftForearm, leftHand, 0.14f, -0.98f, -0.04f);
            PointBone(rightForearm, rightHand, 0.14f, -0.98f, -0.04f);
        }

        Transform FindBone(string name)
        {
            foreach (Transform child in currentModel.GetComponentsInChildren<Transform>(true))
                if (string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase)) return child;
            return null;
        }

        static void PointBone(Transform bone, Transform child, float sideAmount, float downAmount, float depthAmount)
        {
            if (bone == null || child == null) return;
            Vector3 currentDirection = child.position - bone.position;
            if (currentDirection.sqrMagnitude < 0.000001f) return;
            float side = Mathf.Approximately(currentDirection.x, 0f) ? 1f : Mathf.Sign(currentDirection.x);
            Vector3 desiredDirection = new Vector3(side * sideAmount, downAmount, depthAmount).normalized;
            bone.rotation = Quaternion.FromToRotation(currentDirection.normalized, desiredDirection) * bone.rotation;
        }

        Light CreateLight(string name, Color color, float intensity)
        {
            GameObject lightObject = new GameObject(name);
            lightObject.layer = PreviewLayer;
            lightObject.transform.SetParent(stageRoot, false);
            Light light = lightObject.AddComponent<Light>();
            light.cullingMask = 1 << PreviewLayer;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.Soft;
            return light;
        }

        void ClearModel()
        {
            if (currentModel != null) Destroy(currentModel);
            currentModel = null;
            currentHair = null;
            foreach (Material material in runtimeMaterials)
                if (material != null) Destroy(material);
            runtimeMaterials = Array.Empty<Material>();
        }

        void DisposePreview()
        {
            ClearModel();
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
                renderTexture = null;
            }
            if (stageRoot != null)
            {
                Destroy(stageRoot.gameObject);
                stageRoot = null;
                modelAnchor = null;
                previewCamera = null;
            }
        }

        void OnDestroy()
        {
            DisposePreview();
        }

        void OnDisable()
        {
            DisposePreview();
        }

        static void SetLayer(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform) SetLayer(child.gameObject, layer);
        }
    }
}
