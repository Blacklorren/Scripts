using UnityEngine;
using UnityEngine.UI;
using System; // Pour le type Enum
using HandballManager.Gameplay; // For Tactic and enums
using HandballManager.Core; // For Enums
using HandballManager.Data; // Pour FormationData
using TMPro; // If using TextMeshPro for dropdowns/text

namespace HandballManager.UI
{
    /// <summary>
    /// Manages the UI panel for adjusting team tactics during pauses.
    /// </summary>
    public class TacticsPanel : MonoBehaviour
    {
        [Header("Panel Root")]
        public GameObject PanelRoot; // The parent GameObject of the tactics UI

        [Header("Tactic Controls")]
        public TMP_Dropdown DefensiveSystemDropdown; // Use TMP_Dropdown or Dropdown
        public TMP_Dropdown PaceDropdown;
        public TMP_Dropdown FocusPlayDropdown;
        public Slider TeamAggressionSlider;
        public TextMeshProUGUI TeamAggressionValueText; // Optional: Show numeric value
        public Slider DefensiveLineSlider;
        public TextMeshProUGUI DefensiveLineValueText; // Optional: Show numeric value

        // Add buttons if needed (e.g., Apply, Reset, Close)
        // public Button CloseButton;

        private Tactic _currentTargetTactic; // The tactic object being modified

        void Awake()
        {
            // Basic validation
            if (PanelRoot == null || DefensiveSystemDropdown == null || PaceDropdown == null || FocusPlayDropdown == null ||
                TeamAggressionSlider == null || DefensiveLineSlider == null)
            {
                Debug.LogError("TacticsPanel is missing references to one or more UI elements. Please assign them in the Inspector.", this);
                enabled = false;
                return;
            }

            // Ensure panel is hidden initially
            if (PanelRoot != null) PanelRoot.SetActive(false);

            // Populate Dropdowns (This assumes enums are defined in Core or Gameplay)
            PopulateDropdown<DefensiveSystem>(DefensiveSystemDropdown);
            PopulateDropdown<TacticPace>(PaceDropdown);
            PopulateDropdown<OffensiveFocusPlay>(FocusPlayDropdown);

            // Add listeners for UI changes
            DefensiveSystemDropdown.onValueChanged.AddListener(OnDefensiveSystemChanged);
            PaceDropdown.onValueChanged.AddListener(OnPaceChanged);
            FocusPlayDropdown.onValueChanged.AddListener(OnFocusPlayChanged);
            TeamAggressionSlider.onValueChanged.AddListener(OnAggressionChanged);
            DefensiveLineSlider.onValueChanged.AddListener(OnDefensiveLineChanged);

            // Add listener for close button if you have one
            // CloseButton.onClick.AddListener(HidePanel);
        }

        /// <summary>
        /// Shows the tactics panel and populates it with the values from the given tactic.
        /// </summary>
        /// <param name="tacticToEdit">The Tactic object to display and modify.</param>
        public void ShowPanel(Tactic tacticToEdit)
        {
            if (!enabled || tacticToEdit == null) return;

            _currentTargetTactic = tacticToEdit;

            // Set UI elements to match the current tactic
            // Nous devons convertir DefensiveFormationData en index pour le dropdown
            int defensiveFormationIndex = GetFormationIndex(_currentTargetTactic.DefensiveFormationData?.name);
            SetDropdownValue(DefensiveSystemDropdown, defensiveFormationIndex);
            SetDropdownValue(PaceDropdown, (int)_currentTargetTactic.Pace);
            SetDropdownValue(FocusPlayDropdown, (int)_currentTargetTactic.FocusPlay);

            TeamAggressionSlider.value = _currentTargetTactic.TeamAggressionLevel;
            if (TeamAggressionValueText != null) TeamAggressionValueText.text = _currentTargetTactic.TeamAggressionLevel.ToString("F1");

            DefensiveLineSlider.value = _currentTargetTactic.DefensiveLineHeight;
            if (DefensiveLineValueText != null) DefensiveLineValueText.text = _currentTargetTactic.DefensiveLineHeight.ToString("F1");

            // Activate the panel
            if (PanelRoot != null) PanelRoot.SetActive(true);
            SetInteractable(true); // Make sure controls are usable
        }

        /// <summary>
        /// Hides the tactics panel.
        /// </summary>
        public void HidePanel()
        {
            if (PanelRoot != null) PanelRoot.SetActive(false);
            _currentTargetTactic = null; // Clear reference
        }

        /// <summary>
        /// Enables or disables interaction with the tactic controls.
        /// </summary>
        /// <param name="isInteractable">True to enable, false to disable.</param>
        public void SetInteractable(bool isInteractable)
        {
             if (!enabled) return;
             DefensiveSystemDropdown.interactable = isInteractable;
             PaceDropdown.interactable = isInteractable;
             FocusPlayDropdown.interactable = isInteractable;
             TeamAggressionSlider.interactable = isInteractable;
             DefensiveLineSlider.interactable = isInteractable;
             // If you have Apply/Close buttons, set their interactable state too
             // CloseButton.interactable = true; // Close should maybe always be interactable?
        }


        // --- UI Change Handlers ---

