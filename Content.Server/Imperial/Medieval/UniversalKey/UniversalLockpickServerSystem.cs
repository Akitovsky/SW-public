using System.Linq;
using Content.Server.DoAfter;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.Ships.Anchor;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Random;

public sealed partial class UniversalLockpickServerSystem : EntitySystem
{

    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly UniversalLockableSharedSystem _lockableSystem = default!;
    [Dependency] private readonly AudioSystem _audioSystem = default!;
    [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UniversalLockpickComponent, AfterInteractEvent>(OnLockpickInteract);

        SubscribeLocalEvent<UniversalLockpickComponent, UniversalLockpickSetCodeMessage>(OnSetCodeReceived);

        SubscribeLocalEvent<UniversalLockpickComponent, UniversalLockpickHackDoAfterEvent>(OnHackDoAfter);
    }

    private void OnLockpickInteract(Entity<UniversalLockpickComponent> lockpickEntity, ref AfterInteractEvent args)
    {
        if (args.Handled)
            return;

        if (args.Target is not { } lockableUid)
            return;

        if (!TryComp<UniversalLockableComponent>(args.Target, out var lockableComponent))
            return;

        if (!_itemSlots.TryGetSlot(lockableUid, "lockSlot", out var slot))
            return;

        if (slot.Item is not { } lockUid)
            return;

        if (!TryComp<UniversalLockComponent>(lockUid, out var lockComponent))
            return;

        if (!lockComponent.IsSetuped)
            return;

        lockpickEntity.Comp.LockUid = lockUid;
        lockpickEntity.Comp.LockableUid = lockableUid;
        lockpickEntity.Comp.User = args.User;

        var state = new UniversalLockpickBuiState(lockComponent.MaxValue, lockComponent.Length, new int[lockComponent.Length]);
        _uiSystem.SetUiState(lockpickEntity.Owner, UniversalSecurityUiKey.Lockpick, state);
        _uiSystem.TryOpenUi(lockpickEntity.Owner, UniversalSecurityUiKey.Lockpick, args.User);

        args.Handled = true;
    }

    private void OnSetCodeReceived(Entity<UniversalLockpickComponent> lockpickEntity, ref UniversalLockpickSetCodeMessage args)
    {
        if (lockpickEntity.Comp.LockUid is not { } lockUid || !Exists(lockUid) ||
            lockpickEntity.Comp.LockableUid is not { } lockableUid || !Exists(lockableUid) ||
            lockpickEntity.Comp.User is not { } user || !Exists(user) ||
            !TryComp<UniversalLockComponent>(lockUid, out var lockComponent) ||
            !TryComp<UniversalLockableComponent>(lockableUid, out var lockableComponent) ||
            !TryComp<SkillsComponent>(user, out var skillComponent) ||
            !lockComponent.IsSetuped ||
            !_itemSlots.TryGetSlot(lockableUid, "lockSlot", out var slot))
        {
            _uiSystem.CloseUi(lockpickEntity.Owner, UniversalSecurityUiKey.Lockpick);
            return;
        }

        var ev = new UniversalLockpickHackDoAfterEvent
        {
            NewCode = args.NewCode
        };

        var doAfterTime = lockpickEntity.Comp.HackTime / (skillComponent.Levels["Agility"] / 5f);

        var doAfterArgs = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(doAfterTime), ev, lockpickEntity)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BlockDuplicate = true,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            DistanceThreshold = 0.5f
        };

        _doAfterSystem.TryStartDoAfter(doAfterArgs);
        _audioSystem.PlayPvs(new SoundPathSpecifier(lockpickEntity.Comp.EffectSoundOnNext), lockableUid);
    }

    private void OnHackDoAfter(Entity<UniversalLockpickComponent> lockpickEntity, ref UniversalLockpickHackDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
        {
            _uiSystem.CloseUi(lockpickEntity.Owner, UniversalSecurityUiKey.Lockpick);
            args.Handled = true;
            return;
        }

        if (lockpickEntity.Comp.LockUid is not { } lockUid || !Exists(lockUid) ||
            lockpickEntity.Comp.LockableUid is not { } lockableUid || !Exists(lockableUid) ||
            lockpickEntity.Comp.User is not { } user || !Exists(user) ||
            !TryComp<UniversalLockComponent>(lockUid, out var lockComponent) ||
            !TryComp<UniversalLockableComponent>(lockableUid, out var lockableComponent) ||
            !TryComp<SkillsComponent>(user, out var skillComponent) ||
            !lockComponent.IsSetuped ||
            !_itemSlots.TryGetSlot(lockableUid, "lockSlot", out var slot))
        {
            _uiSystem.CloseUi(lockpickEntity.Owner, UniversalSecurityUiKey.Lockpick);
            args.Handled = true;
            return;
        }


        var breakChance = Math.Clamp(lockpickEntity.Comp.BreakChance / MathF.Max(0.1f, skillComponent.Levels["Agility"] / 5f), 0.01f, 0.75f);
        if (_random.Prob(breakChance))
        {
            OnLockpickBreak(lockpickEntity);
            args.Handled = true;
            return;
        }

        if (args.NewCode.SequenceEqual(lockComponent.Code))
        {
            _lockableSystem.OnUsedKeySuccess((lockUid, lockComponent), (lockableUid, lockableComponent), slot, user);
            _audioSystem.PlayPvs(new SoundPathSpecifier(lockpickEntity.Comp.EffectSoundOnSucces), lockUid);
        }

        int[] stateCode = new int[lockComponent.Length];
        for (var i = 0; i < args.NewCode.Length; i++)
        {
            if (lockComponent.Code[i] == args.NewCode[i]) stateCode[i] = 255;
            else if (lockComponent.Code[i] > args.NewCode[i]) stateCode[i] = 1;
            else if (lockComponent.Code[i] < args.NewCode[i]) stateCode[i] = -1;
        }

        var state = new UniversalLockpickBuiState(lockComponent.MaxValue, lockComponent.Length, stateCode);
        _uiSystem.SetUiState(lockpickEntity.Owner, UniversalSecurityUiKey.Lockpick, state);
        _audioSystem.PlayPvs(new SoundPathSpecifier(lockpickEntity.Comp.EffectSoundOnNext), lockpickEntity);

        if (args.NewCode.SequenceEqual(lockComponent.Code))
        {
            lockpickEntity.Comp.LockUid = null;
            lockpickEntity.Comp.LockableUid = null;
            lockpickEntity.Comp.User = null;
        }
    }

    private void OnLockpickBreak(Entity<UniversalLockpickComponent> lockpickEntity)
    {
        AudioParams audioParams = new AudioParams()
        {
            Volume = -15
        };

        _audioSystem.PlayPvs(new SoundPathSpecifier(lockpickEntity.Comp.EffectSoundOnBreak), Transform(lockpickEntity).Coordinates);
        QueueDel(lockpickEntity);
    }
}
