using UnityEngine;
using UnityEngine.UI; // Required for UI elements like Text, Button
using HandballManager.Simulation; // Required for SimPlayer
using HandballManager.Simulation.Engines; // Required for MatchState
using System;
using HandballManager.Gameplay; // Added for Tactic
using TMPro; // If using TextMeshPro for dropdowns/text
using HandballManager.Core; // For GamePhase enum
// Assuming GameManager exists in a namespace like HandballManager.Management
// using HandballManager.Management;

namespace HandballManager.UI
{
    /// <summary>
    /// Manages the main game UI, including the match information panel, tactics panel, timeout, and substitution controls.
    /// </summary>
    public class UIManager : MonoBehaviour, HandballManager.UI.IUIManager
    {
        // --- Existing Fields ---
        [Header("Match Info Panel Elements")]
        public GameObject MatchInfoPanel;
        public Text TimeText;
        public Text HomeScoreText;
        public Text AwayScoreText;
        public Text HomeTeamNameText;
        public Text AwayTeamNameText;
        public Image PossessionIndicator;
        public Color HomePossessionColor = Color.blue;
        public Color AwayPossessionColor = Color.red;
        public Color NeutralPossessionColor = Color.grey;

        [Header("Tactics Panel")]
        public TacticsPanel TacticsAdjustmentPanel; // Assign the TacticsPanel script/GameObject

        // --- New Fields ---
        [Header("Pause/Timeout Controls")]
        public Button TimeoutButton; // Assign the timeout button
        public Text HomeTimeoutsText; // Optional: Display remaining home timeouts
        public Text AwayTimeoutsText; // Optional: Display remaining away timeouts

        [Header("Substitution Controls")]
        public Button OpenSubstitutionPanelButton; // Button to open the substitution interface
        public GameObject SubstitutionPanel; // The main panel for substitution UI (needs its own script eventually)

        [Header("Popup Message")]
        [Tooltip("Generic popup panel for messages.")]
        [SerializeField] private GameObject popupPanel;
        [Tooltip("Text element to display simple popup messages.")]
        [SerializeField] private TMP_Text popupText; 
        [Tooltip("Button to close the generic popup.")]
        [SerializeField] private Button popupOkButton;

        [Header("Game Management")]
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value null
        public /*GameManager*/ MonoBehaviour gameManager; // Assign the GameManager instance
#pragma warning restore CS0649
        private MatchState _currentMatchState;

        void Start()
        {
            // --- Existing Start Logic ---
            if (MatchInfoPanel == null || TimeText == null || HomeScoreText == null || AwayScoreText == null || HomeTeamNameText == null || AwayTeamNameText == null || PossessionIndicator == null)
            {
                Debug.LogError("UIManager is missing references to one or more Match Info UI elements.", this);
                if (MatchInfoPanel != null) MatchInfoPanel.SetActive(false);
            }
            else
            {
                TimeText.text = "00:00";
                HomeScoreText.text = "0";
                AwayScoreText.text = "0";
                HomeTeamNameText.text = "HOME";
                AwayTeamNameText.text = "AWAY";
                PossessionIndicator.color = NeutralPossessionColor;
            }

            if (TacticsAdjustmentPanel == null)
            {
                 Debug.LogWarning("UIManager is missing a reference to the TacticsAdjustmentPanel. Tactics adjustment will be unavailable.", this);
            }
            else
            {
                TacticsAdjustmentPanel.HidePanel();
                TacticsAdjustmentPanel.SetInteractable(false);
            }

            // --- GameManager Reference Check ---
             if (gameManager == null)
            {
                // Fallback commented out, rely on Inspector assignment
                // gameManager = FindObjectOfType</*GameManager*/>();
                if (gameManager == null)
                {
                    Debug.LogError("UIManager: GameManager is not assigned in the inspector. UI interaction will not work.", this);
                    if (TimeoutButton != null) TimeoutButton.interactable = false;
                    if (OpenSubstitutionPanelButton != null) OpenSubstitutionPanelButton.interactable = false;
                }
            }

            // --- Timeout Controls Setup ---
            if (TimeoutButton != null)
            {
                 TimeoutButton.onClick.AddListener(OnTimeoutButtonClicked); // Add listener
                 TimeoutButton.gameObject.SetActive(false);
            }
            else { Debug.LogWarning("UIManager: TimeoutButton is not assigned.", this); }

            if (HomeTimeoutsText != null) { HomeTimeoutsText.text = "-"; HomeTimeoutsText.gameObject.SetActive(false); }
            if (AwayTimeoutsText != null) { AwayTimeoutsText.text = "-"; AwayTimeoutsText.gameObject.SetActive(false); }

            // --- Substitution Controls Setup ---
            if (OpenSubstitutionPanelButton != null)
            {
                 OpenSubstitutionPanelButton.onClick.AddListener(OnOpenSubstitutionPanelClicked); // Add listener
                 OpenSubstitutionPanelButton.gameObject.SetActive(false);
            }
            else { Debug.LogWarning("UIManager: OpenSubstitutionPanelButton is not assigned.", this); }

            if (SubstitutionPanel != null)
            {
                 SubstitutionPanel.SetActive(false);
            }
             else { Debug.LogWarning("UIManager: SubstitutionPanel is not assigned.", this); }

            // --- Popup Setup ---
            if (popupPanel != null) popupPanel.SetActive(false); // Hide initially
            if (popupOkButton != null)
            {
                popupOkButton.onClick.AddListener(HidePopup);
            }
            else
            {
                Debug.LogWarning("UIManager: Popup OK Button is not assigned.", this);
            }
            if (popupText == null) Debug.LogWarning("UIManager: Popup Text is not assigned.", this);
        }

