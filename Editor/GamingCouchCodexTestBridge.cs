#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
internal static class GamingCouchCodexTestBridge
{
    internal const string RequestFilePath = "/tmp/gaming-couch-unity-codex-test-request.json";

    private const string LastRequestIdEditorPrefsKey = "GamingCouch.CodexTestBridge.LastRequestId.";
    private const double PollIntervalSeconds = 0.5d;

    private static readonly string ProjectPath = NormalizeProjectPath(Path.Combine(Application.dataPath, ".."));

    private static double nextPollTime;
    private static CodexTestRunCallbacks activeCallbacks;
    private static TestRunnerApi activeTestRunnerApi;

    static GamingCouchCodexTestBridge()
    {
        EditorApplication.update -= PollForRequests;
        EditorApplication.update += PollForRequests;
    }

    [MenuItem("GamingCouch/Codex Test Bridge/Log Request Path")]
    private static void LogRequestPath()
    {
        Debug.Log("Gaming Couch Codex test bridge request path: " + RequestFilePath);
    }

    private static void PollForRequests()
    {
        if (EditorApplication.timeSinceStartup < nextPollTime)
        {
            return;
        }

        nextPollTime = EditorApplication.timeSinceStartup + PollIntervalSeconds;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(RequestFilePath))
        {
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

        if (request == null || string.IsNullOrWhiteSpace(request.requestId))
        {
            return;
        }

        if (!IsForThisProject(request))
        {
            return;
        }

        if (HasHandledRequest(request.requestId))
        {
            return;
        }

        MarkHandledRequest(request.requestId);

        if (activeCallbacks != null)
        {
            WriteStatus(CodexTestStatus.Rejected(request, "A Unity test run is already active."));
            return;
        }

        StartRun(request);
    }

    private static bool IsForThisProject(CodexTestRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.projectPath))
        {
            return true;
        }

        return string.Equals(
            NormalizeProjectPath(request.projectPath),
            ProjectPath,
            StringComparison.Ordinal
        );
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
            var jobId = activeTestRunnerApi.Execute(settings);
            activeCallbacks.SetJobId(jobId);

            WriteStatus(CodexTestStatus.Started(request, jobId));
            Debug.Log("Gaming Couch Codex test bridge started " + request.GetDisplayMode() + " tests.");
        }
        catch (Exception exception)
        {
            WriteStatus(CodexTestStatus.Error(request, exception.Message));
            CleanupActiveRun();
            Debug.LogError("Gaming Couch Codex test bridge failed to start tests: " + exception);
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
            EnsureParentDirectory(resultsPath);
            TestRunnerApi.SaveResultToFile(result, resultsPath);

            var status = CodexTestStatus.Finished(request, callbacks.JobId, result, resultsPath);
            WriteStatus(status);
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
        EnsureParentDirectory(statusPath);
        File.WriteAllText(statusPath, JsonUtility.ToJson(status, true));
    }

    private static void EnsureParentDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static string NormalizeProjectPath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

#pragma warning disable CS0649
    [Serializable]
    private sealed class CodexTestRequest
    {
        public string requestId;
        public string projectPath;
        public string testMode;
        public string[] testNames;
        public string[] groupNames;
        public string[] categoryNames;
        public string[] assemblyNames;
        public string resultsPath;
        public string statusPath;
        public bool runSynchronously;

        public string GetDisplayMode()
        {
            return string.IsNullOrWhiteSpace(testMode) ? "EditMode" : testMode;
        }

        public string GetResultsPath()
        {
            if (!string.IsNullOrWhiteSpace(resultsPath))
            {
                return resultsPath;
            }

            return Path.Combine(Path.GetTempPath(), "gaming-couch-unity-test-" + requestId + ".xml");
        }

        public string GetStatusPath()
        {
            if (!string.IsNullOrWhiteSpace(statusPath))
            {
                return statusPath;
            }

            return Path.Combine(Path.GetTempPath(), "gaming-couch-unity-test-" + requestId + ".json");
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
    private sealed class CodexTestStatus
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

            return Path.Combine(Path.GetTempPath(), "gaming-couch-unity-test-" + requestId + ".json");
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
    private sealed class CodexTestFailure
    {
        public string fullName;
        public string message;
        public string stackTrace;
    }
}
#endif
