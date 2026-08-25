using UnityEngine;

namespace Metin3Dev.Panel
{
    [CreateAssetMenu(menuName = "Metin3/Panel Connection", fileName = "Metin3PanelConnection")]
    public sealed class Metin3PanelConnection : ScriptableObject
    {
        [Tooltip("Published panel address, without a trailing slash.")]
        public string panelUrl = "http://localhost:3000";
        [Tooltip("Use a server-side key in production. Never commit a live key to Git.")]
        public string gameApiKey = string.Empty;
        [Tooltip("Private Sites access token. Keep it local and never commit a live token to Git.")]
        public string sitesBypassToken = string.Empty;
        [Tooltip("Panel changes are polled in realtime. 3 seconds is recommended for development.")]
        [Min(2f)] public float refreshSeconds = 3f;
        public bool autoConnect = true;
        [Tooltip("Allows the localhost preview endpoint without an API key inside the Unity Editor.")]
        public bool allowLocalEditorPreview = true;
    }
}
