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
    private ActiveCoroutine? areaCoroutine;
    private FileInfo settingsFile = null!;

    public override string Name => "AutoLoot";

    public override string Description => "Value-aware ground loot clicking with priority pickup.";

    public override string Author => "Raff";

    public override string Version => "0.7.4";

    private string whitelistSearch = string.Empty;
    private string blacklistSearch = string.Empty;

    public override void OnEnable(bool isGameOpened)
    {
        settingsFile = new FileInfo(Path.Combine(DllDirectory, "config", "settings.json"));
        settings = JsonHelper.CreateOrLoadJsonFile<AutoLootSettings>(settingsFile);
        service.Initialize(DllDirectory);
        updateCoroutine = CoroutineHandler.Start(OnPerFrameUpdate(), $"{Name}.Update");
        areaCoroutine = CoroutineHandler.Start(OnAreaChange(), $"{Name}.AreaChange");
    }

    public override void OnDisable()
    {
        updateCoroutine?.Cancel();
        updateCoroutine = null;
        areaCoroutine?.Cancel();
        areaCoroutine = null;
    }

    public override void DrawDashboard()
    {
        DrawLiabilityDisclaimer();

        ImGui.Checkbox("Enable auto loot", ref settings.Enabled);
        ImGui.SameLine();
        if (ImGui.Button("Reset stats"))
        {
            service.ResetStats();
        }

        ImGui.Checkbox("Currency only", ref settings.CurrencyOnly);
        ImGui.TextDisabled("Picks up currency orbs, shards, fragments, runes, omens, and similar drops. Gold is never clicked (game auto-picks it).");
        DrawPickupPathFilter("Whitelist", settings.PickupWhitelist, ref whitelistSearch,
            "Always pick up matching currencies/items, even with currency-only on.");
        DrawPickupPathFilter("Blacklist", settings.PickupBlacklist, ref blacklistSearch,
            "Never pick up matching currencies/items (overrides whitelist and currency-only).");
        ImGui.Checkbox("Always pick up waystones/tablets", ref settings.AlwaysPickupWaystonesAndTablets);
        ImGui.Checkbox("Min value filter", ref settings.UseValueFilter);
        ImGui.InputDouble("Min divine value", ref settings.MinDivineValue, 0.1, 1.0);
        if (ImGui.Button("Reload prices"))
        {
            service.ReloadPrices();
        }

        ImGui.TextDisabled(service.PriceStatusMessage);
        ImGui.TextDisabled($"Pricing league: {HostPriceHelper.League}");
        ImGui.TextDisabled("Loot HUD and session totals are shown by BetterLootTracker.");
        ImGui.Checkbox("Show debug overlay", ref settings.ShowDebugOverlay);
        ImGui.Spacing();
        ImGui.TextColored(settings.AccentColor, "Status");
        ImGui.Text(service.StatusMessage);
        ImGui.TextDisabled($"Pickup attempts: {service.PickupAttempts}");
        ImGui.TextDisabled($"Loot in range: {service.LastCandidates.Count}");

        var diagnostics = service.LastDiagnostics;
        ImGui.TextDisabled(
            $"Scan: {diagnostics.AwakeEntities} awake, {service.CachedGroundLootCount} cached, " +
            $"{diagnostics.GroundEntities} ground, {diagnostics.FilteredByPath} filtered, " +
            $"{diagnostics.OutOfRange} far, {diagnostics.Clickable} clickable");
    }

    private static void DrawLiabilityDisclaimer()
    {
        var warning = ImGuiHelper.WarningTextColor();
        var body = new Vector4(1f, 0.93f, 0.86f, 1f);
        var divider = new Vector4(0.5f, 0.5f, 0.5f, 1f);

        ImGui.SetWindowFontScale(1.5f);
        ImGui.PushStyleColor(ImGuiCol.Text, warning);
        ImGui.Text("DISCLAIMER");
        ImGui.PopStyleColor();

        ImGui.PushStyleColor(ImGuiCol.Text, body);
        ImGui.SetWindowFontScale(1.3f);
        ImGui.TextWrapped("AutoLoot automates in-game mouse input.");
        ImGui.TextWrapped(
            "Raff and OriathHub are not liable for any account action, restriction, ban, or other consequence.");
        ImGui.TextWrapped("You use AutoLoot at your own risk and accept sole responsibility for your account.");
        ImGui.PopStyleColor();
        ImGui.SetWindowFontScale(1f);

        ImGui.Spacing();
        var lineWidth = ImGui.GetContentRegionAvail().X;
        var underscoreWidth = ImGui.CalcTextSize("_").X;
        if (underscoreWidth > 0f && lineWidth > 0f)
        {
            var count = Math.Max(1, (int)(lineWidth / underscoreWidth));
            ImGui.TextColored(divider, new string('_', count));
        }
        else
        {
            ImGui.Separator();
        }

        ImGui.Spacing();
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
        ImGui.TextDisabled("Max world distance for pickup. Tagged entities must also be in a nearby circle.");
        if (settings.PickupDistance < 120f)
        {
            ImGui.TextColored(new Vector4(0.95f, 0.55f, 0.2f, 1f),
                "Pickup distance is very low — most loot will be out of range.");
        }
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

    private void DrawPickupPathFilter(string title, List<string> entries, ref string search, string tooltip)
    {
        ImGui.TextUnformatted(title);
        ImGuiHelper.ToolTip(tooltip);

        var options = service.GetCurrencyOptions();
        var searchTerm = search.Trim();
        var filtered = options
            .Where(option => string.IsNullOrEmpty(searchTerm) ||
                             option.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                             option.Id.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 0.55f);
        ImGui.InputTextWithHint($"##{title}Search", "Search currencies...", ref search, 128);
        ImGui.SameLine();
        if (ImGui.BeginCombo($"Add to {title}##{title}Combo", "Choose currency..."))
        {
            foreach (var option in filtered.Take(50))
            {
                var alreadyListed = entries.Contains(option.Id, StringComparer.OrdinalIgnoreCase);
                if (ImGui.Selectable($"{option.Name}##{title}_{option.Id}", alreadyListed))
                {
                    if (!alreadyListed)
                    {
                        entries.Add(option.Id);
                    }
                }
            }

            ImGui.EndCombo();
        }

        if (entries.Count > 0 && ImGui.BeginChild($"##{title}List", new Vector2(0, Math.Min(120, 22 + entries.Count * 20))))
        {
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                var label = ResolvePickupListLabel(entry, options);
                ImGui.TextUnformatted(label);
                ImGui.SameLine();
                if (ImGui.SmallButton($"Remove##{title}_{i}"))
                {
                    entries.RemoveAt(i);
                }
            }

            ImGui.EndChild();
        }
        else
        {
            ImGui.TextDisabled($"No {title.ToLowerInvariant()} entries.");
        }
    }

    private static string ResolvePickupListLabel(string entry, IReadOnlyList<CurrencyOption> options)
    {
        foreach (var option in options)
        {
            if (option.Id.Equals(entry, StringComparison.OrdinalIgnoreCase))
            {
                return option.Name;
            }
        }

        return entry;
    }

    private IEnumerator<Wait> OnAreaChange()
    {
        while (true)
        {
            yield return new Wait(RemoteEvents.AreaChanged);
            service.OnAreaChanged();
        }
    }

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
            areaDetails.Id,
            areaDetails.IsTown,
            areaDetails.IsHideout,
            settings);
    }
}