        /// <summary>
        /// Updates the UI elements with data from the provided MatchState.
        /// Also updates the visibility/interactability of pause-related controls.
        /// </summary>
        /// <param name="state">The current MatchState.</param>
        public void UpdateMatchInfo(MatchState state) // Replaces the previous UpdateMatchInfo
        {
            if (state == null)
            {
                 if (MatchInfoPanel != null && MatchInfoPanel.activeSelf) MatchInfoPanel.SetActive(false);
                 if (TimeoutButton != null) TimeoutButton.gameObject.SetActive(false);
                 if (HomeTimeoutsText != null) HomeTimeoutsText.gameObject.SetActive(false);
                 if (AwayTimeoutsText != null) AwayTimeoutsText.gameObject.SetActive(false);
                 if (OpenSubstitutionPanelButton != null) OpenSubstitutionPanelButton.gameObject.SetActive(false);
                 if (SubstitutionPanel != null) SubstitutionPanel.SetActive(false);
                return;
            }

            _currentMatchState = state;

            // --- Update Match Info Panel ---
            if (MatchInfoPanel != null && !MatchInfoPanel.activeSelf) MatchInfoPanel.SetActive(true);
             if (TimeText != null)
            {
                TimeSpan timeSpan = TimeSpan.FromSeconds(state.MatchTimeSeconds);
                string phaseText = "";
                if (state.CurrentPhase == GamePhase.HalfTime) phaseText = "HT ";
                else if (state.CurrentPhase == GamePhase.Timeout) phaseText = "TO ";
                // Note: GamePhase.Paused n'existe pas dans l'énumération, nous utilisons donc une logique alternative
                // Si vous avez besoin d'indiquer une pause, vous pouvez utiliser une autre approche, par exemple :
                // - Ajouter une propriété IsPaused à MatchState
                // - Utiliser une autre valeur de GamePhase comme indicateur
                // - Ajouter une logique spécifique dans le GameManager
                TimeText.text = phaseText + string.Format("{0:D2}:{1:D2}", (int)timeSpan.TotalMinutes, timeSpan.Seconds); // Use TotalMinutes
            }
             if (HomeScoreText != null) HomeScoreText.text = state.HomeScore.ToString();
            if (AwayScoreText != null) AwayScoreText.text = state.AwayScore.ToString();
            if (HomeTeamNameText != null) HomeTeamNameText.text = state.HomeTeamData?.Name ?? "HOME";
            if (AwayTeamNameText != null) AwayTeamNameText.text = state.AwayTeamData?.Name ?? "AWAY";
            if (PossessionIndicator != null)
            {
                 var possessingTeamId = state.PossessionTeamId;
                 if (possessingTeamId == 0) PossessionIndicator.color = HomePossessionColor;
                 else if (possessingTeamId == 1) PossessionIndicator.color = AwayPossessionColor;
                 else PossessionIndicator.color = NeutralPossessionColor;
            }

            // --- Update Pause Controls Visibility/Interactability ---
            // Note: GamePhase.Paused n'existe pas dans l'énumération, nous utilisons donc seulement les phases existantes
            bool isPausedForUserAction = state.CurrentPhase == GamePhase.HalfTime ||
                                         state.CurrentPhase == GamePhase.Timeout;
            // Si vous avez besoin de détecter d'autres états de pause, vous pouvez ajouter une logique spécifique ici
            bool canInteract = isPausedForUserAction && gameManager != null;

            // Timeout Button & Text
            if (TimeoutButton != null)
            {
                TimeoutButton.gameObject.SetActive(isPausedForUserAction);
                 // TODO: Determine player's team ID dynamically (e.g., from GameManager)
                 int playerTeamId = 0;
                 // Définir les paramètres requis pour CanTriggerTimeout
                 float matchTimeSeconds = state.MatchTimeSeconds;
                 float matchDurationSeconds = 3600f; // 60 minutes par défaut (à adapter selon les règles du jeu)
                 bool canCallTimeout = canInteract && state.CanTriggerTimeout(playerTeamId, matchTimeSeconds, matchDurationSeconds); // Check MatchState rules
                 TimeoutButton.interactable = canCallTimeout;
            }
             if (HomeTimeoutsText != null)
            {
                HomeTimeoutsText.text = $"Home TO: {state.HomeTimeoutsRemaining}";
                HomeTimeoutsText.gameObject.SetActive(isPausedForUserAction);
            }
             if (AwayTimeoutsText != null)
            {
                 AwayTimeoutsText.text = $"Away TO: {state.AwayTimeoutsRemaining}";
                 AwayTimeoutsText.gameObject.SetActive(isPausedForUserAction);
             }

            // Substitution Button
            if (OpenSubstitutionPanelButton != null)
            {
                OpenSubstitutionPanelButton.gameObject.SetActive(isPausedForUserAction);
                OpenSubstitutionPanelButton.interactable = canInteract;
            }

             // Hide Substitution Panel if game resumes *or* if game manager is lost
             if (SubstitutionPanel != null && SubstitutionPanel.activeSelf && !canInteract)
            {
                 SubstitutionPanel.SetActive(false);
            }
        }

