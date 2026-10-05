using System.Collections;
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
        
        private void Awake() => Instance = this;
        
        public IEnumerator Play(VideoClip clip)
        {
            videoRoot.SetActive(true);
            videoPlayer.clip = clip;
            videoPlayer.isLooping = false;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            videoPlayer.Prepare();
            while (!videoPlayer.isPrepared) yield return null;

            bool finished = false;
            videoPlayer.loopPointReached += _ => finished = true;

            EventInstance audio = default;
            Coroutine fadeIn = null;
            if (!cutsceneAudio.IsNull)
            {
                audio = RuntimeManager.CreateInstance(cutsceneAudio);
                audio.setVolume(0f);
                audio.start();
                fadeIn = StartCoroutine(FadeIn(audio, audioFadeInDuration));
            }
            videoPlayer.Play();

            Coroutine endFade = null;
            while (!finished)
            {
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
    }
}