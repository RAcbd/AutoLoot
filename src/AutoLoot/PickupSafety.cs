namespace AutoLoot;

using OriathHub;
using OriathHub.RemoteObjects.States;
using OriathHub.Utils;

internal static class PickupSafety
{
    public static bool CanPickup(InGameState inGame, AutoLootSettings settings)
    {
        if (settings.RequireGameForeground &&
            !FocusHelper.IsGameForeground() &&
            !Core.Process.Foreground)
        {
            return false;
        }

        var ui = inGame.GameUi;
        if (ui is null)
        {
            return true;
        }

        if (settings.PauseWhenPanelsOpen)
        {
            if (ui.IsAnyLargePanelOpen ||
                ui.IsSkillTreeOpen ||
                ui.IsAtlasMapOpen ||
                ui.LeftPanel.IsVisible ||
                ui.RightPanel.IsVisible ||
                ui.WorldMapPanel.IsVisible)
            {
                return false;
            }
        }

        if (settings.PauseWhenChatOpen && ui.ChatParent.IsChatActive)
        {
            return false;
        }

        return true;
    }

    public static string GetPauseReason(InGameState inGame, AutoLootSettings settings)
    {
        if (settings.RequireGameForeground &&
            !FocusHelper.IsGameForeground() &&
            !Core.Process.Foreground)
        {
            return "Paused (game not focused — click back into PoE)";
        }

        var ui = inGame.GameUi;
        if (ui is null)
        {
            return "Paused";
        }

        if (settings.PauseWhenPanelsOpen)
        {
            if (ui.IsAnyLargePanelOpen ||
                ui.IsSkillTreeOpen ||
                ui.IsAtlasMapOpen ||
                ui.LeftPanel.IsVisible ||
                ui.RightPanel.IsVisible ||
                ui.WorldMapPanel.IsVisible)
            {
                return "Paused (panel open)";
            }
        }

        if (settings.PauseWhenChatOpen && ui.ChatParent.IsChatActive)
        {
            return "Paused (chat open)";
        }

        return "Paused";
    }
}