        // --- Button Handlers ---

        public void OnTimeoutButtonClicked()
        {
            // TODO: Determine player's team ID dynamically
            int playerTeamId = 0;
            // Définir les paramètres requis pour CanTriggerTimeout
            float matchTimeSeconds = _currentMatchState?.MatchTimeSeconds ?? 0f;
            float matchDurationSeconds = 3600f; // 60 minutes par défaut (à adapter selon les règles du jeu)
            if (gameManager != null && _currentMatchState != null && _currentMatchState.CanTriggerTimeout(playerTeamId, matchTimeSeconds, matchDurationSeconds))
            {
                 // Call the GameManager to handle the request
                 // Example: gameManager.GetComponent<GameManager>().RequestPlayerTimeout(playerTeamId);
                 Debug.Log($"UI: Requesting timeout for team {playerTeamId} via GameManager (NEEDS IMPLEMENTATION)");

                 // --- TEMPORARY DIRECT CALL (Remove when GameManager handles it) ---
                 // Note: MatchSimulator is not a MonoBehaviour, so FindObjectOfType won't work
                 // We need to get a reference to MatchSimulator through GameManager or dependency injection
                 // For now, this code is commented out until we implement a proper solution
                 /*
                 var simulator = FindObjectOfType<MatchSimulator>();
                 if (simulator != null) {
                     bool success = simulator.TriggerTimeout(playerTeamId);
                     Debug.Log($"Direct Timeout Call Success: {success}");
                 }
                 */
                 // --- END TEMPORARY ---
            }
             else { Debug.LogWarning($"Cannot request timeout: GameManager missing, state invalid, or no timeouts left for team {playerTeamId}.", this); }
        }

        public void OnOpenSubstitutionPanelClicked()
        {
            if (SubstitutionPanel != null && gameManager != null && _currentMatchState != null)
            {
                 bool isActive = SubstitutionPanel.activeSelf;
                 SubstitutionPanel.SetActive(!isActive); // Toggle panel
                 if (!isActive)
                 {
                     // TODO: Call a method on SubstitutionPanel's script to populate player lists
                     // Example: SubstitutionPanel.GetComponent<SubstitutionPanelUI>().PopulateLists(_currentMatchState, 0); // Assuming player team 0
                     Debug.Log("UI: Opened Substitution Panel (Needs population logic)");
                 } else {
                     Debug.Log("UI: Closed Substitution Panel");
                 }
            }
             else { Debug.LogError("Cannot open substitution panel: Panel, GameManager, or MatchState missing.", this); }
        }

         // --- Placeholder for Substitution Confirmation (Called from Substitution Panel UI) ---
        public void ConfirmSubstitution(SimPlayer playerOut, SimPlayer playerIn)
        {
             // TODO: Get player's team ID
             int playerTeamId = 0;
            if (gameManager != null && playerOut != null && playerIn != null)
            {
                 // Call the GameManager to handle the request
                 // Example: gameManager.GetComponent<GameManager>().RequestPlayerSubstitution(playerTeamId, playerOut, playerIn);
                 Debug.Log($"UI: Requesting substitution {playerOut.BaseData.FullName} -> {playerIn.BaseData.FullName} via GameManager (NEEDS IMPLEMENTATION)");

                  // --- TEMPORARY DIRECT CALL (Remove when GameManager handles it) ---
                 // Note: MatchSimulator is not a MonoBehaviour, so FindObjectOfType won't work
                 // We need to get a reference to MatchSimulator through GameManager or dependency injection
                 // For now, this code is commented out until we implement a proper solution
                 /*
                 var simulator = FindObjectOfType<MatchSimulator>();
                 if (simulator != null) {
                     bool success = simulator.TrySubstitute(playerOut, playerIn);
                     Debug.Log($"Direct Substitution Call Success: {success}");
                     if (success && SubstitutionPanel != null) SubstitutionPanel.SetActive(false); // Close panel on success
                 }
                 */
                  // --- END TEMPORARY ---
            }
            else { Debug.LogError("Cannot confirm substitution: GameManager missing or invalid players.", this); }
        }


