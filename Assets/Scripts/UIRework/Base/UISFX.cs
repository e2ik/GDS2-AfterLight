namespace GameUI
{
    public static class UISFX
    {
        // Window transitions
        public static void PlayWindowOpen() { }
        public static void PlayWindowClose() { }

        // Generic interactions
        public static void PlayButtonClick() { }
        public static void PlayTabSwitch() { }
        public static void PlaySliderChanged() { }

        // Rebinding flow
        public static void PlayRebindStart() { }
        public static void PlayRebindComplete() { }
        public static void PlayRebindCancelled() { }
        public static void PlayRebindError() { }
    }
}