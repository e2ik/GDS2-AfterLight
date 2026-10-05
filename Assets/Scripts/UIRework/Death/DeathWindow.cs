namespace GameUI
{
    public class DeathWindow : UIWindow
    {
        protected override void OnWindowOpened()
        {
            UIManager.Instance.SuppressCancel = true;
            MusicManager.Instance?.SetPaused(true);
        }

        protected override void OnWindowClosed()
        {
            UIManager.Instance.SuppressCancel = false;
            MusicManager.Instance?.SetPaused(false);
        }
    }
}