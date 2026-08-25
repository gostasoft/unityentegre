using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Metin3Dev.Panel
{
    [DefaultExecutionOrder(-1000)]
    public sealed class Metin3PanelSync : MonoBehaviour
    {
        const string ResourceName = "Metin3PanelConnection";
        Metin3PanelConnection connection;
        Coroutine refreshLoop;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            Metin3PanelConnection settings = Resources.Load<Metin3PanelConnection>(ResourceName);
            if (settings == null || !settings.autoConnect || string.IsNullOrWhiteSpace(settings.panelUrl)) return;
            GameObject root = new GameObject("Metin3 Panel Sync");
            DontDestroyOnLoad(root);
            root.AddComponent<Metin3PanelSync>().Configure(settings);
        }

        public void Configure(Metin3PanelConnection settings)
        {
            connection = settings;
            if (refreshLoop != null) StopCoroutine(refreshLoop);
            refreshLoop = StartCoroutine(RefreshLoop());
        }

        IEnumerator RefreshLoop()
        {
            while (connection != null)
            {
                yield return Fetch();
                yield return new WaitForSecondsRealtime(Mathf.Max(15f, connection.refreshSeconds));
            }
        }

        IEnumerator Fetch()
        {
            string endpoint = connection.panelUrl.TrimEnd('/') + "/api/game/config";
            using UnityWebRequest request = UnityWebRequest.Get(endpoint);
            if (!string.IsNullOrWhiteSpace(connection.gameApiKey))
                request.SetRequestHeader("Authorization", "Bearer " + connection.gameApiKey.Trim());
            if (!string.IsNullOrWhiteSpace(connection.sitesBypassToken))
                request.SetRequestHeader("OAI-Sites-Authorization", "Bearer " + connection.sitesBypassToken.Trim());
#if UNITY_EDITOR
            if (connection.allowLocalEditorPreview && endpoint.StartsWith("http://localhost"))
                request.SetRequestHeader("X-Metin3-Local", "1");
#endif
            request.timeout = 15;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[Metin3 Panel] Ayarlar alınamadı: " + request.error + " (" + request.responseCode + ")");
                yield break;
            }

            Metin3PanelPayload payload = JsonUtility.FromJson<Metin3PanelPayload>(request.downloadHandler.text);
            if (payload == null || payload.settings == null)
            {
                Debug.LogWarning("[Metin3 Panel] Sunucudan geçersiz yapılandırma geldi.");
                yield break;
            }
            Metin3PanelRuntime.Apply(payload);
            Debug.Log($"[Metin3 Panel] Yapılandırma eşitlendi. Varlık: {payload.entities?.Length ?? 0}, yerleşim: {payload.spawns?.Length ?? 0}, sürüm: {payload.version}");
        }
    }
}
