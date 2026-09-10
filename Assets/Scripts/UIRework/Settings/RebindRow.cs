using UnityEngine;

namespace GameUI
{
    public class RebindRow : MonoBehaviour
    {
        [SerializeField] private RebindButton rebindButton;
        public RebindButton RebindButton => rebindButton;
    }
}