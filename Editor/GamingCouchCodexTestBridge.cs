#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
internal static class GamingCouchCodexTestBridge
{
    private const string AppDataDirectoryName = "Gaming Couch";
    private const string BridgeDirectoryName = "CodexTestBridge";
    private const string SessionsDirectoryName = "sessions";
    private const string ManifestFileName = "session.json";
    private const string OutputsDirectoryName = "outputs";
    private const string RequestFileName = "request.json";
    private const string LastRequestIdEditorPrefsKey = "GamingCouch.CodexTestBridge.LastRequestId.";
    private const string SessionIdSessionStateKey = "GamingCouch.CodexTestBridge.SessionId.";
    private const string SessionTokenSessionStateKey = "GamingCouch.CodexTestBridge.SessionToken.";
    private const string RefreshedRequestIdSessionStateKey = "GamingCouch.CodexTestBridge.RefreshedRequestId.";
    private const double PollIntervalSeconds = 0.5d;
    private const uint PrivateDirectoryMode = 448; // 0700
    private const uint PrivateFileMode = 384; // 0600
    private const int MaxRetainedOutputFiles = 40;

    internal static readonly string ProjectPath = NormalizeProjectPath(Path.Combine(Application.dataPath, ".."));
    private static readonly string LocalAppDataDirectory = GetLocalAppDataDirectory();
    internal static readonly string BridgeRootDirectory = GetBridgeRootDirectory(ProjectPath);
    private static readonly string SessionId = LoadOrCreatePinnedSessionValue(SessionIdSessionStateKey, () => Guid.NewGuid().ToString("N"));
    private static readonly string SessionToken = LoadOrCreatePinnedSessionValue(SessionTokenSessionStateKey, CreateSessionToken);
    private static readonly string SessionsDirectory = Path.Combine(BridgeRootDirectory, SessionsDirectoryName);
    private static readonly string SessionDirectory = Path.Combine(SessionsDirectory, SessionId);
    internal static readonly string OutputDirectory = Path.Combine(SessionDirectory, OutputsDirectoryName);
    internal static readonly string RequestFilePath = Path.Combine(SessionDirectory, RequestFileName);
    private static readonly string ManifestPath = Path.Combine(BridgeRootDirectory, ManifestFileName);

    private static double nextPollTime;
    private static CodexTestRunCallbacks activeCallbacks;
    private static TestRunnerApi activeTestRunnerApi;
    private static bool bridgeSessionReady;

    static GamingCouchCodexTestBridge()
    {
        PrepareBridgeSession();
        EditorApplication.update -= PollForRequests;
        EditorApplication.update += PollForRequests;
    }

    private static void PrepareBridgeSession()
    {
        try
        {
            EnsureDirectoryTreeWithoutSymlinks(BridgeRootDirectory, LocalAppDataDirectory);
            EnsureDirectoryWithoutSymlink(SessionsDirectory);
            EnsureDirectoryWithoutSymlink(SessionDirectory);
            EnsureDirectoryWithoutSymlink(OutputDirectory);

            var session = new CodexTestBridgeSession
            {
                projectPath = ProjectPath,
                sessionId = SessionId,
                token = SessionToken,
                requestPath = RequestFilePath,
                outputDirectory = OutputDirectory,
                createdAtUtc = DateTime.UtcNow.ToString("o")
            };

            WriteFileAtomically(
                ManifestPath,
                JsonUtility.ToJson(session, true),
                BridgeRootDirectory
            );
            TryPruneOutputs();
            bridgeSessionReady = true;
            Debug.Log("Gaming Couch Codex test bridge session ready at " + RequestFilePath + ".");
        }
        catch (Exception exception)
        {
            bridgeSessionReady = false;
            Debug.LogError("Gaming Couch Codex test bridge disabled because session setup failed: " + exception);
        }
    }

