using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Metin2Dev.Frontend
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler))]
    public sealed class Metin2FrontendCompositionScaler : MonoBehaviour
    {
        static readonly string[] ScreenNames =
        {
            "Login Screen",
            "Empire Selection",
            "Character Selection",
            "Character Creation",
            "Loading Screen",
        };

        [SerializeField] Vector2 authoredResolution = new Vector2(1280f, 720f);
        [SerializeField, HideInInspector] bool authoredResolutionCaptured;

        Vector2 lastCanvasSize;
        bool applying;

        public bool HasCapturedResolution => authoredResolutionCaptured;
        public Vector2 AuthoredResolution => authoredResolution;

        public bool CaptureCurrentLayout()
        {
            if (authoredResolutionCaptured) return false;
            Canvas.ForceUpdateCanvases();
            RectTransform canvasRect = transform as RectTransform;
            if (canvasRect == null) return false;

            Vector2 currentSize = canvasRect.rect.size;
            if (currentSize.x < 32f || currentSize.y < 32f)
                currentSize = new Vector2(Mathf.Max(32f, Screen.width), Mathf.Max(32f, Screen.height));

            authoredResolution = currentSize;
            authoredResolutionCaptured = true;
            ApplyNow();
            return true;
        }

        public void ApplyNow()
        {
            if (applying || !authoredResolutionCaptured || authoredResolution.x < 1f || authoredResolution.y < 1f)
                return;

            applying = true;
            try
            {
                CanvasScaler scaler = GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                    scaler.scaleFactor = 1f;
                }

                Canvas.ForceUpdateCanvases();
                RectTransform canvasRect = transform as RectTransform;
                if (canvasRect == null) return;
                Vector2 available = canvasRect.rect.size;
                if (available.x < 1f || available.y < 1f) return;

                float uniformScale = Mathf.Min(
                    available.x / authoredResolution.x,
                    available.y / authoredResolution.y);
                uniformScale = Mathf.Max(0.01f, uniformScale);

                foreach (Transform child in transform)
                {
                    if (!ScreenNames.Contains(child.name)) continue;
                    RectTransform screen = child as RectTransform;
                    if (screen == null) continue;
                    screen.anchorMin = new Vector2(0.5f, 0.5f);
                    screen.anchorMax = new Vector2(0.5f, 0.5f);
                    screen.pivot = new Vector2(0.5f, 0.5f);
                    screen.anchoredPosition = Vector2.zero;
                    screen.sizeDelta = authoredResolution;
                    screen.localScale = new Vector3(uniformScale, uniformScale, 1f);
                }
                lastCanvasSize = available;
            }
            finally
            {
                applying = false;
            }
        }

        void OnEnable()
        {
            ApplyNow();
        }

        void OnValidate()
        {
            authoredResolution.x = Mathf.Max(32f, authoredResolution.x);
            authoredResolution.y = Mathf.Max(32f, authoredResolution.y);
            ApplyNow();
        }

        void OnRectTransformDimensionsChange()
        {
            ApplyNow();
        }

        void LateUpdate()
        {
            RectTransform canvasRect = transform as RectTransform;
            if (canvasRect != null && canvasRect.rect.size != lastCanvasSize) ApplyNow();
        }
    }
}
