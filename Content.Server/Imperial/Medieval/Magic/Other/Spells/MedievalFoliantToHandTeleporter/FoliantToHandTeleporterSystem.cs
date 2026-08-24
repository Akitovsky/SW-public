using System.Linq;
using Content.Server.Imperial.Medieval.Magic.BindStoreOnEquip;
using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;

namespace Content.Server.Imperial.Medieval.Magic.MedievalFoliantToHandTeleporter;
public sealed partial class FoliantToHandTeleporterSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly SharedStorageSystem _storageSystem = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FoliantToHandTeleporterComponent, MedievalAfterSpawnEntityBySpellEvent>(FindFoliant);
    }

    private void FindFoliant(EntityUid uid, FoliantToHandTeleporterComponent component, MedievalAfterSpawnEntityBySpellEvent args)
    {
        EntityUid playerUid = args.Performer;
        var query = EntityQueryEnumerator<BindStoreOnEquipComponent>();

        while (query.MoveNext(out var folliantUID, out var bindComp))
        {
            if (bindComp.BindedEntity == playerUid)
            {
                TryMoveToCarriedSlot(playerUid, folliantUID);
                break;
            }
        }
    }

    private void TryMoveToCarriedSlot(EntityUid playerUid, EntityUid foliantUid)
    {
        var carriedEntities = _inventorySystem.GetHandOrInventoryEntities(playerUid).ToList();

        // If the foliant is already in a hand, inventory slot, or carried storage,
        // leave it exactly where it is.
        if (carriedEntities.Contains(foliantUid))
            return;

        var current = foliantUid;
        while (_containerSystem.TryGetContainingContainer(current, out var container))
        {
            if (carriedEntities.Contains(container.Owner))
                return;

            current = container.Owner;
        }

        if (_handsSystem.TryPickupAnyHand(playerUid, foliantUid))
            return;

        foreach (var carried in carriedEntities)
        {
            if (carried == foliantUid || !TryComp<StorageComponent>(carried, out var storage))
                continue;

            if (_storageSystem.Insert(carried, foliantUid, out _, storageComp: storage, playSound: false))
                return;
        }
    }
}