        private void OnDefensiveSystemChanged(int index)
        {
            if (_currentTargetTactic != null)
            {
                // Convertir l'index en nom de formation
                string formationName = GetFormationNameFromIndex(index);
                
                // Charger la formation à partir des ressources
                _currentTargetTactic.DefensiveFormationData = LoadFormationData(formationName);
                
                Debug.Log($"Tactic Updated: DefensiveFormationData = {_currentTargetTactic.DefensiveFormationData?.FormationName ?? "None"}"); // For debugging
            }
        }
        
        private string GetFormationNameFromIndex(int index)
        {
            // Convertir l'index en nom de formation
            // Ceci doit correspondre à l'ordre des options dans le dropdown
            switch (index)
            {
                case 0: return "6-0";
                case 1: return "5-1";
                case 2: return "4-2";
                case 3: return "3-3";
                case 4: return "3-2-1";
                default: return "6-0"; // Formation par défaut
            }
        }
        
        private int GetFormationIndex(string formationName)
        {
            // Convertir le nom de formation en index
            // Ceci doit correspondre à l'ordre des options dans le dropdown
            if (string.IsNullOrEmpty(formationName)) return 0; // Default to first option
            
            switch (formationName)
            {
                case "6-0": return 0;
                case "5-1": return 1;
                case "4-2": return 2;
                case "3-3": return 3;
                case "3-2-1": return 4;
                default: return 0; // Default to first option if not found
            }
        }
        
        private FormationData LoadFormationData(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning("Formation name is null or empty");
                return null;
            }

            string resourcePath = "Formations/" + name;
            FormationData loadedData = Resources.Load<FormationData>(resourcePath);

            if (loadedData == null)
            {
                Debug.LogWarning($"Failed to load FormationData from Resources: {resourcePath}. Make sure the asset exists and the path is correct.");
            }

            return loadedData;
        }

        private void OnPaceChanged(int index)
        {
            if (_currentTargetTactic != null)
            {
                _currentTargetTactic.Pace = (TacticPace)index;
                 Debug.Log($"Tactic Updated: Pace = {_currentTargetTactic.Pace}");
            }
        }

        private void OnFocusPlayChanged(int index)
        {
             if (_currentTargetTactic != null)
            {
                _currentTargetTactic.FocusPlay = (OffensiveFocusPlay)index;
                 Debug.Log($"Tactic Updated: FocusPlay = {_currentTargetTactic.FocusPlay}");
            }
        }

        private void OnAggressionChanged(float value)
        {
            if (_currentTargetTactic != null)
            {
                _currentTargetTactic.TeamAggressionLevel = value;
                if (TeamAggressionValueText != null) TeamAggressionValueText.text = value.ToString("F1");
                 Debug.Log($"Tactic Updated: TeamAggressionLevel = {_currentTargetTactic.TeamAggressionLevel:F1}");
            }
        }

        private void OnDefensiveLineChanged(float value)
        {
             if (_currentTargetTactic != null)
            {
                _currentTargetTactic.DefensiveLineHeight = value;
                 if (DefensiveLineValueText != null) DefensiveLineValueText.text = value.ToString("F1");
                 Debug.Log($"Tactic Updated: DefensiveLineHeight = {_currentTargetTactic.DefensiveLineHeight:F1}");
            }
        }

        // --- Helper Methods ---

        private void PopulateDropdown<TEnum>(TMP_Dropdown dropdown) where TEnum : Enum
        {
            if (dropdown == null) return;
            dropdown.ClearOptions();
            var names = System.Enum.GetNames(typeof(TEnum));
            var options = new System.Collections.Generic.List<TMP_Dropdown.OptionData>();
            foreach (var name in names)
            {
                options.Add(new TMP_Dropdown.OptionData(FormatEnumName(name))); // Format name if needed
            }
            dropdown.AddOptions(options);
        }

        // Safely sets dropdown value, preventing errors if index is out of range
        private void SetDropdownValue(TMP_Dropdown dropdown, int value)
        {
            if (dropdown != null)
            {
                dropdown.value = Mathf.Clamp(value, 0, dropdown.options.Count - 1);
            }
        }

        // Optional: Improve enum name display (e.g., "SixZero" -> "6-0")
        private string FormatEnumName(string name)
        {
            // Example formatting for DefensiveSystem
            if (name == "SixZero") return "6-0";
            if (name == "FiveOne") return "5-1";
            if (name == "ThreeTwoOne") return "3-2-1";
             // Add more specific formatting if needed
            // Simple space insertion for camel case:
            // return System.Text.RegularExpressions.Regex.Replace(name, "(\\B[A-Z])", " $1");
            return name; // Default
        }

        void OnDestroy()
        {
            // Remove listeners to prevent errors if the object is destroyed
            if (DefensiveSystemDropdown != null) DefensiveSystemDropdown.onValueChanged.RemoveAllListeners();
            if (PaceDropdown != null) PaceDropdown.onValueChanged.RemoveAllListeners();
            if (FocusPlayDropdown != null) FocusPlayDropdown.onValueChanged.RemoveAllListeners();
            if (TeamAggressionSlider != null) TeamAggressionSlider.onValueChanged.RemoveAllListeners();
            if (DefensiveLineSlider != null) DefensiveLineSlider.onValueChanged.RemoveAllListeners();
            // Remove button listeners too
        }
    }
}
