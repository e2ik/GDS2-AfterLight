using System.Text;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using UnityEngine.InputSystem;

public class FmodInstanceDebugger : MonoBehaviour
{
    [SerializeField] private Key logKey = Key.F9;
    [SerializeField] private bool showOverlay = true;
    [SerializeField] private string pathFilter = "";
    [SerializeField, Min(0.1f)] private float overlayRefresh = 0.5f;

    private string overlayText = "";
    private float nextRefresh;

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[logKey].wasPressedThisFrame)
            Debug.Log(BuildReport());

        if (showOverlay && Time.unscaledTime >= nextRefresh)
        {
            overlayText = BuildReport();
            nextRefresh = Time.unscaledTime + overlayRefresh;
        }
    }

    [ContextMenu("Log Live FMOD Instances")]
    public void LogNow()
    {
        Debug.Log(BuildReport());
    }

    private string BuildReport()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("[FMOD] Live instances:");
        int total = 0;

        if (RuntimeManager.StudioSystem.getParameterByName("Intensity", out float intensity) == FMOD.RESULT.OK)
            sb.AppendLine($"  global Intensity: {intensity:F2}");

        if (RuntimeManager.CoreSystem.getChannelsPlaying(out int channels, out int realChannels) == FMOD.RESULT.OK)
            sb.AppendLine($"  channels playing: {channels} (real {realChannels})");

        if (RuntimeManager.StudioSystem.getBankList(out Bank[] banks) != FMOD.RESULT.OK) return "[FMOD] Could not read banks";

        foreach (Bank bank in banks)
        {
            if (bank.getEventList(out EventDescription[] events) != FMOD.RESULT.OK) continue;

            foreach (EventDescription desc in events)
            {
                desc.getPath(out string path);
                if (string.IsNullOrEmpty(path)) path = desc.handle.ToString();
                if (!string.IsNullOrEmpty(pathFilter) && !path.Contains(pathFilter)) continue;
                if (desc.getInstanceList(out EventInstance[] instances) != FMOD.RESULT.OK || instances.Length == 0) continue;

                foreach (EventInstance inst in instances)
                {
                    inst.getPlaybackState(out PLAYBACK_STATE state);
                    if (state == PLAYBACK_STATE.STOPPED) continue;

                    inst.getVolume(out float volume);
                    inst.isVirtual(out bool isVirtual);
                    inst.getTimelinePosition(out int position);
                    inst.getPaused(out bool paused);
                    desc.isStream(out bool stream);
                    desc.getSampleLoadingState(out LOADING_STATE loading);
                    sb.AppendLine($"  {path}  state:{state}  vol:{volume:F2}  virtual:{isVirtual}  paused:{paused}  time:{position / 1000f:F1}s  stream:{stream}  samples:{loading}  count:{instances.Length}");
                    total++;
                }
            }
        }

        sb.AppendLine($"  total playing: {total}");
        return sb.ToString();
    }

    private void OnGUI()
    {
        if (!showOverlay) return;
        GUI.Label(new Rect(10, 10, 900, 600), overlayText);
    }
}
