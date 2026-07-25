using System;
using System.IO;
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
            throw new NotSupportedException();
        }

        internal static string ToJson(object value)
        {
            throw new NotSupportedException();
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
        internal static T LoadAssetAtPath<T>(string path) where T : UnityEngine.Object
        {
            return null;
        }

        internal static bool OpenAsset(UnityEngine.Object value)
        {
            return true;
        }

        internal static bool OpenAsset(UnityEngine.Object value, int line)
        {
            return true;
        }

        internal static bool OpenAsset(UnityEngine.Object value, int line, int column)
        {
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
