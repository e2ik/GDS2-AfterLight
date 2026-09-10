using UnityEngine;
using UnityEngine.InputSystem;

namespace GameUI
{
    public static class InputRebindSaver
    {
        private const string PrefKey = "InputBindingOverrides";

        public static void Save(InputActionAsset asset)
        {
            string json = asset.SaveBindingOverridesAsJson();
            PlayerPrefs.SetString(PrefKey, json);
            PlayerPrefs.Save();
        }

        public static void Load(InputActionAsset asset)
        {
            if (!PlayerPrefs.HasKey(PrefKey)) { return; }

            string json = PlayerPrefs.GetString(PrefKey);
            asset.LoadBindingOverridesFromJson(json);
        }
    }
}