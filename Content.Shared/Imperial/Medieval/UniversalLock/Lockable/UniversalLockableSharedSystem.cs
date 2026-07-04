using Content.Shared.Administration.Logs;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval.Ships.Anchor;
using Content.Shared.Interaction;
using Content.Shared.Lock;
using Content.Shared.Popups;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Imperial.Medieval.UniversalSecurity;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Utility;
using System.Linq;

public sealed class UniversalLockableSharedSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly SharedDoorSystem _doorSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UniversalLockableComponent, ActivateInWorldEvent>(OnActivate, before: new[] { typeof(MedievalAnchorSystem), typeof(SharedStorageSystem), typeof(SharedDoorSystem) });
        SubscribeLocalEvent<UniversalLockableComponent, InteractUsingEvent>(OnInteractUsing, before: new[] { typeof(MedievalAnchorSystem), typeof(SharedStorageSystem), typeof(SharedDoorSystem) });

        SubscribeLocalEvent<UniversalLockableComponent, GetVerbsEvent<AlternativeVerb>>(AddAltVerbs);
        SubscribeLocalEvent<UniversalLockableComponent, UniversalLockableDoAfterEvent>(OnLockableDoAfter);

        SubscribeLocalEvent<UniversalLockableComponent, ExaminedEvent>(OnExamine);
    }

    private void OnActivate(Entity<UniversalLockableComponent> entity, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (IsLocked(entity))
        {
            _audioSystem.PlayPvs(entity.Comp.ActivateInWorldDenySound, entity);
            args.Handled = true;
        }
    }

    private void OnInteractUsing(Entity<UniversalLockableComponent> entity, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<UniversalKeyComponent>(args.Used, out var universalKeyComponent))
        {
            if (!universalKeyComponent.IsSetuped)
                return;

            OnUsedKey((args.Used, universalKeyComponent), entity, args.User);
            args.Handled = true;
            return;
        }

        if (TryComp<UniversalLockpickComponent>(args.Used, out var lockpickComponent))
            return;

        if (IsLocked(entity))
        {
            _audioSystem.PlayPvs(entity.Comp.InteractUsingDenySound, entity);
            args.Handled = true;
        }
    }

    private void AddAltVerbs(Entity<UniversalLockableComponent> lockableEntity, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!_itemSlots.TryGetSlot(lockableEntity, "lockSlot", out var slot))
            return;

        if (!IsLocked(lockableEntity))
        {
            var user = args.User;
            var time = slot.Locked ? lockableEntity.Comp.ToggleItemSlotLockedDoAfterTime : 0.1f;

            AlternativeVerb verb = new()
            {
                Text = Loc.GetString("toggle-item-slot-locked"),
                Act = () =>
                {
                    var doAfterArgs = new DoAfterArgs(
                    EntityManager,
                    user,
                    TimeSpan.FromSeconds(time),
                    new UniversalLockableDoAfterEvent(),
                    lockableEntity)
                    {
                        BreakOnMove = true,
                        BreakOnDamage = true,
                        NeedHand = true,
                        BlockDuplicate = true,
                    };

                    _doAfterSystem.TryStartDoAfter(doAfterArgs);
                }
            };

            args.Verbs.Add(verb);
        }
    }

    private void OnLockableDoAfter(Entity<UniversalLockableComponent> lockableEntity, ref UniversalLockableDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (!_itemSlots.TryGetSlot(lockableEntity, "lockSlot", out var slot))
            return;

        _itemSlots.SetLock(lockableEntity, slot, !slot.Locked);
    }

    private void OnExamine(Entity<UniversalLockableComponent> lockableEntity, ref ExaminedEvent args)
    {
        if (!_itemSlots.TryGetSlot(lockableEntity, "lockSlot", out var slot))
            return;

        if (slot.Item is not { } item)
            return;

        if (!TryComp<UniversalLockComponent>(item, out var lockComp))
            return;

        FormattedMessage msg = new FormattedMessage();

        if (slot.Locked)
            msg.PushColor(Color.Red);
        else
            msg.PushColor(Color.YellowGreen);

        msg.AddText(Name(item));
        msg.Pop();

        args.AddMessage(msg);
    }

    private bool IsLocked(Entity<UniversalLockableComponent> entity)
    {
        if (!_itemSlots.TryGetSlot(entity, "lockSlot", out var slot))
            return false;

        if (slot.Item is null)
            return false;

        if (!TryComp<UniversalLockComponent>(slot.Item, out var lockComp))
            return false;

        return lockComp.IsLocked;
    }

    private void OnUsedKey(Entity<UniversalKeyComponent> keyUsedEntity, Entity<UniversalLockableComponent> lockableEnitity, EntityUid user)
    {
        var keyComp = keyUsedEntity.Comp;

        if (!_itemSlots.TryGetSlot(lockableEnitity, "lockSlot", out var slot))
            return;

        if (slot.Item is not { } lockUid)
            return;

        if (!TryComp<UniversalLockComponent>(lockUid, out var lockComp))
            return;

        if (keyComp.IsSuperKey)
        {
            OnUsedKeySuccess((lockUid, lockComp), lockableEnitity, slot, user);
            return;
        }

        if (keyComp.Code.SequenceEqual(lockComp.Code))
            OnUsedKeySuccess((lockUid, lockComp), lockableEnitity, slot, user);
        else
            OnUsedKeyFail();
    }

    public void OnUsedKeySuccess(Entity<UniversalLockComponent> lockEntity, Entity<UniversalLockableComponent> lockableEnitity, ItemSlot slot, EntityUid user)
    {
        lockEntity.Comp.IsLocked = !lockEntity.Comp.IsLocked;

        _itemSlots.SetLock(lockableEnitity, slot, true);

        _adminLogger.Add(Content.Shared.Database.LogType.Action, Content.Shared.Database.LogImpact.Low, $"{ToPrettyString(user):user} used key, lock is locked = {lockEntity.Comp.IsLocked} {ToPrettyString(user):lockEntity}");

        if (TryComp<DoorBoltComponent>(lockableEnitity, out var doorBoltComponent))
        {
            _doorSystem.SetBoltsDown((lockableEnitity, doorBoltComponent), lockEntity.Comp.IsLocked, user, true);
            Dirty(lockableEnitity, doorBoltComponent);
        }

        if (!lockEntity.Comp.IsLocked)
        {
            _audioSystem.PlayPvs(lockableEnitity.Comp.LockUnlockedSound, lockableEnitity);
            _popupSystem.PopupClient(Loc.GetString("universal-lock-unlocked-popup"), user);
        }
        else
        {
            _audioSystem.PlayPvs(lockableEnitity.Comp.LockLockedSound, lockableEnitity);
            _popupSystem.PopupClient(Loc.GetString("universal-lock-locked-popup"), user);
        }

        if (!_containerSystem.TryGetContainer(lockableEnitity, "lockSlot", out var container))
            return;

        container.ShowContents = true;

        Dirty(lockEntity);
    }

    private void OnUsedKeyFail()
    {
        // TODO SOUND if lock and key have diffrentes codes (my american language is bad)
    }
}