        // --- Existing Tactics Panel Methods ---
        public void ShowTacticsPanel(Tactic tacticToEdit, bool allowInteraction)
        {
             Debug.Log($"[UIManager] Showing tactics panel for: {tacticToEdit}, interactive: {allowInteraction}");
             if (TacticsAdjustmentPanel != null)
            {
                TacticsAdjustmentPanel.ShowPanel(tacticToEdit);
                TacticsAdjustmentPanel.SetInteractable(allowInteraction);
            } else { Debug.LogWarning("Tried to show Tactics Panel, but it's not assigned."); }
        }
        public void HideTacticsPanel()
        {
             Debug.Log("[UIManager] Hiding tactics panel");
             if (TacticsAdjustmentPanel != null) TacticsAdjustmentPanel.HidePanel();
        }
        public void SetTacticsPanelInteractable(bool isInteractable)
        {
             if (TacticsAdjustmentPanel != null) TacticsAdjustmentPanel.SetInteractable(isInteractable);
        }

        // --- Popup Management (from Core/GameManager/UiManager.cs) ---
        /// <summary>
        /// Displays a simple popup message to the player.
        /// </summary>
        /// <param name="message">The message to display.</param>
        public void DisplayPopup(string message)
        {
            if (popupPanel == null || popupText == null)
            {
                 Debug.LogWarning("Popup Panel or Text not assigned. Cannot display popup.");
                 // Fallback to console log
                 Debug.Log($"[UI Popup Fallback]: {message}");
                 return;
            }
             Debug.Log($"[UIManager] Displaying Popup: '{message}'");
             popupText.text = message;
             popupPanel.SetActive(true);
             // Optional: Consider if pausing simulation is needed here based on context
             // if (_currentMatchState != null) _currentMatchState.PauseSimulation(); // Requires PauseSimulation method
        }

        /// <summary>
        /// Hides the generic popup panel. Called by the popup's OK button via listener.
        /// </summary>
        public void HidePopup()
        {
             if (popupPanel != null)
             {
                 popupPanel.SetActive(false);
                  Debug.Log("[UIManager] Hiding Popup.");
                 // Optional: Resume simulation if it was paused
                 // if (_currentMatchState != null) _currentMatchState.ResumeSimulation(); // Requires ResumeSimulation method
             }
        }
        
        // --- Methods needed by GameManager ---
        
        /// <summary>
        /// Updates the UI to match the current game state.
        /// </summary>
        public void UpdateUIForGameState(GameState state)
        {
            Debug.Log($"[UIManager] Updating UI for game state: {state}");
            // Implement UI updates based on game state
            // This is a placeholder implementation
        }
        
        /// <summary>
        /// Updates the UI when the game state changes.
        /// </summary>
        public void UpdateGameStateUI(GameState state)
        {
            Debug.Log($"[UIManager] Game state changed to: {state}");
            // Implement UI updates for state change
            // This is a placeholder implementation
        }
        
        /// <summary>
        /// Shows the team management screen for the specified team.
        /// </summary>
        public void ShowTeamScreen(object team)
        {
            Debug.Log("[UIManager] Showing team screen");
            // Implement team screen display
            // This is a placeholder implementation
        }
        
        /// <summary>
        /// Shows the match preview screen with the given match result
        /// </summary>
        /// <param name="matchResult">The match result to display</param>
        public void ShowMatchPreview(object matchResult)
        {
            Debug.Log($"[UIManager] Showing match preview for: {matchResult}");
            // Implement match preview display logic
        }

        /// <summary>
        /// Interface implementation that shows the tactics panel with the given tactic
        /// </summary>
        /// <param name="tactic">The tactic to display</param>
        /// <param name="interactive">Whether the panel should be interactive</param>
        public void ShowTacticsPanel(object tactic, bool interactive)
        {
            // Cast the object to Tactic if possible
            if (tactic is Tactic tacticObj)
            {
                ShowTacticsPanel(tacticObj, interactive);
            }
            else
            {
                Debug.LogWarning($"[UIManager] Cannot show tactics panel: expected Tactic but got {tactic?.GetType().Name ?? "null"}");
            }
        }

        // Méthode HideTacticsPanel déjà implémentée plus haut
    }
}