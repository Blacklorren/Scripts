using UnityEngine;
using UnityEngine.UI; // Required for UI elements like Text, Button
using HandballManager.Simulation; // Required for MatchState, SimPlayer
using System;
using HandballManager.Gameplay; // Added for Tactic
using TMPro; // If using TextMeshPro for dropdowns/text
using HandballManager.Core; // For GamePhase enum

namespace HandballManager.UI
{
    /// <summary>
    /// Interface defining the core UI management functionality needed by GameManager
    /// </summary>
    public interface IUIManager
    {
        void UpdateUIForGameState(GameState state);
        void UpdateGameStateUI(GameState state);
        void ShowTeamScreen(object team);
        void ShowMatchPreview(object matchResult);
        void DisplayPopup(string message);
        void ShowTacticsPanel(object tactic, bool interactive);
        void HideTacticsPanel();
    }
}
