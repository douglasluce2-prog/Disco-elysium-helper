// Shape-only stand-ins for the assemblies MelonLoader generates from Disco Elysium (in Il2CppInterop style:
// game classes derive from Il2CppSystem.Object, arrays are Il2CppReferenceArray, global-namespace game
// types live in "Il2Cpp", TMPro in "Il2CppTMPro"). Only the members the mod uses are declared.
// Used purely to compile-check the mod without the game. Nothing here runs.
using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace UnityEngine
{
    public class Object : Il2CppSystem.Object
    {
        public Object(IntPtr p) : base(p) { }
        public string name { get => null; set { } }
        public static void DontDestroyOnLoad(Object target) { }
        public static Il2CppReferenceArray<Object> FindObjectsOfType(Il2CppSystem.Type type) => null;
        public static implicit operator bool(Object exists) => exists != null;
    }
    public sealed class GameObject : Object
    {
        public GameObject(string name) : base(IntPtr.Zero) { }
        public T AddComponent<T>() where T : Component => default;
        public Transform transform => null;
        public void SetActive(bool value) { }
        public bool activeSelf => false;
        public bool activeInHierarchy => false;
    }
    public class Component : Object { public Component(IntPtr p) : base(p) { } public GameObject gameObject => null; public Transform transform => null; }
    public class Behaviour : Component { public Behaviour(IntPtr p) : base(p) { } public bool enabled { get; set; } public bool isActiveAndEnabled => false; }
    public class MonoBehaviour : Behaviour { public MonoBehaviour(IntPtr p) : base(p) { } }
    public class ScriptableObject : Object { public ScriptableObject(IntPtr p) : base(p) { } }
    public class Transform : Component { public Transform(IntPtr p) : base(p) { } public void SetParent(Transform parent, bool worldPositionStays) { } public bool IsChildOf(Transform parent) => false; }
    public sealed class RectTransform : Transform
    {
        public RectTransform(IntPtr p) : base(p) { }
        public Vector2 anchorMin { get; set; }
        public Vector2 anchorMax { get; set; }
        public Vector2 pivot { get; set; }
        public Vector2 anchoredPosition { get; set; }
        public Vector2 sizeDelta { get; set; }
        public Vector2 offsetMin { get; set; }
        public Vector2 offsetMax { get; set; }
        public Rect rect => default;
    }
    public struct Rect { public float width => 0; public float height => 0; }
    public struct Vector2
    {
        public float x; public float y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => default;
        public static Vector2 one => new Vector2(1, 1);
        public static implicit operator Vector2(Vector3 v) => new Vector2(v.x, v.y);
        public static implicit operator Vector3(Vector2 v) => new Vector3(v.x, v.y, 0);
    }
    public struct Vector3 { public float x; public float y; public float z; public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; } }
    public struct Vector4 { public float x, y, z, w; }
    public struct Color { public float r, g, b, a; public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; } }
    public struct Mathf { public static float Min(float a, float b) => a < b ? a : b; public static float Max(float a, float b) => a > b ? a : b; }
    public sealed class Time : Il2CppSystem.Object { public Time(IntPtr p) : base(p) { } public static float unscaledTime => 0; }
    public sealed class Input : Il2CppSystem.Object
    {
        public Input(IntPtr p) : base(p) { }
        public static bool GetKeyDown(KeyCode key) => false;
        public static bool GetKey(KeyCode key) => false;
        public static bool GetMouseButtonDown(int button) => false;
        public static Vector3 mousePosition => default;
        public static Vector2 mouseScrollDelta => default;
        public static string inputString => "";
        public static void ResetInputAxes() { }
    }
    public enum KeyCode { None = 0, Backspace = 8, Return = 13, Escape = 27, UpArrow = 273, DownArrow = 274, PageUp = 280, PageDown = 281, F1 = 282, F2 = 283, F3 = 284 }
    public sealed class Camera : Behaviour { public Camera(IntPtr p) : base(p) { } public static Camera main => null; }
    public enum RenderMode { ScreenSpaceOverlay = 0, ScreenSpaceCamera = 1, WorldSpace = 2 }
    public sealed class Canvas : Behaviour { public Canvas(IntPtr p) : base(p) { } public RenderMode renderMode { get; set; } public int sortingOrder { get; set; } public Camera worldCamera { get; set; } }
    public sealed class RectTransformUtility : Il2CppSystem.Object { public RectTransformUtility(IntPtr p) : base(p) { } public static bool RectangleContainsScreenPoint(RectTransform rect, Vector2 screenPoint, Camera cam) => false; }
    public sealed class Resources : Il2CppSystem.Object { public Resources(IntPtr p) : base(p) { } public static Il2CppReferenceArray<Object> FindObjectsOfTypeAll(Il2CppSystem.Type type) => null; }
}

