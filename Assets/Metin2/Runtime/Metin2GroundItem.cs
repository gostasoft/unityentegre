using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Metin2Dev.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class Metin2GroundItem : MonoBehaviour
    {
        Metin2InventoryEntry entry;
        Canvas labelCanvas;

        public static void Spawn(Metin2InventoryEntry item, Vector3 position)
        {
            if (item == null) return;
            if (Physics.Raycast(position + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore))
                position = hit.point + Vector3.up * 0.12f;
            GameObject root = new GameObject($"Drop - {item.name} x{item.count}");
            root.transform.position = position;
            Metin2GroundItem drop = root.AddComponent<Metin2GroundItem>();
            drop.entry = item;
            drop.BuildVisual();
        }

        void BuildVisual()
        {
            GameObject model = Metin2ItemDatabase.GetWorldModel(entry.vnum);
            if (model != null)
            {
                GameObject visual = Instantiate(model, transform, false);
                visual.name = "Original Item Model";
                NormalizeModel(visual);
            }
            else
            {
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = "Drop Marker";
                marker.transform.SetParent(transform, false);
                marker.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                marker.transform.localScale = new Vector3(0.28f, 0.025f, 0.28f);
                Renderer renderer = marker.GetComponent<Renderer>();
                if (renderer != null) renderer.material.color = new Color(0.95f, 0.72f, 0.18f, 0.9f);
            }

            SphereCollider collider = gameObject.AddComponent<SphereCollider>();
            collider.radius = 0.65f;
            collider.center = Vector3.up * 0.35f;
            BuildLabel();
            StartCoroutine(Expire());
        }

        void BuildLabel()
        {
            GameObject canvasObject = new GameObject("Item Name and Icon", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            canvasObject.transform.localScale = Vector3.one * 0.006f;
            labelCanvas = canvasObject.GetComponent<Canvas>();
            labelCanvas.renderMode = RenderMode.WorldSpace;
            labelCanvas.sortingOrder = 32000;
            RectTransform canvasRect = labelCanvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(250f, 54f);

            Texture2D icon = Metin2ItemDatabase.GetIcon(entry.vnum);
            if (icon != null)
            {
                GameObject iconObject = new GameObject("Original Item Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                RectTransform rect = iconObject.GetComponent<RectTransform>();
                rect.SetParent(canvasRect, false);
                rect.anchorMin = new Vector2(0f, 0.5f); rect.anchorMax = new Vector2(0f, 0.5f); rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(42f, 42f);
                iconObject.GetComponent<RawImage>().texture = icon;
            }

            GameObject textObject = new GameObject("Item Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(canvasRect, false);
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(icon != null ? 48f : 4f, 0f); textRect.offsetMax = Vector2.zero;
            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 22; text.alignment = TextAnchor.MiddleLeft; text.color = new Color(1f, 0.82f, 0.22f);
            text.text = entry.name + (entry.count > 1 ? " x" + entry.count : string.Empty);
            Outline outline = textObject.GetComponent<Outline>(); outline.effectColor = Color.black; outline.effectDistance = new Vector2(2f, -2f);
        }

        void LateUpdate()
        {
            Camera camera = Camera.main;
            if (labelCanvas != null && camera != null)
                labelCanvas.transform.rotation = Quaternion.LookRotation(labelCanvas.transform.position - camera.transform.position, Vector3.up);
        }

        void OnMouseDown() => Pickup();

        void Pickup()
        {
            Metin2PlayerState player = Metin2PlayerState.Local;
            if (player == null || Vector3.Distance(player.transform.position, transform.position) > 4.5f)
            {
                Metin2ChatService.Append(Metin2ChatChannel.Info, "Eşyayı almak için biraz daha yaklaş.");
                return;
            }
            if (!Metin2InventoryService.Add(entry.vnum, entry.name, entry.count, entry.stackable)) return;
            Metin2ChatService.Append(Metin2ChatChannel.Info, $"{entry.name} x{entry.count} aldın.");
            Destroy(gameObject);
        }

        IEnumerator Expire()
        {
            yield return new WaitForSecondsRealtime(180f);
            if (this != null) Destroy(gameObject);
        }

        void NormalizeModel(GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (largest > 0.001f) model.transform.localScale *= 0.65f / largest;
            Bounds adjusted = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++) adjusted.Encapsulate(renderers[index].bounds);
            model.transform.position += Vector3.up * (transform.position.y - adjusted.min.y + 0.08f);
        }
    }
}
