using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Video;
using FMODUnity;
using FMOD.Studio;
using STOP_MODE = FMOD.Studio.STOP_MODE;

namespace Enemies.Cutscene
{
    public class CutscenePlayer : MonoBehaviour
    {
        public static CutscenePlayer Instance { get; private set; }
        
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private GameObject videoRoot;
        [SerializeField] private EventReference cutsceneAudio;
        [SerializeField] private bool allowSkip = true;
        [SerializeField] private GameObject skipPrompt;
        [SerializeField, Min(0f)] private float skipPromptDuration = 3f;
        [SerializeField, Min(0f)] private float skipPromptFadeDuration = 0.2f;

        private CanvasGroup skipPromptGroup;
        private bool skipPromptTarget;
        [SerializeField] private float audioFadeOutDuration = 0.7f;
        [SerializeField] private float audioFadeInDuration = 0.7f;
        [SerializeField] private float prepareTimeout = 10f;
        [SerializeField] private GameObject[] hideDuringCutscene;

        private bool[] hiddenStates;
        private bool uiHidden;

        private void Awake() => Instance = this;

        public void SetCutsceneUIHidden(bool hidden)
        {
            if (hideDuringCutscene == null || uiHidden == hidden) return;

            if (hidden)
            {
                hiddenStates = new bool[hideDuringCutscene.Length];
                for (int i = 0; i < hideDuringCutscene.Length; i++)
                {
                    GameObject target = hideDuringCutscene[i];
                    if (target == null) continue;

                    hiddenStates[i] = target.activeSelf;
                    target.SetActive(false);
                }
            }
            else if (hiddenStates != null)
            {
                for (int i = 0; i < hideDuringCutscene.Length && i < hiddenStates.Length; i++)
                {
                    GameObject target = hideDuringCutscene[i];
                    if (target != null && hiddenStates[i]) target.SetActive(true);
                }
            }

            uiHidden = hidden;
        }
        
        public IEnumerator Play(string filename)
        {
            ClearTarget();
            SetSkipPromptVisible(false, true);

            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = Application.streamingAssetsPath + "/" + ResolveFileName(filename);
            
            videoPlayer.isLooping = false;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;

            bool failed = false;
            VideoPlayer.ErrorEventHandler onError = (vp, msg) =>
            {
                Debug.LogError($"[CutscenePlayer] Video error: {msg}");
                failed = true;
            };

            videoPlayer.errorReceived += onError;
            
            videoPlayer.Prepare();
            float waited = 0f;
            while (!videoPlayer.isPrepared && !failed && waited < prepareTimeout)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!videoPlayer.isPrepared)
            {
                Debug.LogWarning("[CutscenePlayer] Video failed to prepare, skipping cutscene");
                videoPlayer.errorReceived -= onError;
                videoPlayer.Stop();
                videoRoot.SetActive(false);
                yield break;
            }

            bool finished = false;
            VideoPlayer.EventHandler onFinished = _ => finished = true;
            videoPlayer.loopPointReached += onFinished;

            videoPlayer.Play();

            float frameWait = 0f;
            while ((!videoPlayer.isPlaying || videoPlayer.frame < 1) && !failed && frameWait < 1f)
            {
                frameWait += Time.unscaledDeltaTime;
                yield return null;
            }
            
            videoRoot.SetActive(true);
            
            EventInstance audio = default;
            Coroutine fadeIn = null;
            if (!cutsceneAudio.IsNull)
            {
                audio = RuntimeManager.CreateInstance(cutsceneAudio);
                audio.setVolume(0f);
                audio.start();
                fadeIn = StartCoroutine(FadeIn(audio, audioFadeInDuration));
            }

