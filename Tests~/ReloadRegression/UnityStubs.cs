using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

namespace UnityEngine
{
    internal class Object
    {
    }

    internal sealed class AnimationClip : Object
    {
    }

    internal static class Application
    {
        internal static string dataPath =>
            Path.Combine(Path.GetTempPath(), "CodexUnityLinkReloadRegression", "Assets");
    }

    internal static class Debug
    {
        internal static void Log(object message)
        {
        }

        internal static void LogWarning(object message)
        {
        }

        internal static void LogError(object message)
        {
        }
    }

    internal static class JsonUtility
    {
        internal static T FromJson<T>(string json)
        {
            var value = Activator.CreateInstance<T>();
            SetField(value, "version", ExtractInt(json, "version"));
            SetField(value, "requestId", ExtractString(json, "requestId"));
            SetField(value, "action", ExtractString(json, "action"));
            SetField(value, "projectRoot", ExtractString(json, "projectRoot"));
            SetField(value, "assetPath", ExtractString(json, "assetPath"));
            SetField(value, "line", ExtractInt(json, "line"));
            SetField(value, "column", ExtractInt(json, "column"));
            return value;
        }

        internal static string ToJson(object value)
        {
            var type = value.GetType();
            return "{\"version\":" + GetField<int>(type, value, "version")
                   + ",\"requestId\":\"" + Escape(GetField<string>(type, value, "requestId")) + "\""
                   + ",\"ok\":" + GetField<bool>(type, value, "ok").ToString().ToLowerInvariant()
                   + ",\"code\":\"" + Escape(GetField<string>(type, value, "code")) + "\""
                   + ",\"message\":\"" + Escape(GetField<string>(type, value, "message")) + "\"}";
        }

        private static void SetField<T>(T value, string name, object fieldValue)
        {
            value.GetType().GetField(name)?.SetValue(value, fieldValue);
        }

        private static TValue GetField<TValue>(Type type, object value, string name)
        {
            return (TValue)type.GetField(name).GetValue(value);
        }

        private static string ExtractString(string json, string name)
        {
            var match = Regex.Match(
                json,
                "\\\"" + Regex.Escape(name) + "\\\"\\s*:\\s*\\\"(?<value>(?:\\\\.|[^\\\"])*)\\\"");
            if (!match.Success) return null;
            return match.Groups["value"].Value.Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        private static int ExtractInt(string json, string name)
        {
            var match = Regex.Match(
                json,
                "\\\"" + Regex.Escape(name) + "\\\"\\s*:\\s*(?<value>-?[0-9]+)");
            return match.Success ? int.Parse(match.Groups["value"].Value) : 0;
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class InitializeOnLoadAttribute : Attribute
    {
    }

    internal static class EditorApplication
    {
        internal static event Action update;
        internal static event Action quitting;
        internal static event Action delayCall;

        internal static void RaiseUpdate()
        {
            update?.Invoke();
        }

        internal static void RaiseDelayCall()
        {
            var callback = delayCall;
            delayCall = null;
            callback?.Invoke();
        }

        internal static void QueuePlayerLoopUpdate()
        {
        }
    }

    internal static class AssemblyReloadEvents
    {
        internal static event Action beforeAssemblyReload;
    }

    internal class EditorWindow
    {
        internal static T GetWindow<T>() where T : EditorWindow, new()
        {
            return new T();
        }

        internal void Show()
        {
        }

        internal void Focus()
        {
        }
    }

    internal sealed class AnimationWindow : EditorWindow
    {
        internal AnimationClip animationClip;
    }

    internal static class Selection
    {
        internal static UnityEngine.Object activeObject;
    }

    internal static class EditorGUIUtility
    {
        internal static void PingObject(UnityEngine.Object value)
        {
        }
    }

    internal static class AssetDatabase
    {
        internal static readonly ManualResetEventSlim OpenStarted = new(false);
        internal static readonly ManualResetEventSlim AllowOpen = new(false);
        internal static bool blockOpen;

        internal static void ResetOpenGate()
        {
            OpenStarted.Reset();
            AllowOpen.Reset();
            blockOpen = true;
        }

        internal static void ReleaseOpen()
        {
            blockOpen = false;
            AllowOpen.Set();
        }

        internal static T LoadAssetAtPath<T>(string path) where T : UnityEngine.Object
        {
            return (T)Activator.CreateInstance(typeof(T), true);
        }

        internal static bool OpenAsset(UnityEngine.Object value)
        {
            return CompleteOpen();
        }

        internal static bool OpenAsset(UnityEngine.Object value, int line)
        {
            return CompleteOpen();
        }

        internal static bool OpenAsset(UnityEngine.Object value, int line, int column)
        {
            return CompleteOpen();
        }

        private static bool CompleteOpen()
        {
            OpenStarted.Set();
            if (blockOpen) AllowOpen.Wait(TimeSpan.FromSeconds(5));
            return true;
        }
    }

    internal static class SettingsService
    {
        internal static EditorWindow OpenProjectSettings()
        {
            return new EditorWindow();
        }
    }
}

namespace UnityEditor.PackageManager.UI
{
    internal static class Window
    {
        internal static void Open(string packageToSelect)
        {
        }
    }
}