    private static void PollForRequests()
    {
        if (EditorApplication.timeSinceStartup < nextPollTime)
        {
            return;
        }

        nextPollTime = EditorApplication.timeSinceStartup + PollIntervalSeconds;

        if (!bridgeSessionReady || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(RequestFilePath))
        {
            return;
        }

        if (!IsSafeBridgeFile(RequestFilePath, SessionDirectory, out var unsafeRequestReason))
        {
            Debug.LogWarning("Gaming Couch Codex test bridge ignored an unsafe request file: " + unsafeRequestReason);
            return;
        }

        CodexTestRequest request;
        try
        {
            request = JsonUtility.FromJson<CodexTestRequest>(File.ReadAllText(RequestFilePath));
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Gaming Couch Codex test bridge ignored an unreadable request: " + exception.Message);
            return;
        }

        if (request == null || !IsValidRequestId(request.requestId))
        {
            return;
        }

        if (!IsForThisProject(request))
        {
            return;
        }

        if (!HasSessionToken(request.token))
        {
            Debug.LogWarning("Gaming Couch Codex test bridge ignored a request with an invalid session token.");
            return;
        }

        if (HasHandledRequest(request.requestId))
        {
            return;
        }

        // Recompile edited scripts before running so the run reflects on-disk changes without the
        // user having to focus the Editor first. AssetDatabase.Refresh() imports changed assets and,
        // when scripts changed, triggers a compile + domain reload. The request file is left in place
        // and the run is not yet marked handled, so the reloaded bridge -- which reuses the same
        // SessionState-pinned session id/token and therefore the same manifest paths -- re-enters
        // here, sees the refresh was already initiated for this request id, and runs against the
        // freshly compiled assemblies. When nothing changed, Refresh() is a no-op and the next poll
        // proceeds straight to the run. Callers can opt out with skipRefresh (see --no-refresh).
        if (!request.skipRefresh && !string.Equals(GetRefreshedRequestId(), request.requestId, StringComparison.Ordinal))
        {
            SetRefreshedRequestId(request.requestId);
            WriteStatus(CodexTestStatus.Refreshing(request));
            AssetDatabase.Refresh();
            return;
        }

        MarkHandledRequest(request.requestId);
        ClearRefreshedRequestId();
        TryDeleteRequestFile();

        if (activeCallbacks != null)
        {
            WriteStatus(CodexTestStatus.Rejected(request, "A Unity test run is already active."));
            return;
        }

        StartRun(request);
    }

    private static bool IsForThisProject(CodexTestRequest request)
    {
        return IsProjectPathForThisProject(request.projectPath);
    }

    internal static bool IsProjectPathForThisProject(string projectPath)
    {
        return !string.IsNullOrWhiteSpace(projectPath)
            && string.Equals(
                NormalizeProjectPath(projectPath),
                ProjectPath,
                PathComparison
            );
    }

    internal static string GetDefaultOutputPath(string requestId, string extension)
    {
        if (!IsValidRequestId(requestId))
        {
            throw new ArgumentException("Request id must be a GUID nonce without separators.");
        }

        var fullPath = NormalizeProjectPath(Path.Combine(OutputDirectory, "gaming-couch-unity-test-" + requestId + "." + extension));
        if (!IsPathInsideDirectory(fullPath, OutputDirectory))
        {
            throw new ArgumentException("Output path must stay inside the bridge session output directory.");
        }
        return fullPath;
    }