            Coroutine endFade = null;
            float skipPromptTimer = 0f;
            while (!finished)
            {
                if (failed)
                    break;

                UpdateSkipPromptFade();

                if (skipPromptTimer > 0f)
                {
                    skipPromptTimer -= Time.unscaledDeltaTime;
                    if (skipPromptTimer <= 0f) SetSkipPromptVisible(false);
                }

                if (allowSkip && AnySkipInputPressed())
                {
                    if (skipPrompt == null || skipPromptTimer > 0f)
                        break;

                    SetSkipPromptVisible(true);
                    skipPromptTimer = skipPromptDuration > 0f ? skipPromptDuration : float.MaxValue;
                }

                if (endFade == null && audio.isValid() && MusicManager.Instance != null &&
                    videoPlayer.length > audioFadeOutDuration &&
                    videoPlayer.length - videoPlayer.time <= audioFadeOutDuration)
                {
                    if(fadeIn != null)
                        StopCoroutine(fadeIn);

                    endFade = StartCoroutine(MusicManager.Instance.FadeOutAndRelease(audio, audioFadeOutDuration));
                } 
                
                yield return null;
            }

            videoPlayer.Stop();
            videoRoot.SetActive(false);
            SetSkipPromptVisible(false, true);
            ClearTarget();
            videoPlayer.errorReceived -= onError;
            videoPlayer.loopPointReached -= onFinished;
            
            if(fadeIn != null)
                StopCoroutine(fadeIn);

            if (endFade != null)
            {
                yield return endFade;
            }
            else if (MusicManager.Instance != null && audio.isValid())
            {
                yield return MusicManager.Instance.FadeOutAndRelease(audio, audioFadeOutDuration);
            }
            else if (audio.isValid())
            {
                audio.stop(STOP_MODE.IMMEDIATE);
                audio.release();
            }
        }

        private IEnumerator FadeIn(EventInstance inst, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                if (!inst.isValid())
                    yield break;

                t += Time.unscaledDeltaTime;
                inst.setVolume(Mathf.Clamp01(t / duration));
                yield return null;
            }

            if (inst.isValid())
                inst.setVolume(1f);
        }
        
        private void SetSkipPromptVisible(bool visible, bool instant = false)
        {
            if (skipPrompt == null) return;

            if (skipPromptGroup == null)
            {
                skipPromptGroup = skipPrompt.GetComponent<CanvasGroup>();
                if (skipPromptGroup == null) skipPromptGroup = skipPrompt.AddComponent<CanvasGroup>();
                skipPromptGroup.interactable = false;
                skipPromptGroup.blocksRaycasts = false;
            }

            skipPromptTarget = visible;

            if (instant || skipPromptFadeDuration <= 0f)
            {
                skipPromptGroup.alpha = visible ? 1f : 0f;
                skipPrompt.SetActive(visible);
                return;
            }

            if (visible) skipPrompt.SetActive(true);
        }

        private void UpdateSkipPromptFade()
        {
            if (skipPrompt == null || skipPromptGroup == null || !skipPrompt.activeSelf) return;

            float target = skipPromptTarget ? 1f : 0f;
            skipPromptGroup.alpha = Mathf.MoveTowards(skipPromptGroup.alpha, target, Time.unscaledDeltaTime / Mathf.Max(0.0001f, skipPromptFadeDuration));

            if (!skipPromptTarget && skipPromptGroup.alpha <= 0f) skipPrompt.SetActive(false);
        }

        private static bool AnySkipInputPressed()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                return true;

            if (Mouse.current != null &&
                (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame))
                return true;

            foreach (Gamepad pad in Gamepad.all)
            {
                foreach (InputControl control in pad.allControls)
                {
                    if (control is not ButtonControl button || button.synthetic) continue;
                    if (button.parent is StickControl) continue;
                    if (button.wasPressedThisFrame) return true;
                }
            }

            return false;
        }

        private void ClearTarget()
        {
            RenderTexture rt = videoPlayer.targetTexture;
            
            if (rt == null) 
                return;
            
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = prev;
        }

        private static string ResolveFileName(string filename)
        {
            string baseName = Path.GetFileNameWithoutExtension(filename);
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
            return baseName + ".webm";
#else
            return baseName + ".mp4";
#endif
        }
    }
}