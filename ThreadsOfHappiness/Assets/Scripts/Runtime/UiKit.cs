using UnityEngine;
using UnityEngine.UI;

namespace Toh.Runtime
{
    /// <summary>uGUI 白膜构建辅助：全部 UI 由代码动态创建。</summary>
    public static class UiKit
    {
        public static Canvas CreateCanvas()
        {
            var go = new GameObject("Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform CreatePanel(Transform parent, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var img = go.AddComponent<Image>();
            img.color = color;
            return rt;
        }

        public static Text CreateText(Transform parent, string name, string content, int size,
            Color color, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var text = go.AddComponent<Text>();
            text.text = content;
            text.color = color;
            text.alignment = align;
            text.fontSize = size;
            text.supportRichText = true;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return text;
        }

        public static Button CreateButton(Transform parent, string name, string label, int fontSize,
            Color bgColor, Color textColor, Vector2 size)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = bgColor;
            var btn = go.AddComponent<Button>();
            var txt = CreateText(rt, "Label", label, fontSize, textColor, TextAnchor.MiddleCenter);
            txt.raycastTarget = false;
            return btn;
        }

        public static readonly Color PanelBg = new Color(1f, 1f, 1f, 0.85f);
        public static readonly Color CellAlive = new Color(0.98f, 0.97f, 0.94f);
        public static readonly Color CellHappy = new Color(0.99f, 0.80f, 0.87f);
        public static readonly Color CellSelect = new Color(0.72f, 0.87f, 1.00f);
        public static readonly Color CellPossessed = new Color(0.85f, 0.72f, 1.00f);
        public static readonly Color CellTarget = new Color(0.75f, 0.95f, 0.75f);
        public static readonly Color MonitorColor = new Color(0.25f, 0.47f, 0.86f);
        public static readonly Color MonitorActive = new Color(1f, 0.76f, 0.10f);           // 本回合监视生效中：黄色
        public static readonly Color MonitorExpired = new Color(0.55f, 0.55f, 0.57f, 0.9f); // 失效后：灰色保持不变
        public static readonly Color GiftRed = new Color(0.90f, 0.30f, 0.28f);
        public static readonly Color GiftBlue = new Color(0.25f, 0.55f, 0.90f);
        public static readonly Color TextDark = new Color(0.15f, 0.15f, 0.16f);
    }
}
