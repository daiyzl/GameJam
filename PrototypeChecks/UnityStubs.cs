// Only used by the external logic checks, never copied into Assets.
using System;
namespace UnityEngine
{
    public class MonoBehaviour { public Transform transform = new Transform(); }
    public class Transform { public Vector3 position; }
    public sealed class SerializeField : Attribute { }
    public sealed class MinAttribute : Attribute { public MinAttribute(float n) { } }
    public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    public sealed class ContextMenu : Attribute { public ContextMenu(string s) { } }
    public struct Vector2Int
    {
        public int x, y;
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
    }
    public struct Color
    {
        public Color(float r, float g, float b) { }
        public static Color yellow, green, cyan, red, gray;
    }
    public static class Mathf
    {
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static int Min(int a, int b) => Math.Min(a, b);
        public static float Clamp(float v, float min, float max) => Math.Max(min, Math.Min(max, v));
        public static int Clamp(int v, int min, int max) => Math.Max(min, Math.Min(max, v));
        public static int FloorToInt(float v) => (int)Math.Floor(v);
    }
    public static class Time { public static float deltaTime; }
    public static class Debug { public static void Log(object message, object context) { } }
    public static class Gizmos
    {
        public static Color color;
        public static void DrawWireCube(Vector3 center, Vector3 size) { }
        public static void DrawCube(Vector3 center, Vector3 size) { }
    }
}
