using Content.Shared.Administration.Logs;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval.UniversalSecurity;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Utility;
using System.Linq;
using Content.Shared.Imperial.Medieval.Ships.Anchor;
using Robust.Shared.Random;
using Content.Shared.Imperial.LockDoor.Components;
using Content.Server.Imperial.Medieval.UniversalLock;
using Robust.Shared.Timing;
using Content.Server.Administration.Commands;
using Robust.Shared.Audio;
using Content.Shared.Lock;
public sealed class UniversalLockableServerSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly SharedDoorSystem _doorSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly UniversalLockServerSystem _universalLockSystem = default!;
    [Dependency] private readonly LockSystem _lockSystem = default!;

    public int RandomedSeed;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UniversalLockableComponent, ActivateInWorldEvent>(OnActivate, before: new[] { typeof(MedievalAnchorSystem), typeof(SharedStorageSystem), typeof(SharedDoorSystem) });
        SubscribeLocalEvent<UniversalLockableComponent, InteractUsingEvent>(OnInteractUsing, before: new[] { typeof(MedievalAnchorSystem), typeof(SharedStorageSystem), typeof(SharedDoorSystem) });

        SubscribeLocalEvent<UniversalLockableComponent, GetVerbsEvent<AlternativeVerb>>(AddAltVerbs);
        SubscribeLocalEvent<UniversalLockableComponent, UniversalLockableDoAfterEvent>(OnLockableDoAfter);

        SubscribeLocalEvent<UniversalLockableComponent, ExaminedEvent>(OnExamine);

        SubscribeLocalEvent<UniversalLockableComponent, MapInitEvent>(OnMapInit);

        RandomedSeed = _random.Next(1000000000);
    }

    private void OnMapInit(Entity<UniversalLockableComponent> lockableEntity, ref MapInitEvent args)
    {
        Timer.Spawn(0, () =>
        {
            if (!TryComp<LockDoorComponent>(lockableEntity, out var lockDoorComponent))
                return;

            if (lockDoorComponent.AccessLists[0] is not { } accessId)
                return;

            if (!_itemSlots.TryGetSlot(lockableEntity, "lockSlot", out var slot))
                return;

            if (slot.Item is not { } lockUid)
                return;

            if (!TryComp<UniversalLockComponent>(lockUid, out var lockComponent))
                return;

            if (!TryComp<DoorBoltComponent>(lockableEntity, out var doorBoltComponent))
                return;

            int[] newCode = GenerateFactionArray(accessId, RandomedSeed, 16, 9);

            _universalLockSystem.SetLockCodeFraction((lockUid, lockComponent), newCode, 16);
            _itemSlots.TryInsert(lockableEntity, slot, lockUid, null, true);

            if (doorBoltComponent.BoltsDown)
                OnFractionLockSpawn((lockUid, lockComponent), lockableEntity, slot);

            RemComp(lockableEntity, doorBoltComponent);
        });
    }

    public static int[] GenerateFactionArray(string factionId, int randomNumber, int maxValue, int length)
    {
        // Защита от некорректной длины
        if (length <= 0)
            return Array.Empty<int>();

        // Защита от некорректного максимума
        if (maxValue < 0)
            maxValue = 0;

        int[] result = new int[length];

        for (int i = 0; i < length; i++)
        {
            // Комбинируем фракцию, число и текущий индекс, чтобы элементы отличались друг от друга
            int hash = HashCode.Combine(factionId, randomNumber, i);

            // Убираем знак минус (делаем число строго положительным)
            int positiveHash = hash & int.MaxValue;

            // Ограничиваем число до maxValue включительно.
            // Например, если maxValue = 9, то % 10 вернет значение от 0 до 9.
            result[i] = positiveHash % (maxValue + 1);
        }

        return result;
    }

    private void OnActivate(Entity<UniversalLockableComponent> entity, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (IsLocked(entity))
        {
            AudioParams audioParams = new AudioParams()
            {
                Volume = -10,
            };
            _audioSystem.PlayPvs(entity.Comp.ActivateInWorldDenySound, entity, audioParams);
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
            AudioParams audioParams = new AudioParams()
            {
                Volume = -10,
            };
            _audioSystem.PlayPvs(entity.Comp.InteractUsingDenySound, entity, audioParams);
            args.Handled = true;
        }
    }

    private void AddAltVerbs(Entity<UniversalLockableComponent> lockableEntity, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!_itemSlots.TryGetSlot(lockableEntity, "lockSlot", out var slot))
            return;

        if (slot.Item is null)
            return;

        if (IsLocked(lockableEntity))
            return;

        var user = args.User;
        var time = slot.Locked ? lockableEntity.Comp.ToggleItemSlotLockedDoAfterTime : 0.1f;

        AlternativeVerb verb = new()
        {
            Text = Loc.GetString("universal-security-eject-lock"),
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

    private void OnLockableDoAfter(Entity<UniversalLockableComponent> lockableEntity, ref UniversalLockableDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (!_itemSlots.TryGetSlot(lockableEntity, "lockSlot", out var slot))
            return;

        if (slot.Item is not { } lockUid)
            return;

        _itemSlots.SetLock(lockableEntity, slot, !slot.Locked);

        if (!slot.Locked)
            _itemSlots.TryEjectToHands(lockUid, slot, args.User);
    }

    private void OnExamine(Entity<UniversalLockableComponent> lockableEntity, ref ExaminedEvent args)
    {
        if (!_itemSlots.TryGetSlot(lockableEntity, "lockSlot", out var slot))
            return;

        if (slot.Item is not { } item)
            return;

        if (!TryComp<UniversalLockComponent>(item, out var lockComponent))
            return;

        FormattedMessage msg = new FormattedMessage();
        msg.PushColor(Color.Yellow);
        msg.AddText(Name(item) + " ");
        if (lockComponent.IsLocked)
            msg.AddText(Loc.GetString("universal-lock-examine-is-locked"));
        else
            msg.AddText(Loc.GetString("universal-lock-examine-is-unlocked"));

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

    public void OnUsedKeySuccess(Entity<UniversalLockComponent> lockEntity, Entity<UniversalLockableComponent> lockableEnitity, ItemSlot slot, EntityUid? user)
    {
        lockEntity.Comp.IsLocked = !lockEntity.Comp.IsLocked;

        _itemSlots.SetLock(lockableEnitity, slot, true);

        _adminLogger.Add(Content.Shared.Database.LogType.Action, Content.Shared.Database.LogImpact.Low, $"{ToPrettyString(user):user} used key, lock is locked = {lockEntity.Comp.IsLocked} {ToPrettyString(user):lockEntity}");

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

        if (!TryComp<LockComponent>(lockableEnitity, out var lockComponent))
            return;

        _lockSystem.ToggleLock(lockableEnitity, null, lockComponent);
    }

    public void OnFractionLockSpawn(Entity<UniversalLockComponent> lockEntity, Entity<UniversalLockableComponent> lockableEnitity, ItemSlot slot)
    {
        lockEntity.Comp.IsLocked = !lockEntity.Comp.IsLocked;

        _itemSlots.SetLock(lockableEnitity, slot, true);
    }

    private void OnUsedKeyFail()
    {
        // TODO SOUND if lock and key have diffrentes codes (my american language is bad)
    }
}
