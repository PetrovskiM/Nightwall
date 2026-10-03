using UnityEngine;

namespace Nightwall
{
    /// <summary>
    /// Minimal on-screen build toolbar: one button per buildable structure, showing its name and
    /// construction cost, that selects it for placement. This is an input surface (Input →
    /// gameplay), so it may drive <see cref="BuildingPlacer.ActivateForIndex"/>; it reads structure
    /// metadata off the prefabs and never mutates gameplay state directly. IMGUI for the prototype so
    /// it needs no canvas/prefab wiring — to be replaced by real mobile UI later.
    /// </summary>
    public class BuildBar : MonoBehaviour
    {
        [SerializeField] BuildingPlacer placer;
        [SerializeField] int buttonWidth = 150;
        [SerializeField] int buttonHeight = 54;
        [SerializeField] Vector2 margin = new Vector2(24f, 24f);

        GUIStyle _style;

        void OnGUI()
        {
            if (placer == null) return;
            // Build is a daytime activity; hide the bar when the player can't build.
            if (GameManager.Instance != null && !GameManager.Instance.CanBuild) return;

            var list = placer.Buildables;
            if (list == null || list.Count == 0) return;

            if (_style == null)
                _style = new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold };

            // Anchor along the bottom of the device safe area.
            Rect safe = Screen.safeArea;
            float x = safe.x + margin.x;
            float y = safe.yMax - margin.y - buttonHeight;

            for (int i = 0; i < list.Count; i++)
            {
                var prefab = list[i];
                if (prefab == null) continue;

                var structure = prefab.GetComponent<DefensiveStructure>();
                string label = structure != null
                    ? $"{i + 1}. {structure.DisplayName}\n{structure.Cost}"
                    : $"{i + 1}. {prefab.name}";

                bool selected = placer.IsActive && placer.SelectedIndex == i;
                var prev = GUI.backgroundColor;
                if (selected) GUI.backgroundColor = new Color(0.4f, 0.9f, 0.5f);

                if (GUI.Button(new Rect(x, y, buttonWidth, buttonHeight), label, _style))
                    placer.ActivateForIndex(i);

                GUI.backgroundColor = prev;
                x += buttonWidth + 8f;
            }
        }
    }
}
