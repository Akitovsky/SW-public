using Content.Shared.Containers.ItemSlots;
using Content.Shared.Imperial.Medieval.UniversalSecurity;
using Content.Shared.Storage.Components;

public sealed class UniversalLockableSharedSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UniversalLockableComponent, StorageCloseAttemptEvent>(OnStorageCloseAttempt);
    }

    private void OnStorageCloseAttempt(EntityUid uid, UniversalLockableComponent component, ref StorageCloseAttemptEvent args)
    {
        var lockableEntity = (uid, component);

        if (!IsLocked(lockableEntity))
            return;

        args.Cancelled = true;
    }

    private bool IsLocked(Entity<UniversalLockableComponent> entity)
    {
        if (!_itemSlots.TryGetSlot(entity, "lockSlot", out var slot) || slot.Item is not { } lockUid)
            return false;

        return TryComp<UniversalLockComponent>(lockUid, out var lockComp) && lockComp.IsLocked;
    }
}
