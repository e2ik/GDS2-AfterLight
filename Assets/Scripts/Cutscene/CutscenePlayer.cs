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
            if (!cutsceneAudio.IsNull)
            {
                audio = RuntimeManager.CreateInstance(cutsceneAudio);
                audio.start();
            }
            videoPlayer.Play();

            while (!finished)
            {
                if (allowSkip && Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                    break;
                yield return null;
            }

            videoPlayer.Stop();
            if (audio.isValid())
            {
                audio.stop(STOP_MODE.ALLOWFADEOUT);
                audio.release();
            }
            videoRoot.SetActive(false);
        }
    }
}