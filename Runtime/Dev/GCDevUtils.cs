using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System;
using DSB.GC;
using DSB.GC.Log;

namespace DSB.GC.Dev
{
    public enum FluctuateFpsMode
    {
        Disabled,
        EditorOnly,
        EditorAndBuild
    }

    public class GCDevUtils : MonoBehaviour
    {
        [Header("Time Scale Shortcuts")]
        [SerializeField]
        private bool enableTimeScaleShortcuts = true;

        [SerializeField, Tooltip("Decreases time scale by 0.1")]
        private KeyCode decreaseTimeScalePrimary = KeyCode.KeypadMinus;

        [SerializeField, Tooltip("Alternative key to decrease time scale by 0.1")]
        private KeyCode decreaseTimeScaleAlternative = KeyCode.Comma;

        [SerializeField, Tooltip("Increases time scale by 0.1")]
        private KeyCode increaseTimeScalePrimary = KeyCode.KeypadPlus;

        [SerializeField, Tooltip("Alternative key to increase time scale by 0.1")]
        private KeyCode increaseTimeScaleAlternative = KeyCode.Period;

        [SerializeField, Tooltip("Toggles pause. Hold Shift to restore previous time scale instead of 1.0")]
        private KeyCode togglePausePrimary = KeyCode.KeypadEnter;

        [SerializeField, Tooltip("Alternative key to toggle pause. Hold Shift to restore previous time scale instead of 1.0")]
        private KeyCode togglePauseAlternative = KeyCode.Minus;

        [Header("Fluctuate FPS")]
        [SerializeField]
        private FluctuateFpsMode enableFluctuateFps = FluctuateFpsMode.Disabled;

        [SerializeField]
        private int fluctuateMaxMsPerFrame = 16;

        [Header("Play Mode Restart")]
        [SerializeField]
        private bool enablePlayModeRestart = true;

        [SerializeField]
        private KeyCode playModeRestartKey = KeyCode.Tab;

        private bool isRestarting = false;
        public bool IsRestarting => isRestarting;

        private void OnEnable()
        {
            if (ShouldFluctuateFps())
            {
                Debug.LogWarning("[GCDevUtils] Fluctuate FPS is enabled and set to '" + enableFluctuateFps + "' with max ms per frame of '" + fluctuateMaxMsPerFrame + "'");
            }

            previouslySetTimescale = Time.timeScale;
        }

        private void Update()
        {
            HandleFluctuateFps();
            HandlePlayModeRestart();
        }

        void OnGUI()
        {
#if UNITY_EDITOR
            Event e = Event.current;
            if (e.isKey && e.type == EventType.KeyDown)
            {
                HandleTimeScale(e.keyCode);
            }
#endif
        }

        private bool ShouldFluctuateFps()
        {
            if (enableFluctuateFps == FluctuateFpsMode.EditorAndBuild)
            {
                return true;
            }
#if UNITY_EDITOR
            else if (enableFluctuateFps == FluctuateFpsMode.EditorOnly)
            {
                return true;
            }
#endif
            return false;
        }

        private void HandleFluctuateFps()
        {
#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.F7))
            {
                enableFluctuateFps = (FluctuateFpsMode)(((int)enableFluctuateFps + 1) % 3);
            }
#endif

            bool shouldFluctuate = ShouldFluctuateFps();

            if (shouldFluctuate && fluctuateMaxMsPerFrame > 0)
            {
                var t = (Mathf.Sin(Time.time * 7.0f) + 1.0f) * 0.5f;
                var jitter = UnityEngine.Random.Range(0, 4);
                var targetMs = Mathf.Clamp(Mathf.RoundToInt(t * fluctuateMaxMsPerFrame) + jitter, 0, fluctuateMaxMsPerFrame + 3);

                var start = System.Diagnostics.Stopwatch.StartNew();
                while (start.ElapsedMilliseconds < targetMs)
                {
                }
                start.Stop();
            }
        }

        private float previouslySetTimescale = 1.0f;

        private void HandleTimeScale(KeyCode keyCode)
        {
#if UNITY_EDITOR
            if (!enableTimeScaleShortcuts)
            {
                return;
            }

            if (keyCode == decreaseTimeScalePrimary || keyCode == decreaseTimeScaleAlternative)
            {
                previouslySetTimescale = Time.timeScale;
                Time.timeScale = Mathf.Max(Time.timeScale - 0.1f, 0.0f);
            }
            if (keyCode == increaseTimeScalePrimary || keyCode == increaseTimeScaleAlternative)
            {
                previouslySetTimescale = Time.timeScale;
                Time.timeScale = Mathf.Min(Time.timeScale + 0.1f, 10.0f);
            }
            if (keyCode == togglePausePrimary || keyCode == togglePauseAlternative)
            {
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    Time.timeScale = Mathf.Approximately(Time.timeScale, 0.0f) ? previouslySetTimescale : 0.0f;
                }
                else
                {
                    Time.timeScale = Mathf.Approximately(Time.timeScale, 0.0f) ? 1.0f : 0.0f;
                }
            }
#endif
        }

        private void HandlePlayModeRestart()
        {
            if (Input.GetKeyDown(playModeRestartKey) && !isRestarting)
            {
                StartCoroutine(_HandleGamePlayModeRestart());
            }
        }

        private IEnumerator _HandleGamePlayModeRestart()
        {
            if (isRestarting || !Application.isEditor || !enablePlayModeRestart || !Application.isPlaying)
            {
                yield break;
            }

            isRestarting = true;

            try
            {
                GCLog.LogDebug("Restarting game in play mode -----------");

                GameObject temp = new GameObject("SceneProbe");
                DontDestroyOnLoad(temp);
                Scene dontDestroyScene = temp.scene;
                DestroyImmediate(temp);

                GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                List<GameObject> donDestroyOnLoadObjects = new List<GameObject>();

                foreach (var obj in allObjects)
                {
                    if (obj.scene == dontDestroyScene && obj.transform.parent == null)
                    {
                        donDestroyOnLoadObjects.Add(obj);
                    }
                }

                foreach (var obj in donDestroyOnLoadObjects)
                {
                    if (obj != null)
                    {
                        DestroyImmediate(obj);
                    }
                }

                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
            catch (Exception e)
            {
                GCLog.LogWarning("Error restarting game in play mode: " + e.Message);
            }

            yield return null;
            isRestarting = false;
        }
    }
}
