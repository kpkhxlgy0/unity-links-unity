using System;
using System.Collections;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KPK.CodexUnityLink.Editor;
using UnityEditor;
using UnityEngine;

internal static class ReceiverReloadRegression
{
    private const int Iterations = 250;

    private static int Main()
    {
        Directory.CreateDirectory(Application.dataPath);
        var receiverType = Type.GetType(
            "KPK.CodexUnityLink.Editor.UnityAssetLinkReceiver, ReceiverReloadRegression",
            true);
        RuntimeHelpers.RunClassConstructor(receiverType.TypeHandle);

        var start = GetPrivateStaticMethod(receiverType, "Start");
        var stop = GetPrivateStaticMethod(receiverType, "Stop");
        var activePipe = receiverType.GetField(
            "activePipe",
            BindingFlags.NonPublic | BindingFlags.Static);
        var pendingErrors = receiverType.GetField(
            "PendingErrors",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (activePipe == null)
        {
            Console.Error.WriteLine("FAIL: activePipe field was not found.");
            return 1;
        }

        if (!WaitUntil(() => activePipe.GetValue(null) != null, TimeSpan.FromSeconds(2)))
        {
            WritePendingErrors(pendingErrors);
            Console.Error.WriteLine("FAIL: listener did not start for acknowledgement test.");
            return 1;
        }
        if (!AcknowledgesBeforeOpening(receiverType)) return 1;

        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            if (!WaitUntil(() => activePipe.GetValue(null) != null, TimeSpan.FromSeconds(2)))
            {
                WritePendingErrors(pendingErrors);
                Console.Error.WriteLine(
                    $"FAIL: listener did not start on iteration {iteration}.");
                return 1;
            }

            Thread.Sleep(10);
            stop.Invoke(null, null);
            start.Invoke(null, null);
        }

        if (!WaitUntil(() => activePipe.GetValue(null) != null, TimeSpan.FromSeconds(2)))
        {
            Console.Error.WriteLine("FAIL: final listener did not start.");
            return 1;
        }

        Thread.Sleep(10);
        stop.Invoke(null, null);
        Thread.Sleep(500);
        Console.WriteLine(
            $"PASS: listener stopped cleanly across {Iterations + 1} reload cycles.");
        return 0;
    }

    private static bool AcknowledgesBeforeOpening(Type receiverType)
    {
        var projectRoot = Directory.GetParent(Application.dataPath).FullName;
        var target = Path.Combine(Application.dataPath, "Slow.prefab");
        File.WriteAllText(target, "fixture");
        var pendingRequests = receiverType.GetField(
            "PendingRequests",
            BindingFlags.NonPublic | BindingFlags.Static);
        AssetDatabase.ResetOpenGate();
        Task updateTask = null;
        Task openTask = null;
        try
        {
            using (var pipe = new NamedPipeClientStream(
                       ".",
                       UnityAssetLinkPath.GetPipeName(projectRoot),
                       PipeDirection.InOut,
                       PipeOptions.Asynchronous))
            {
                pipe.Connect(2000);
                using (var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, true))
                using (var writer = new StreamWriter(
                           pipe,
                           new UTF8Encoding(false),
                           4096,
                           true) { AutoFlush = true })
                {
                    writer.WriteLine(
                        "{\"version\":1,\"requestId\":\"slow-open\",\"action\":\"openAsset\","
                        + "\"projectRoot\":\"" + EscapeJson(projectRoot) + "\","
                        + "\"assetPath\":\"Assets/Slow.prefab\",\"line\":0,\"column\":0}");
                    if (!WaitUntil(
                            () => QueueCount(pendingRequests) > 0,
                            TimeSpan.FromSeconds(2)))
                    {
                        Console.Error.WriteLine("FAIL: request did not reach the receiver queue.");
                        return false;
                    }

                    var responseTask = reader.ReadLineAsync();
                    updateTask = Task.Run(() => EditorApplication.RaiseUpdate());
                    if (!responseTask.Wait(500))
                    {
                        Console.Error.WriteLine("FAIL: Unity did not acknowledge before the blocked open completed.");
                        return false;
                    }
                    var response = responseTask.Result;
                    if (response == null
                        || !response.Contains("\"requestId\":\"slow-open\"")
                        || !response.Contains("\"code\":\"accepted\""))
                    {
                        Console.Error.WriteLine($"FAIL: unexpected acknowledgement: {response}");
                        return false;
                    }
                    if (AssetDatabase.OpenStarted.IsSet)
                    {
                        Console.Error.WriteLine("FAIL: asset opening started before acknowledgement was returned.");
                        return false;
                    }
                    if (!updateTask.Wait(500))
                    {
                        Console.Error.WriteLine("FAIL: acknowledgement update did not complete promptly.");
                        return false;
                    }

                    openTask = Task.Run(() =>
                    {
                        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
                        while (DateTime.UtcNow < deadline && !AssetDatabase.OpenStarted.IsSet)
                        {
                            EditorApplication.RaiseUpdate();
                            Thread.Sleep(1);
                        }
                    });
                    if (!AssetDatabase.OpenStarted.Wait(500))
                    {
                        Console.Error.WriteLine("FAIL: accepted asset was not opened on the next main-thread update.");
                        return false;
                    }
                    AssetDatabase.ReleaseOpen();
                    if (!openTask.Wait(2000))
                    {
                        Console.Error.WriteLine("FAIL: asset open did not complete on the next main-thread update.");
                        return false;
                    }
                }
            }
        }
        finally
        {
            AssetDatabase.ReleaseOpen();
            updateTask?.Wait(2000);
            openTask?.Wait(2000);
        }
        Console.WriteLine("PASS: receiver acknowledges before a slow Unity asset open.");
        return true;
    }

    private static int QueueCount(FieldInfo field)
    {
        var queue = field?.GetValue(null);
        var count = queue?.GetType().GetProperty("Count");
        return count != null ? (int)count.GetValue(queue) : 0;
    }

    private static string EscapeJson(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static MethodInfo GetPrivateStaticMethod(Type receiverType, string name)
    {
        var method = receiverType.GetMethod(
            name,
            BindingFlags.NonPublic | BindingFlags.Static);
        if (method == null)
            throw new MissingMethodException(receiverType.FullName, name);
        return method;
    }

    private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return true;
            Thread.Sleep(1);
        }
        return false;
    }

    private static void WritePendingErrors(FieldInfo pendingErrors)
    {
        if (!(pendingErrors?.GetValue(null) is IEnumerable values)) return;
        foreach (var value in values)
            Console.Error.WriteLine($"Listener error: {value}");
    }
}
