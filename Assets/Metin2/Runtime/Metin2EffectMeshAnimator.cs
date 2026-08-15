using UnityEngine;

namespace Metin2Dev
{
    /// <summary>Plays the vertex-frame animation stored in a Metin2 MDE effect mesh.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class Metin2EffectMeshAnimator : MonoBehaviour
    {
        public Mesh[] frames;
        public float[] visibility;
        public float frameDelay = 0.02f;
        public bool loop = true;
        public Color baseColor = Color.white;

        MeshFilter meshFilter;
        Renderer meshRenderer;
        MaterialPropertyBlock properties;
        double startedAt;
        int appliedFrame = -1;

        void OnEnable()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<Renderer>();
            startedAt = Time.realtimeSinceStartupAsDouble;
            appliedFrame = -1;
            ApplyFrame(0);
        }

        void Update()
        {
            if (frames == null || frames.Length == 0) return;
            int rawFrame = Mathf.Max(0, (int)((Time.realtimeSinceStartupAsDouble - startedAt) / Mathf.Max(0.001f, frameDelay)));
            int frame = loop ? rawFrame % frames.Length : Mathf.Min(rawFrame, frames.Length - 1);
            ApplyFrame(frame);
        }

        void ApplyFrame(int frame)
        {
            if (frame == appliedFrame || frames == null || frame < 0 || frame >= frames.Length) return;
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            if (meshRenderer == null) meshRenderer = GetComponent<Renderer>();
            if (meshFilter != null && frames[frame] != null) meshFilter.sharedMesh = frames[frame];

            if (meshRenderer != null)
            {
                if (properties == null) properties = new MaterialPropertyBlock();
                meshRenderer.GetPropertyBlock(properties);
                Color color = baseColor;
                if (visibility != null && frame < visibility.Length) color.a *= visibility[frame];
                properties.SetColor("_BaseColor", color);
                properties.SetColor("_Color", color);
                meshRenderer.SetPropertyBlock(properties);
            }
            appliedFrame = frame;
        }
    }
}
