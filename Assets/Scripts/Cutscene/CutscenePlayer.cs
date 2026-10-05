using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
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
        [SerializeField] private float audioFadeOutDuration = 0.7f;
        [SerializeField] private float audioFadeInDuration = 0.7f;
        [SerializeField] private float prepareTimeout = 10f;
        
        private void Awake() => Instance = this;
        
        public IEnumerator Play(string filename)
        {
            ClearTarget();

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
            videoPlayer.loopPointReached += _ => finished = true;

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
            while (!finished)
            {
                if (failed)
                    break;
                
                if (allowSkip && Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                    break;

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
            ClearTarget();
            videoPlayer.errorReceived -= onError;
            
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