namespace UnityEngine.EventSystems
{
    public class UIBehaviour : UnityEngine.MonoBehaviour { public UIBehaviour(IntPtr p) : base(p) { } }
    public class BaseRaycaster : UIBehaviour { public BaseRaycaster(IntPtr p) : base(p) { } }
}

namespace UnityEngine.UI
{
    public class Graphic : UnityEngine.EventSystems.UIBehaviour
    {
        public Graphic(IntPtr p) : base(p) { }
        public Color color { get; set; }
        public bool raycastTarget { get; set; }
        public Canvas canvas => null;
        public RectTransform rectTransform => null;
    }
    public class MaskableGraphic : Graphic { public MaskableGraphic(IntPtr p) : base(p) { } }
    public class Image : MaskableGraphic { public Image(IntPtr p) : base(p) { } }
    public class RectMask2D : UnityEngine.EventSystems.UIBehaviour { public RectMask2D(IntPtr p) : base(p) { } }
    public class GraphicRaycaster : UnityEngine.EventSystems.BaseRaycaster { public GraphicRaycaster(IntPtr p) : base(p) { } }
    public class CanvasScaler : UnityEngine.EventSystems.UIBehaviour
    {
        public CanvasScaler(IntPtr p) : base(p) { }
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink }
        public ScaleMode uiScaleMode { get; set; }
        public Vector2 referenceResolution { get; set; }
        public ScreenMatchMode screenMatchMode { get; set; }
        public float matchWidthOrHeight { get; set; }
    }
}

namespace Il2CppTMPro
{
    public enum TextAlignmentOptions { TopLeft = 257, Top = 258, TopRight = 260 }
    public enum TextOverflowModes { Overflow = 0, Ellipsis = 1, Masking = 2, Truncate = 3, ScrollRect = 4, Page = 5, Linked = 6 }
    public class TMP_Asset : UnityEngine.ScriptableObject { public TMP_Asset(IntPtr p) : base(p) { } }
    public class TMP_FontAsset : TMP_Asset { public TMP_FontAsset(IntPtr p) : base(p) { } }
    public class TMP_WordInfo : Il2CppSystem.ValueType { public TMP_WordInfo(IntPtr p) : base(p) { } public int firstCharacterIndex { get; set; } public string GetWord() => null; }
    public class TMP_LinkInfo : Il2CppSystem.ValueType { public TMP_LinkInfo(IntPtr p) : base(p) { } public string GetLinkID() => null; }
    public class TMP_TextInfo : Il2CppSystem.Object
    {
        public TMP_TextInfo(IntPtr p) : base(p) { }
        public int characterCount { get; set; }
        public int wordCount { get; set; }
        public int linkCount { get; set; }
        public Il2CppReferenceArray<TMP_WordInfo> wordInfo { get; set; }
        public Il2CppReferenceArray<TMP_LinkInfo> linkInfo { get; set; }
    }
    public abstract class TMP_Text : UnityEngine.UI.MaskableGraphic
    {
        public TMP_Text(IntPtr p) : base(p) { }
        public string text { get; set; }
        public TMP_FontAsset font { get; set; }
        public float fontSize { get; set; }
        public bool richText { get; set; }
        public bool enableWordWrapping { get; set; }
        public TextOverflowModes overflowMode { get; set; }
        public TextAlignmentOptions alignment { get; set; }
        public float lineSpacing { get; set; }
        public float paragraphSpacing { get; set; }
        public TMP_TextInfo textInfo => null;
        public string GetParsedText() => null;
        public Vector2 GetPreferredValues(string text, float width, float height) => default;
    }
    public class TextMeshProUGUI : TMP_Text { public TextMeshProUGUI(IntPtr p) : base(p) { } }
    public sealed class TMP_TextUtilities : Il2CppSystem.Object
    {
        public TMP_TextUtilities(IntPtr p) : base(p) { }
        public static int FindIntersectingLink(TMP_Text text, Vector3 position, Camera camera) => -1;
        public static int FindIntersectingWord(TMP_Text text, Vector3 position, Camera camera) => -1;
    }
}

namespace Il2Cpp
{
    public class FinalEntry : Il2CppSystem.Object { public FinalEntry(IntPtr p) : base(p) { } public string speakerName { get; set; } public string spokenLine { get; set; } }
    public class LogRenderer : UnityEngine.MonoBehaviour { public LogRenderer(IntPtr p) : base(p) { } public void AddToLog(FinalEntry entry) { } }
}

namespace Il2CppInControl
{
    public class InputManager : Il2CppSystem.Object { public InputManager(IntPtr p) : base(p) { } public static void ClearInputState() { } }
}
