// 仅用于离线编译检查的 UnityEngine 最小桩实现（不进 Unity 工程）
// 目的：在 dotnet 下编译 Assets/Scripts/Runtime，捕捉语法与 API 用法错误。
using System;
using System.Collections;

namespace UnityEngine
{
    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0); public static Vector2 one => new Vector2(1, 1); }
    public struct Vector3 { public float x, y, z; public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; } }
    public struct Color { public float r, g, b, a;
        public Color(float r, float g, float b) : this(r, g, b, 1f) { }
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1); public static Color clear => new Color(0, 0, 0, 0); }

    public class Font : Object { public string name; }
    public class Coroutine { }

    public class WaitForSeconds { public WaitForSeconds(float t) { } }

    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }

    [AttributeUsage(AttributeTargets.Method)]
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute() { }
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { }
    }
    public enum RuntimeInitializeLoadType { BeforeSceneLoad, AfterSceneLoad }

    public class Object
    {
        public string name;
        public string tag;
        public static void Destroy(Object o) { }
        public static T FindObjectOfType<T>() where T : Object => null;
    }

    public class Component : Object
    {
        public GameObject gameObject => null;
        public Transform transform => null;
        public T GetComponent<T>() => default;
        public T GetComponentInChildren<T>() => default;
    }

    public class GameObject : Object
    {
        public GameObject() { }
        public GameObject(string name) { base.name = name; }
        public Transform transform => null;
        public T AddComponent<T>() where T : Component => null;
        public T GetComponent<T>() => default;
        public void SetActive(bool v) { }
    }

    public class Transform : Component, IEnumerable
    {
        public Vector3 position { get; set; }
        public Vector2 anchoredPosition { get; set; }
        public void SetParent(Transform p, bool worldStays) { }
        public void SetAsLastSibling() { }
        public IEnumerator GetEnumerator() => ((IEnumerable)Array.Empty<object>()).GetEnumerator();
    }

    public class RectTransform : Transform
    {
        public Vector2 anchorMin { get; set; }
        public Vector2 anchorMax { get; set; }
        public Vector2 offsetMin { get; set; }
        public Vector2 offsetMax { get; set; }
        public Vector2 sizeDelta { get; set; }
        public Vector2 pivot { get; set; }
    }

    public class Camera : Component
    {
        public bool orthographic { get; set; }
        public float orthographicSize { get; set; }
        public Color backgroundColor { get; set; }
    }

    public static class Resources
    {
        public static T GetBuiltinResource<T>(string path) where T : Object, new() => new T();
    }

    public class MonoBehaviour : Component
    {
        public Coroutine StartCoroutine(IEnumerator e) => null;
    }
}

namespace UnityEngine.EventSystems
{
    public class UIBehaviour : UnityEngine.MonoBehaviour { }
    public class EventSystem : UIBehaviour { }
    public class StandaloneInputModule : UIBehaviour { }
}

namespace UnityEngine.UI
{
    public delegate void UnityAction();

    public class Graphic : UnityEngine.EventSystems.UIBehaviour
    {
        public UnityEngine.Color color { get; set; }
        public bool raycastTarget { get; set; }
        public UnityEngine.RectTransform rectTransform => null;
    }

    public class Image : Graphic { }

    public class Text : Graphic
    {
        public string text { get; set; }
        public int fontSize { get; set; }
        public UnityEngine.TextAnchor alignment { get; set; }
        public bool supportRichText { get; set; }
        public UnityEngine.Font font { get; set; }
    }

    public class ButtonClickedEvent
    {
        public void AddListener(UnityAction a) { }
        public void RemoveListener(UnityAction a) { }
    }

    public class Selectable : UnityEngine.EventSystems.UIBehaviour
    {
        public bool interactable { get; set; }
    }

    public class Button : Selectable
    {
        public ButtonClickedEvent onClick { get; } = new ButtonClickedEvent();
    }

    public class Canvas : UnityEngine.MonoBehaviour
    {
        public RenderMode renderMode { get; set; }
    }

    public class CanvasScaler : UnityEngine.MonoBehaviour
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize }
        public ScaleMode uiScaleMode { get; set; }
        public UnityEngine.Vector2 referenceResolution { get; set; }
        public float matchWidthOrHeight { get; set; }
    }

    public class GraphicRaycaster : UnityEngine.MonoBehaviour { }

    public class LayoutGroup : UnityEngine.EventSystems.UIBehaviour
    {
        public float spacing { get; set; }
        public UnityEngine.TextAnchor childAlignment { get; set; }
        public bool childControlWidth { get; set; }
        public bool childControlHeight { get; set; }
        public bool childForceExpandWidth { get; set; }
        public bool childForceExpandHeight { get; set; }
    }

    public class HorizontalLayoutGroup : LayoutGroup { }
    public class VerticalLayoutGroup : LayoutGroup { }

    public class LayoutElement : UnityEngine.EventSystems.UIBehaviour
    {
        public float minWidth { get; set; }
        public float preferredWidth { get; set; }
    }
}
