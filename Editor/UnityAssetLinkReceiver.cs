using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace KPK.CodexUnityLink.Editor
{
    [InitializeOnLoad]
    internal static class UnityAssetLinkReceiver
    {
        private sealed class PendingRequest
        {
            internal string json;
            internal TaskCompletionSource<string> completion;
            internal TaskCompletionSource<bool> responseWritten;
        }

        private sealed class AcceptedRequest
        {
            internal UnityAssetLinkRequest request;
            internal string assetPath;
            internal Task<bool> responseWritten;
        }

        private static readonly ConcurrentQueue<PendingRequest> PendingRequests = new();
        private static readonly ConcurrentQueue<AcceptedRequest> PendingOpens = new();
        private static readonly ConcurrentQueue<string> PendingErrors = new();
        private static readonly object PipeGate = new();
        private static CancellationTokenSource cancellation;
        private static Task listenerTask;
        private static NamedPipeServerStream activePipe;
        private static string projectRoot;

        static UnityAssetLinkReceiver()
        {
            projectRoot = Directory.GetParent(Application.dataPath).FullName;
            EditorApplication.update += ProcessPendingRequests;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            Start();
            Debug.Log($"[CodexUnityLink] Listening for project: {projectRoot}");
        }

        private static void Start()
        {
            var source = new CancellationTokenSource();
            cancellation = source;
            var pipeName = UnityAssetLinkPath.GetPipeName(projectRoot);
            listenerTask = Task.Run(() => ListenAsync(pipeName, source.Token));
        }

        private static void Stop()
        {
            var source = cancellation;
            var task = listenerTask;
            if (source == null) return;
            cancellation = null;
            listenerTask = null;
            source.Cancel();
            lock (PipeGate)
            {
                if (activePipe != null)
                {
                    activePipe.Dispose();
                    activePipe = null;
                }
            }
            task?.GetAwaiter().GetResult();
            source.Dispose();
            while (PendingRequests.TryDequeue(out var pending))
            {
                pending.completion.TrySetCanceled();
            }
            while (PendingOpens.TryDequeue(out _))
            {
            }
            while (PendingErrors.TryDequeue(out _))
            {
            }
        }

        private static async Task ListenAsync(string pipeName, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using (var pipe = new NamedPipeServerStream(
                               pipeName,
                               PipeDirection.InOut,
                               1,
                               PipeTransmissionMode.Byte,
                               PipeOptions.Asynchronous))
                    {
                        lock (PipeGate)
                        {
                            activePipe = pipe;
                        }
                        while (!token.IsCancellationRequested)
                        {
                            await pipe.WaitForConnectionAsync(token);
                            try
                            {
                                await ServeAsync(pipe, token);
                            }
                            finally
                            {
                                if (pipe.IsConnected)
                                    pipe.Disconnect();
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    if (token.IsCancellationRequested) return;
                    if (!await DelayRetryAsync(token)) return;
                }
                catch (IOException)
                {
                    if (token.IsCancellationRequested) return;
                    if (!await DelayRetryAsync(token)) return;
                }
                catch (Exception exception)
                {
                    PendingErrors.Enqueue(exception.Message);
                    if (!await DelayRetryAsync(token)) return;
                }
                finally
                {
                    lock (PipeGate)
                    {
                        activePipe = null;
                    }
                }
            }
        }

        private static async Task<bool> DelayRetryAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(1000, token);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private static async Task ServeAsync(Stream stream, CancellationToken token)
        {
            using (var reader = new StreamReader(stream, Encoding.UTF8, false, 4096, true))
            using (var writer = new StreamWriter(
                       stream,
                       new UTF8Encoding(false),
                       4096,
                       true) { AutoFlush = true })
            {
                var json = await reader.ReadLineAsync();
                if (json == null) return;
                var completion = new TaskCompletionSource<string>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var pending = new PendingRequest
                {
                    json = json,
                    completion = completion,
                    responseWritten = new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously)
                };
                PendingRequests.Enqueue(pending);
                using (token.Register(() => completion.TrySetCanceled()))
                {
                    try
                    {
                        var response = await completion.Task;
                        await writer.WriteLineAsync(response);
                        await writer.FlushAsync();
                        pending.responseWritten.TrySetResult(true);
                    }
                    catch
                    {
                        pending.responseWritten.TrySetResult(false);
                        throw;
                    }
                }
            }
        }

        private static void ProcessPendingRequests()
        {
            while (PendingErrors.TryDequeue(out var message))
            {
                Debug.LogError($"[CodexUnityLink] Pipe listener failed: {message}");
            }
            while (PendingRequests.TryDequeue(out var pending))
            {
                try
                {
                    var response = AcceptRequest(pending.json, out var accepted);
                    pending.completion.TrySetResult(response);
                    if (accepted != null)
                    {
                        accepted.responseWritten = pending.responseWritten.Task;
                        PendingOpens.Enqueue(accepted);
                    }
                }
                catch (Exception exception)
                {
                    var response = UnityAssetLinkProtocol.Failure(
                        null,
                        "openFailed",
                        exception.Message);
                    pending.completion.TrySetResult(SerializeFailure(response));
                }
            }
            ProcessPendingOpens();
            if (!PendingOpens.IsEmpty) EditorApplication.QueuePlayerLoopUpdate();
        }

        private static string AcceptRequest(string json, out AcceptedRequest accepted)
        {
            accepted = null;
            if (!UnityAssetLinkProtocol.TryParse(json, out var request, out var error))
                return SerializeFailure(error);
            if (!UnityAssetLinkPath.TryResolveAsset(projectRoot, request, out var assetPath, out error))
                return SerializeFailure(error);

            accepted = new AcceptedRequest
            {
                request = request,
                assetPath = assetPath
            };
            return UnityAssetLinkProtocol.Serialize(
                UnityAssetLinkProtocol.Accepted(request.requestId));
        }

        private static void ProcessPendingOpens()
        {
            var pendingCount = PendingOpens.Count;
            for (var index = 0; index < pendingCount; index++)
            {
                if (!PendingOpens.TryDequeue(out var accepted)) return;
                if (!accepted.responseWritten.IsCompleted)
                {
                    PendingOpens.Enqueue(accepted);
                    continue;
                }
                if (!accepted.responseWritten.Result) continue;
                try
                {
                    OpenAcceptedRequest(accepted);
                }
                catch (Exception exception)
                {
                    SerializeFailure(UnityAssetLinkProtocol.Failure(
                        accepted.request.requestId,
                        "openFailed",
                        exception.Message));
                }
            }
        }

        private static void OpenAcceptedRequest(AcceptedRequest accepted)
        {
            var request = accepted.request;
            var assetPath = accepted.assetPath;
            bool opened;
            if (assetPath.StartsWith("ProjectSettings/", StringComparison.Ordinal))
            {
                opened = OpenProjectSettings();
            }
            else if (assetPath.StartsWith("Packages/", StringComparison.Ordinal))
            {
                opened = OpenPackageManager(assetPath);
            }
            else
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (asset == null)
                {
                    var error = UnityAssetLinkProtocol.Failure(
                        request.requestId,
                        "assetMissing",
                        "Unity could not load the requested asset.");
                    SerializeFailure(error);
                    return;
                }
                opened = OpenAsset(asset, request.line, request.column);
            }

            if (!opened)
            {
                var error = UnityAssetLinkProtocol.Failure(
                    request.requestId,
                    "openFailed",
                    "Unity did not accept the link open request.");
                SerializeFailure(error);
            }
        }

        private static bool OpenProjectSettings()
        {
            var window = SettingsService.OpenProjectSettings();
            if (window == null) return false;
            window.Show();
            window.Focus();
            return true;
        }

        private static bool OpenPackageManager(string assetPath)
        {
            UnityEditor.PackageManager.UI.Window.Open(GetPackageToSelect(assetPath));
            return true;
        }

        private static string GetPackageToSelect(string assetPath)
        {
            var segments = assetPath.Split('/');
            return segments.Length >= 3 ? segments[1] : null;
        }

        private static bool OpenAsset(UnityEngine.Object asset, int line, int column)
        {
            if (asset is AnimationClip clip) return OpenAnimationClip(clip);
            if (line <= 0) return AssetDatabase.OpenAsset(asset);
            if (column <= 0) return AssetDatabase.OpenAsset(asset, line);
            return AssetDatabase.OpenAsset(asset, line, column);
        }

        private static bool OpenAnimationClip(AnimationClip clip)
        {
            Selection.activeObject = clip;
            EditorGUIUtility.PingObject(clip);
            var window = EditorWindow.GetWindow<AnimationWindow>();
            window.animationClip = clip;
            window.Show();
            window.Focus();
            return window.animationClip == clip;
        }

        private static string SerializeFailure(UnityAssetLinkResponse response)
        {
            Debug.LogWarning($"[CodexUnityLink] {response.code}: {response.message}");
            return UnityAssetLinkProtocol.Serialize(response);
        }
    }
}
