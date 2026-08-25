using System;
using System.Collections;
using Metin2Dev.Gameplay;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace Metin3Dev.Panel
{
    public sealed class Metin3PlayerTelemetry : MonoBehaviour
    {
        [Serializable]
        sealed class PlayerSyncPayload
        {
            public string account;
            public string characterName;
            public string empire;
            public string characterClass;
            public int level;
            public bool online;
            public long yang;
            public long won;
            public string mapCode;
            public float x;
            public float y;
            public string hwid;
            public string pcId;
        }

        Metin3PanelConnection connection;
        Coroutine loop;

        public void Configure(Metin3PanelConnection settings)
        {
            connection = settings;
            if (loop != null) StopCoroutine(loop);
            loop = StartCoroutine(SyncLoop());
        }

        IEnumerator SyncLoop()
        {
            while (connection != null)
            {
                if (Metin2GameplaySession.HasCharacter) yield return Send();
                yield return new WaitForSecondsRealtime(Mathf.Max(5f, connection.refreshSeconds));
            }
        }

        IEnumerator Send()
        {
            Metin2PlayerController player = FindFirstObjectByType<Metin2PlayerController>();
            Vector3 position = player != null ? player.transform.position : Vector3.zero;
            PlayerSyncPayload payload = new PlayerSyncPayload
            {
                account = Metin2GameplaySession.AccountName,
                characterName = Metin2GameplaySession.CharacterName,
                empire = Metin2GameplaySession.Empire.ToString(),
                characterClass = Metin2GameplaySession.CharacterClass.ToString(),
                level = Metin2GameplaySession.Level,
                online = true,
                yang = Metin2GameplayUI.CurrentGold,
                won = 0,
                mapCode = SceneManager.GetActiveScene().name,
                x = position.x,
                y = position.z,
                hwid = SystemInfo.deviceUniqueIdentifier,
                pcId = SystemInfo.deviceName + ":" + SystemInfo.deviceModel,
            };
            string endpoint = connection.panelUrl.TrimEnd('/') + "/api/game/player-sync";
            using UnityWebRequest request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST);
            byte[] json = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
            request.uploadHandler = new UploadHandlerRaw(json);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrWhiteSpace(connection.gameApiKey)) request.SetRequestHeader("Authorization", "Bearer " + connection.gameApiKey.Trim());
            if (!string.IsNullOrWhiteSpace(connection.sitesBypassToken)) request.SetRequestHeader("OAI-Sites-Authorization", "Bearer " + connection.sitesBypassToken.Trim());
#if UNITY_EDITOR
            if (connection.allowLocalEditorPreview && endpoint.StartsWith("http://localhost")) request.SetRequestHeader("X-Metin3-Local", "1");
#endif
            request.timeout = 15;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success) Debug.LogWarning("[Metin3 Panel] Oyuncu telemetrisi gönderilemedi: " + request.error);
        }
    }
}