    internal static bool IsValidRequestId(string requestId)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            return false;
        }

        Guid parsed;
        return Guid.TryParseExact(requestId, "N", out parsed);
    }

    private static bool HasSessionToken(string token)
    {
        return ConstantTimeEquals(token, SessionToken);
    }

    private static bool HasHandledRequest(string requestId)
    {
        return string.Equals(
            EditorPrefs.GetString(GetLastRequestIdKey(), string.Empty),
            requestId,
            StringComparison.Ordinal
        );
    }

    private static void MarkHandledRequest(string requestId)
    {
        EditorPrefs.SetString(GetLastRequestIdKey(), requestId);
    }

    private static string GetLastRequestIdKey()
    {
        return LastRequestIdEditorPrefsKey + ProjectPath;
    }

    // Tracks which request id has already had its pre-run AssetDatabase.Refresh() kicked off, stored
    // in SessionState so it survives the domain reload the refresh may trigger. On re-entry after the
    // reload the id matches and the bridge skips straight to the run instead of refreshing again (which
    // would loop forever). Cleared once the run actually starts.
    private static string GetRefreshedRequestId()
    {
        return SessionState.GetString(RefreshedRequestIdSessionStateKey + ProjectPath, string.Empty);
    }

    private static void SetRefreshedRequestId(string requestId)
    {
        SessionState.SetString(RefreshedRequestIdSessionStateKey + ProjectPath, requestId ?? string.Empty);
    }

    private static void ClearRefreshedRequestId()
    {
        SessionState.SetString(RefreshedRequestIdSessionStateKey + ProjectPath, string.Empty);
    }

    private static void StartRun(CodexTestRequest request)
    {
        try
        {
            var settings = CreateExecutionSettings(request);
            activeTestRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            activeTestRunnerApi.hideFlags = HideFlags.HideAndDontSave;

            activeCallbacks = ScriptableObject.CreateInstance<CodexTestRunCallbacks>();
            activeCallbacks.hideFlags = HideFlags.HideAndDontSave;
            activeCallbacks.Initialize(request);

            activeTestRunnerApi.RegisterCallbacks(activeCallbacks);
            RunAndTrackStatus(
                request,
                () => activeTestRunnerApi.Execute(settings),
                WriteStatus,
                () => activeCallbacks != null,
                jobId => activeCallbacks.SetJobId(jobId)
            );
            Debug.Log("Gaming Couch Codex test bridge started " + request.GetDisplayMode() + " tests.");
        }
        catch (Exception exception)
        {
            WriteStatus(CodexTestStatus.Error(request, exception.Message));
            CleanupActiveRun();
            Debug.LogError("Gaming Couch Codex test bridge failed to start tests: " + exception);
        }
    }

    // Orchestrates the status writes around Execute. A synchronous EditMode run
    // (ExecutionSettings.runSynchronously) fires RunFinished -> CompleteRun, which writes the
    // terminal status and clears the active run, all *before* Execute returns. Writing "started"
    // BEFORE Execute means that terminal write lands last and is never clobbered back to
    // "started" (which made the Python harness time out on a successful run). The job id is only
    // known after Execute returns, so it is filled in afterwards for asynchronous runs; a
    // synchronous run has already completed and cleaned up (isRunActive == false) by then, so its
    // terminal status is left as the last write. Extracted behind delegates so tests can simulate
    // a synchronous Execute without a live Editor. See remediation Task 1.
    internal static void RunAndTrackStatus(
        CodexTestRequest request,
        Func<string> execute,
        Action<CodexTestStatus> writeStatus,
        Func<bool> isRunActive,
        Action<string> setJobId
    )
    {
        writeStatus(CodexTestStatus.Started(request, null));

        var jobId = execute();

        if (isRunActive())
        {
            setJobId(jobId);
            writeStatus(CodexTestStatus.Started(request, jobId));
        }
    }

    private static ExecutionSettings CreateExecutionSettings(CodexTestRequest request)
    {
        var filter = new Filter
        {
            testMode = ParseTestMode(request.testMode),
            testNames = CleanStrings(request.testNames),
            groupNames = CleanStrings(request.groupNames),
            categoryNames = CleanStrings(request.categoryNames),
            assemblyNames = CleanStrings(request.assemblyNames)
        };

        var settings = new ExecutionSettings(filter);
        if (request.runSynchronously && filter.testMode == TestMode.EditMode)
        {
            settings.runSynchronously = true;
        }

        return settings;
    }

    private static TestMode ParseTestMode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return TestMode.EditMode;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "edit":
            case "editor":
            case "editmode":
                return TestMode.EditMode;
            case "play":
            case "playmode":
                return TestMode.PlayMode;
            case "all":
            case "both":
                return TestMode.EditMode | TestMode.PlayMode;
            default:
                throw new ArgumentException("Unsupported test mode '" + value + "'. Use EditMode, PlayMode, or All.");
        }
    }

    private static string[] CleanStrings(string[] values)
    {
        if (values == null)
        {
            return null;
        }

        var cleaned = new List<string>();
        for (var index = 0; index < values.Length; index++)
        {
            var value = values[index];
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            cleaned.Add(value.Trim());
        }

        return cleaned.Count == 0 ? null : cleaned.ToArray();
    }

    private static void CompleteRun(CodexTestRunCallbacks callbacks, ITestResultAdaptor result)
    {
        if (callbacks != activeCallbacks)
        {
            return;
        }

        try
        {
            var request = callbacks.Request;
            var resultsPath = request.GetResultsPath();
            EnsureOutputParentDirectory(resultsPath);
            TestRunnerApi.SaveResultToFile(result, resultsPath);

            var status = CodexTestStatus.Finished(request, callbacks.JobId, result, resultsPath);
            WriteStatus(status);
            TryPruneOutputs();
            Debug.Log("Gaming Couch Codex test bridge finished " + request.GetDisplayMode() + " tests with " + status.state + ".");
        }
        catch (Exception exception)
        {
            WriteStatus(CodexTestStatus.Error(callbacks.Request, exception.Message));
            Debug.LogError("Gaming Couch Codex test bridge failed while saving test results: " + exception);
        }
        finally
        {
            CleanupActiveRun();
        }
    }

    private static void FailRun(CodexTestRunCallbacks callbacks, string message)
    {
        if (callbacks != activeCallbacks)
        {
            return;
        }

        WriteStatus(CodexTestStatus.Error(callbacks.Request, message));
        CleanupActiveRun();
    }

    private static void CleanupActiveRun()
    {
        if (activeTestRunnerApi != null && activeCallbacks != null)
        {
            activeTestRunnerApi.UnregisterCallbacks(activeCallbacks);
        }

        if (activeCallbacks != null)
        {
            UnityEngine.Object.DestroyImmediate(activeCallbacks);
            activeCallbacks = null;
        }

        if (activeTestRunnerApi != null)
        {
            UnityEngine.Object.DestroyImmediate(activeTestRunnerApi);
            activeTestRunnerApi = null;
        }
    }

    private static void WriteStatus(CodexTestStatus status)
    {
        var statusPath = status.GetStatusPath();
        EnsureOutputParentDirectory(statusPath);
        WriteFileAtomically(statusPath, JsonUtility.ToJson(status, true), OutputDirectory);
    }

    private static void EnsureOutputParentDirectory(string path)
    {
        var fullPath = NormalizeProjectPath(path);
        if (!IsPathInsideDirectory(fullPath, OutputDirectory))
        {
            throw new ArgumentException("Output path must stay inside the bridge session output directory.");
        }

        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new ArgumentException("Output path must include a parent directory.");
        }

        EnsureDirectoryTreeWithoutSymlinks(directory, OutputDirectory);
        RejectExistingSymlinksInPath(fullPath, OutputDirectory, true);
    }

    private static string NormalizeProjectPath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    internal static string GetBridgeRootDirectory(string projectPath)
    {
        return Path.Combine(
            LocalAppDataDirectory,
            AppDataDirectoryName,
            BridgeDirectoryName,
            ComputeProjectHash(NormalizeProjectPath(projectPath))
        );
    }

    private static string GetLocalAppDataDirectory()
    {
        if (Application.platform == RuntimePlatform.WindowsEditor)
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local",
            "share"
        );
    }

    internal static bool IsPathInsideDirectory(string path, string directory)
    {
        var fullPath = NormalizeProjectPath(path);
        var fullDirectory = NormalizeProjectPath(directory);
        if (string.Equals(fullPath, fullDirectory, PathComparison))
        {
            return true;
        }

        var directoryWithSeparator = fullDirectory + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(directoryWithSeparator, PathComparison);
    }

    private static bool IsSafeBridgeFile(string path, string containingDirectory, out string reason)
    {
        reason = null;
        try
        {
            if (!IsPathInsideDirectory(path, containingDirectory))
            {
                reason = "request file is outside the bridge session directory";
                return false;
            }

            RejectExistingSymlinksInPath(path, containingDirectory, true);
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.Directory) != 0)
            {
                reason = "request path is a directory";
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            reason = exception.Message;
            return false;
        }
    }

    private static void EnsureDirectoryWithoutSymlink(string directory)
    {
        if (File.Exists(directory) && !Directory.Exists(directory))
        {
            throw new IOException("Expected a directory but found a file at " + directory + ".");
        }

        if (Directory.Exists(directory))
        {
            RejectSymlink(directory);
            RestrictUnixPermissions(directory, PrivateDirectoryMode);
            return;
        }

        Directory.CreateDirectory(directory);
        RejectSymlink(directory);
        RestrictUnixPermissions(directory, PrivateDirectoryMode);
    }

    private static void EnsureDirectoryTreeWithoutSymlinks(string directory, string rootDirectory)
    {
        var fullDirectory = NormalizeProjectPath(directory);
        var fullRootDirectory = NormalizeProjectPath(rootDirectory);
        if (!IsPathInsideDirectory(fullDirectory, fullRootDirectory))
        {
            throw new ArgumentException("Directory must stay inside the bridge session directory.");
        }

        EnsureDirectoryWithoutSymlink(fullRootDirectory);
        if (string.Equals(fullDirectory, fullRootDirectory, PathComparison))
        {
            return;
        }

        var remainder = fullDirectory.Substring(fullRootDirectory.Length)
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var current = fullRootDirectory;
        foreach (var segment in remainder.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            EnsureDirectoryWithoutSymlink(current);
        }
    }

    private static void RejectExistingSymlinksInPath(string path, string rootDirectory, bool includeLeaf)
    {
        var fullPath = NormalizeProjectPath(path);
        var fullRootDirectory = NormalizeProjectPath(rootDirectory);
        if (!IsPathInsideDirectory(fullPath, fullRootDirectory))
        {
            throw new ArgumentException("Path must stay inside the bridge session directory.");
        }

        RejectSymlink(fullRootDirectory);
        var pathToCheck = includeLeaf ? fullPath : Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(pathToCheck) || string.Equals(pathToCheck, fullRootDirectory, PathComparison))
        {
            return;
        }

        var remainder = pathToCheck.Substring(fullRootDirectory.Length)
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var current = fullRootDirectory;
        foreach (var segment in remainder.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (File.Exists(current) || Directory.Exists(current))
            {
                RejectSymlink(current);
            }
        }
    }

    private static void RejectSymlink(string path)
    {
        if (IsSymlink(path))
        {
            throw new IOException("Refusing to use symlinked bridge path " + path + ".");
        }
    }

    private static bool IsSymlink(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    private static void RestrictUnixPermissions(string path, uint mode)
    {
        if (Application.platform == RuntimePlatform.WindowsEditor)
        {
            return;
        }

        if (Chmod(path, mode) != 0)
        {
            throw new IOException("Failed to restrict bridge path permissions for " + path + ": errno " + Marshal.GetLastWin32Error() + ".");
        }
    }

    private static void WriteFileAtomically(string path, string contents, string containingDirectory)
    {
        var fullPath = NormalizeProjectPath(path);
        if (!IsPathInsideDirectory(fullPath, containingDirectory))
        {
            throw new ArgumentException("File path must stay inside the bridge session directory.");
        }

        var parentDirectory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(parentDirectory))
        {
            throw new ArgumentException("File path must include a parent directory.");
        }

        EnsureDirectoryTreeWithoutSymlinks(parentDirectory, containingDirectory);
        RejectExistingSymlinksInPath(fullPath, containingDirectory, true);

        var tempPath = Path.Combine(parentDirectory, Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            RejectExistingSymlinksInPath(tempPath, containingDirectory, true);
            File.WriteAllText(tempPath, contents);
            RestrictUnixPermissions(tempPath, PrivateFileMode);
            RejectExistingSymlinksInPath(fullPath, containingDirectory, true);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            File.Move(tempPath, fullPath);
            RestrictUnixPermissions(fullPath, PrivateFileMode);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static void TryDeleteRequestFile()
    {
        try
        {
            DeleteBridgeFile(RequestFilePath, SessionDirectory);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Gaming Couch Codex test bridge could not delete a handled request file: " + exception.Message);
        }
    }

    private static void TryPruneOutputs()
    {
        try
        {
            PruneOutputs(OutputDirectory, MaxRetainedOutputFiles);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Gaming Couch Codex test bridge could not prune old output files: " + exception.Message);
        }
    }

    // Deletes a single bridge-owned file after re-confirming it stays inside the confining
    // directory and that no path segment (including the leaf) is a symlink. Mirrors the
    // symlink-safe deletion WriteFileAtomically performs before File.Move, so cleanup never
    // follows an attacker-planted link out of the session tree.
    private static void DeleteBridgeFile(string path, string containingDirectory)
    {
        var fullPath = NormalizeProjectPath(path);
        if (!IsPathInsideDirectory(fullPath, containingDirectory))
        {
            throw new ArgumentException("File path must stay inside the bridge session directory.");
        }

        if (!File.Exists(fullPath))
        {
            return;
        }

        RejectExistingSymlinksInPath(fullPath, containingDirectory, true);
        File.Delete(fullPath);
    }

    // Bounds the number of files retained directly inside a bridge output directory, keeping the
    // most recently written ones and deleting the rest through the symlink-safe, confined
    // DeleteBridgeFile helper. Extracted so the retention policy is unit-testable without a live
    // Editor session. See remediation Task 8.
    internal static void PruneOutputs(string directory, int maxCount)
    {
        if (maxCount < 0 || string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return;
        }

        var files = new DirectoryInfo(directory).GetFiles();
        if (files.Length <= maxCount)
        {
            return;
        }

        Array.Sort(files, (left, right) => right.LastWriteTimeUtc.CompareTo(left.LastWriteTimeUtc));
        for (var index = maxCount; index < files.Length; index++)
        {
            DeleteBridgeFile(files[index].FullName, directory);
        }
    }

    private static string CreateSessionToken()
    {
        return Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    }

    // Pins the session id and token to the Editor process rather than the loaded C# domain, so they
    // survive the domain reload a pre-run recompile triggers. Without this, [InitializeOnLoad] would
    // mint a fresh id/token on every reload and rewrite the manifest, orphaning a request the Python
    // runner already wrote against the previous manifest paths. SessionState lives for the Editor
    // launch and clears on restart -- the right lifetime, since the runner re-reads the manifest on
    // every invocation.
    private static string LoadOrCreatePinnedSessionValue(string keyPrefix, Func<string> factory)
    {
        var key = keyPrefix + ProjectPath;
        var existing = SessionState.GetString(key, string.Empty);
        if (!string.IsNullOrEmpty(existing))
        {
            return existing;
        }

        var created = factory();
        SessionState.SetString(key, created);
        return created;
    }

    private static bool ConstantTimeEquals(string actual, string expected)
    {
        if (string.IsNullOrEmpty(actual) || string.IsNullOrEmpty(expected))
        {
            return false;
        }

        var actualBytes = Encoding.UTF8.GetBytes(actual);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var difference = actualBytes.Length ^ expectedBytes.Length;
        var length = Math.Min(actualBytes.Length, expectedBytes.Length);
        for (var index = 0; index < length; index++)
        {
            difference |= actualBytes[index] ^ expectedBytes[index];
        }

        return difference == 0;
    }

    private static string ComputeProjectHash(string projectPath)
    {
        using (var sha256 = SHA256.Create())
        {
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(projectPath));
            var builder = new StringBuilder();
            for (var index = 0; index < 16; index++)
            {
                builder.Append(bytes[index].ToString("x2"));
            }

            return builder.ToString();
        }
    }

    private static StringComparison PathComparison
    {
        get
        {
            return Application.platform == RuntimePlatform.WindowsEditor
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
        }
    }

    [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
    private static extern int Chmod(string path, uint mode);

#pragma warning disable CS0649
    [Serializable]
    private sealed class CodexTestBridgeSession
    {
        public string projectPath;
        public string sessionId;
        public string token;
        public string requestPath;
        public string outputDirectory;
        public string createdAtUtc;
    }

    [Serializable]
    internal sealed class CodexTestRequest
    {
        public string requestId;
        public string token;
        public string projectPath;
        public string testMode;
        public string[] testNames;
        public string[] groupNames;
        public string[] categoryNames;
        public string[] assemblyNames;
        public bool runSynchronously;

        // Opt out of the pre-run AssetDatabase.Refresh(). Defaults to false so a request that omits
        // the field (older runners) still refreshes; the Python runner sets it via --no-refresh.
        public bool skipRefresh;

        public string GetDisplayMode()
        {
            return string.IsNullOrWhiteSpace(testMode) ? "EditMode" : testMode;
        }

        public string GetResultsPath()
        {
            return GetDefaultOutputPath(requestId, "xml");
        }

        public string GetStatusPath()
        {
            return GetDefaultOutputPath(requestId, "json");
        }
    }
#pragma warning restore CS0649

    private sealed class CodexTestRunCallbacks : ScriptableObject, IErrorCallbacks
    {
        public CodexTestRequest Request { get; private set; }
        public string JobId { get; private set; }

        public void Initialize(CodexTestRequest request)
        {
            Request = request;
        }

        public void SetJobId(string jobId)
        {
            JobId = jobId;
        }

        public void RunStarted(ITestAdaptor testsToRun)
        {
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            CompleteRun(this, result);
        }

        public void TestStarted(ITestAdaptor test)
        {
        }

        public void TestFinished(ITestResultAdaptor result)
        {
        }

        public void OnError(string message)
        {
            FailRun(this, message);
        }
    }

    [Serializable]
    internal sealed class CodexTestStatus
    {
        public string requestId;
        public string projectPath;
        public string state;
        public string testMode;
        public string jobId;
        public string message;
        public string resultsPath;
        public string statusPath;
        public string startedAtUtc;
        public string finishedAtUtc;
        public int passCount;
        public int failCount;
        public int skipCount;
        public int inconclusiveCount;
        public int assertCount;
        public double duration;
        public CodexTestFailure[] failures;

        public static CodexTestStatus Refreshing(CodexTestRequest request)
        {
            return FromRequest(request, "refreshing", "Refreshing assets and recompiling before the run.", null);
        }

        public static CodexTestStatus Started(CodexTestRequest request, string jobId)
        {
            return FromRequest(request, "started", "Unity accepted the test request.", jobId);
        }

        public static CodexTestStatus Rejected(CodexTestRequest request, string message)
        {
            return FromRequest(request, "rejected", message, null);
        }

        public static CodexTestStatus Error(CodexTestRequest request, string message)
        {
            return FromRequest(request, "error", message, null);
        }

        public static CodexTestStatus Finished(
            CodexTestRequest request,
            string jobId,
            ITestResultAdaptor result,
            string resultsPath
        )
        {
            var status = FromRequest(
                request,
                result.FailCount == 0 && result.TestStatus != TestStatus.Failed ? "completed" : "failed",
                result.ResultState,
                jobId
            );
            status.resultsPath = resultsPath;
            status.finishedAtUtc = DateTime.UtcNow.ToString("o");
            status.passCount = result.PassCount;
            status.failCount = result.FailCount;
            status.skipCount = result.SkipCount;
            status.inconclusiveCount = result.InconclusiveCount;
            status.assertCount = result.AssertCount;
            status.duration = result.Duration;
            status.failures = GetFailures(result).ToArray();
            return status;
        }

        public string GetStatusPath()
        {
            if (!string.IsNullOrWhiteSpace(statusPath))
            {
                return statusPath;
            }

            return GetDefaultOutputPath(requestId, "json");
        }

        private static CodexTestStatus FromRequest(
            CodexTestRequest request,
            string state,
            string message,
            string jobId
        )
        {
            return new CodexTestStatus
            {
                requestId = request.requestId,
                projectPath = ProjectPath,
                state = state,
                testMode = request.GetDisplayMode(),
                jobId = jobId,
                message = message,
                resultsPath = request.GetResultsPath(),
                statusPath = request.GetStatusPath(),
                startedAtUtc = DateTime.UtcNow.ToString("o"),
                failures = new CodexTestFailure[0]
            };
        }

        private static List<CodexTestFailure> GetFailures(ITestResultAdaptor result)
        {
            var failures = new List<CodexTestFailure>();
            AddFailures(result, failures);
            return failures;
        }

        private static void AddFailures(ITestResultAdaptor result, List<CodexTestFailure> failures)
        {
            if (result == null)
            {
                return;
            }

            if (result.TestStatus == TestStatus.Failed && (result.Test == null || !result.Test.IsSuite))
            {
                failures.Add(new CodexTestFailure
                {
                    fullName = result.FullName,
                    message = result.Message,
                    stackTrace = result.StackTrace
                });
            }

            if (!result.HasChildren)
            {
                return;
            }

            foreach (var child in result.Children)
            {
                AddFailures(child, failures);
            }
        }
    }

    [Serializable]
    internal sealed class CodexTestFailure
    {
        public string fullName;
        public string message;
        public string stackTrace;
    }
}
#endif
