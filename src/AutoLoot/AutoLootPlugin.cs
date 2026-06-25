namespace AutoLoot;

using System.Collections;
using System.Numerics;
using Coroutine;
using ImGuiNET;
using OriathHub;
using OriathHub.CoroutineEvents;
using OriathHub.Plugin;
using OriathHub.RemoteEnums;
using OriathHub.Utils;
using OriathPlugins.Common.Loot;
using OriathPlugins.Common.Pricing;

public sealed class AutoLootPlugin : PluginBase
{
    private readonly AutoLootService service = new();
    private AutoLootSettings settings = new();
    private ActiveCoroutine? updateCoroutine;
    private FileInfo settingsFile = null!;

    public override string Version => "0.4.1";

    public override string Name => $"U | AutoLoot | v{Version}";

    public override string Description => "Value-aware ground loot clicking with priority pickup.";

    public override string Author => "Raff";

    public override void OnEnable(bool isGameOpened)
    {
        settingsFile = new FileInfo(Path.Combine(DllDirectory, "config", "settings.json"));
        settings = JsonHelper.CreateOrLoadJsonFile<AutoLootSettings>(settingsFile);
        var pluginsRoot = NinjaPriceLoader.GetPluginsRootFromDllDirectory(DllDirectory);
        service.Initialize(DllDirectory, pluginsRoot, settings);
        updateCoroutine = CoroutineHandler.Start(OnPerFrameUpdate(), $"{Name}.Update");
    }

    public override void OnDisable()
    {
        updateCoroutine?.Cancel();
        updateCoroutine = null;
    }

    public override void DrawDashboard()
    {
        ImGui.Checkbox("Enable auto loot", ref settings.Enabled);
        ImGui.SameLine();
        if (ImGui.Button("Reset stats"))
        {
            service.ResetStats();
        }

        ImGui.Checkbox("Stackables only", ref settings.LootStackablesOnly);
        ImGui.Checkbox("Always pick up waystones/tablets", ref settings.AlwaysPickupWaystonesAndTablets);
        ImGui.Checkbox("Min value filter", ref settings.UseValueFilter);
        ImGui.InputDouble("Min divine value", ref settings.MinDivineValue, 0.1, 1.0);
        ImGui.InputText("Ninja league", ref settings.NinjaLeague, 128);
        if (ImGui.Button("Reload prices"))
        {
            service.ReloadPrices(NinjaPriceLoader.GetPluginsRootFromDllDirectory(DllDirectory), settings.NinjaLeague);
        }

        ImGui.TextDisabled(service.PriceStatusMessage);
        ImGui.Checkbox("Show debug overlay", ref settings.ShowDebugOverlay);
        ImGui.Spacing();
        ImGui.TextColored(settings.AccentColor, "Status");
        ImGui.Text(service.StatusMessage);
        ImGui.TextDisabled($"Pickup attempts: {service.PickupAttempts}");
        ImGui.TextDisabled($"Loot in range: {service.LastCandidates.Count}");

        var diagnostics = service.LastDiagnostics;
        ImGui.TextDisabled(
            $"Scan: {diagnostics.AwakeEntities} awake, {diagnostics.GroundEntities} ground, " +
            $"{diagnostics.FilteredByPath} filtered, {diagnostics.OutOfRange} far, {diagnostics.Clickable} clickable");
    }

    public override void DrawSettings()
    {
        DrawDashboard();
        ImGui.Separator();
        ImGui.Checkbox("Pause in towns", ref settings.PauseInTown);
        ImGui.Checkbox("Pause in hideouts", ref settings.PauseInHideout);
        ImGui.Checkbox("Require game foreground", ref settings.RequireGameForeground);
        ImGui.Checkbox("Pause when panels open", ref settings.PauseWhenPanelsOpen);
        ImGui.Checkbox("Pause when chat open", ref settings.PauseWhenChatOpen);
        ImGui.DragFloat("Pickup distance", ref settings.PickupDistance, 1f, 50f, 900f);
        ImGui.InputInt("Min ms between pickups", ref settings.MinMsBetweenPickups);
        ImGui.InputInt("Click hold ms", ref settings.ClickHoldMs);
        ImGui.ColorEdit4("Dashboard accent", ref settings.AccentColor);
    }

    public override void DrawUI()
    {
        if (!settings.ShowDebugOverlay || !FocusHelper.IsGameOrOverlayForeground())
        {
            return;
        }

        if (Core.States.GameCurrentState != GameStateTypes.InGameState)
        {
            return;
        }

        var drawList = ImGui.GetBackgroundDrawList();
        foreach (var marker in service.DebugMarkers)
        {
            var color = marker.Kind switch
            {
                GroundLootMarkerKind.Clickable => 0xFF00FF00u,
                GroundLootMarkerKind.Valuable => 0xFF00FFFFu,
                GroundLootMarkerKind.Filtered => 0xFF4444FFu,
                GroundLootMarkerKind.OutOfRange => 0xFF0088FFu,
                _ => 0xFF888888u,
            };

            drawList.AddCircleFilled(marker.ClientPosition, 7f, color);
        }
    }

    public override void SaveSettings() => JsonHelper.SaveToFile(settings, settingsFile);

    private IEnumerator<Wait> OnPerFrameUpdate()
    {
        while (true)
        {
            yield return new Wait(OriathEvents.PerFrameDataUpdate);
            UpdatePickup();
        }
    }

    private void UpdatePickup()
    {
        if (Core.States.GameCurrentState != GameStateTypes.InGameState)
        {
            return;
        }

        var inGame = Core.States.InGameStateObject;
        var area = inGame.CurrentAreaInstance;
        var areaDetails = inGame.CurrentWorldInstance.AreaDetails;
        service.ProcessFrame(
            inGame,
            area,
            areaDetails.IsTown,
            areaDetails.IsHideout,
            settings);
    }
}
