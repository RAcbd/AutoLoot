namespace AutoLoot;

using OriathHub.RemoteEnums.Entity;
using OriathHub.RemoteObjects.States.InGameStateObjects;
using OriathPlugins.Common.Loot;

internal sealed class ClickTracker
{
    private readonly Dictionary<uint, DateTime> ignoredEntityUntilUtc = new();
    private uint? pendingEntityId;
    private bool pendingIsWorldItem;
    private DateTime pendingSinceUtc = DateTime.MinValue;

    private const int PickupWaitMs = 120;
    private const int IgnoreAfterFailedMs = 500;

    public void Reset()
    {
        ignoredEntityUntilUtc.Clear();
        pendingEntityId = null;
        pendingIsWorldItem = false;
        pendingSinceUtc = DateTime.MinValue;
    }

    public bool IsIgnored(uint entityId, int clientX, int clientY)
    {
        _ = clientX;
        _ = clientY;
        Prune();
        return ignoredEntityUntilUtc.TryGetValue(entityId, out var entityUntil) &&
               entityUntil > DateTime.UtcNow;
    }

    public bool ShouldWaitForPickup() =>
        pendingEntityId.HasValue &&
        (DateTime.UtcNow - pendingSinceUtc).TotalMilliseconds < PickupWaitMs;

    public void BeginPickup(uint entityId, int clientX, int clientY, bool isWorldItem)
    {
        _ = clientX;
        _ = clientY;
        pendingEntityId = entityId;
        pendingIsWorldItem = isWorldItem;
        pendingSinceUtc = DateTime.UtcNow;
    }

    public void UpdateFrame(AreaInstance area)
    {
        Prune();
        if (!pendingEntityId.HasValue)
        {
            return;
        }

        var entityId = pendingEntityId.Value;
        if (WasRemovedThisFrame(area, entityId))
        {
            pendingEntityId = null;
            return;
        }

        if ((DateTime.UtcNow - pendingSinceUtc).TotalMilliseconds < PickupWaitMs)
        {
            return;
        }

        if (!TryFindEntity(area, entityId, out var entity))
        {
            pendingEntityId = null;
            return;
        }

        if (pendingIsWorldItem || GroundLootRules.IsWorldItemPlaceholder(entity))
        {
            pendingEntityId = null;
            return;
        }

        if (entity.EntityState is EntityStates.Useless)
        {
            pendingEntityId = null;
            return;
        }

        if (!GroundLootRules.IsGroundLootEntity(entity))
        {
            pendingEntityId = null;
            return;
        }

        ignoredEntityUntilUtc[entityId] = DateTime.UtcNow.AddMilliseconds(IgnoreAfterFailedMs);
        pendingEntityId = null;
    }

    private void Prune()
    {
        var now = DateTime.UtcNow;
        foreach (var key in ignoredEntityUntilUtc.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToList())
        {
            ignoredEntityUntilUtc.Remove(key);
        }
    }

    private static bool WasRemovedThisFrame(AreaInstance area, uint entityId)
    {
        foreach (var entity in area.EntitiesRemovedThisFrame)
        {
            if (entity.Id == entityId)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindEntity(AreaInstance area, uint entityId, out Entity entity)
    {
        foreach (var (_, candidate) in area.AwakeEntities)
        {
            if (candidate.Id == entityId)
            {
                entity = candidate;
                return true;
            }
        }

        entity = default!;
        return false;
    }
}
