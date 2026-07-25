using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
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
