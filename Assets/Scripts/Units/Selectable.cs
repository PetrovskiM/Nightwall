using UnityEngine;

namespace Nightwall
{
    /// <summary>Marks an object the player can select; toggles an optional highlight.</summary>
    public class Selectable : MonoBehaviour
    {
        [SerializeField] GameObject selectionIndicator;

        public bool IsSelected { get; private set; }

        void Awake()
        {
            if (selectionIndicator != null) selectionIndicator.SetActive(false);
        }

        public void SetSelected(bool value)
        {
            IsSelected = value;
            if (selectionIndicator != null) selectionIndicator.SetActive(value);
        }
    }
}
