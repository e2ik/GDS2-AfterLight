using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    public class AudioRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Slider slider;

        public TMP_Text Label => label;
        public Slider Slider => slider;
    }
}