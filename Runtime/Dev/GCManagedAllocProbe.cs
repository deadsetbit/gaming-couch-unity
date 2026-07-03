#if ENABLE_PROFILER
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace DSB.GC.Dev
{
    /// <summary>
    /// Drop-in scene probe that logs managed GC allocations per frame, for the
    /// quick-wins "profiler pass". Attach it to any GameObject in a scene, enter
    /// Play mode (editor) or run a Development build with the Profiler connected,
    /// and it prints a rolling summary of bytes allocated on the managed heap
    /// per frame.
    ///
    /// Methodology: "GC Allocated In Frame" is a PROCESS-WIDE counter (engine +
    /// every script + this package), not package-only. The signal you want is
    /// the BEFORE/AFTER delta: run the SAME scenario at the base commit and at
    /// HEAD and compare avg/max B/frame. For package-only attribution, use the
    /// Profiler window's CPU module GC.Alloc call-stack view instead — this probe
    /// gives the aggregate number, the delta is what's attributable to a change.
    ///
    /// Scenarios worth labelling (one probe per run, change <see cref="scenarioLabel"/>):
    ///   - "idle"              : playing, no player state changing  -> target ~0 B/frame after P0
    ///   - "screen_space-off"  : screen_space disabled but QueuePointData still called each frame
    ///   - "meter-animating"   : a meter changing every frame (worst case; P1 territory)
    ///
    /// Compiled only when ENABLE_PROFILER is defined (editor + Development builds),
    /// so it is stripped from release builds and adds zero shipping overhead.
    /// Remove it from any scene you ship, or keep it in a dev-only scene.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("GamingCouch/Dev/Managed Alloc Probe")]
    public sealed class GCManagedAllocProbe : MonoBehaviour
    {
        [Tooltip("Free-text tag printed with each summary so you can label the scenario, e.g. \"idle\", \"meter-animating\", \"screen_space-off\".")]
        [SerializeField] private string scenarioLabel = "unlabeled";

        [Tooltip("Seconds between summary log lines. The log frame itself allocates the summary string, so keep this coarse (>= ~0.5s) to avoid polluting the idle measurement.")]
        [SerializeField] private float logIntervalSeconds = 1f;

        [Tooltip("Warn on any single frame whose managed allocation reaches this many bytes (0 = disabled). Handy for catching one fat allocation among quiet frames.")]
        [SerializeField] private long spikeWarnBytes = 0;

        private ProfilerRecorder _gcPerFrame;

        private float _windowStart;
        private long _windowFrames;
        private long _windowTotalBytes;
        private long _windowMaxBytes;
        private long _windowNonZeroFrames;

        private readonly StringBuilder _log = new StringBuilder(192);

        private void OnEnable()
        {
            _gcPerFrame = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            ResetWindow();

            if (!_gcPerFrame.Valid)
            {
                Debug.LogWarning(
                    "[GCManagedAllocProbe] 'GC Allocated In Frame' counter is unavailable. " +
                    "It is only produced in the editor or a Development build " +
                    "(Build Settings > Development Build). Aggregate numbers will read 0.");
            }
        }

        private void OnDisable()
        {
            // Safe to call on a default/invalid recorder.
            _gcPerFrame.Dispose();
        }

        private void Update()
        {
            if (!_gcPerFrame.Valid)
            {
                return;
            }

            // LastValue is the previous completed frame's managed allocation in bytes.
            var bytesThisFrame = _gcPerFrame.LastValue;

            _windowFrames++;
            _windowTotalBytes += bytesThisFrame;
            if (bytesThisFrame > _windowMaxBytes)
            {
                _windowMaxBytes = bytesThisFrame;
            }
            if (bytesThisFrame > 0)
            {
                _windowNonZeroFrames++;
            }

            if (spikeWarnBytes > 0 && bytesThisFrame >= spikeWarnBytes)
            {
                Debug.LogWarning($"[GCManagedAllocProbe:{scenarioLabel}] frame allocation spike: {bytesThisFrame} B");
            }

            var elapsed = Time.unscaledTime - _windowStart;
            if (elapsed >= logIntervalSeconds && _windowFrames > 0)
            {
                LogWindow(elapsed);
                ResetWindow();
            }
        }

        private void LogWindow(float elapsedSeconds)
        {
            var avgBytesPerFrame = (double)_windowTotalBytes / _windowFrames;
            var allocatingPct = 100.0 * _windowNonZeroFrames / _windowFrames;

            // The only allocation this probe makes, once per interval — ignore it
            // when reading the idle number, or watch it appear on the log frame.
            _log.Clear();
            _log.Append("[GCManagedAllocProbe:").Append(scenarioLabel).Append("] ")
                .Append(_windowFrames).Append(" frames / ").Append(elapsedSeconds.ToString("F2")).Append("s  |  ")
                .Append("avg ").Append(avgBytesPerFrame.ToString("F0")).Append(" B/frame  |  ")
                .Append("max ").Append(_windowMaxBytes).Append(" B  |  ")
                .Append("allocating ").Append(_windowNonZeroFrames).Append('/').Append(_windowFrames)
                .Append(" (").Append(allocatingPct.ToString("F0")).Append("%)  |  ")
                .Append("window total ").Append(_windowTotalBytes).Append(" B");

            Debug.Log(_log.ToString());
        }

        private void ResetWindow()
        {
            _windowStart = Time.unscaledTime;
            _windowFrames = 0;
            _windowTotalBytes = 0;
            _windowMaxBytes = 0;
            _windowNonZeroFrames = 0;
        }
    }
}
#endif
