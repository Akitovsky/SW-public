using Content.Server.DoAfter;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.Ships.Anchor;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Robust.Server.Audio;
using Robust.Server.GameObjects;

namespace Content.Server.Imperial.Medieval.UniversalLock;

public sealed class UniversalLockSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly AppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly AudioSystem _audioSystem = default!;
    [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UniversalLockComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<UniversalLockComponent, UniversalLockSetCodeMessage>(OnSetCodeReceived);
        SubscribeLocalEvent<UniversalLockComponent, InteractUsingEvent>(OnInteractUsing);

        SubscribeLocalEvent<UniversalLockComponent, UniversalKeySetupDoAfterEvent>(OnKeySetupDoAfterEvent);
    }

    private void OnUseInHand(Entity<UniversalLockComponent> lockEntity, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (lockEntity.Comp.IsSetuped)
            return;

        var state = new UniversalLockBuiState(lockEntity.Comp.MaxValue, lockEntity.Comp.Length);
        _uiSystem.SetUiState(lockEntity.Owner, UniversalLockUiKey.Key, state);
        _uiSystem.TryOpenUi(lockEntity.Owner, UniversalLockUiKey.Key, args.User);

        args.Handled = true;
    }

    private void OnInteractUsing(Entity<UniversalLockComponent> entity, ref InteractUsingEvent args)
    {
        if (!TryComp<UniversalKeyComponent>(args.Used, out var universalKeyComponent))
            return;

        if (universalKeyComponent.IsSetuped)
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, universalKeyComponent.DoAfterSetupTime, new UniversalKeySetupDoAfterEvent(), entity, used: args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BlockDuplicate = true,
            BreakOnDropItem = true,
            BreakOnHandChange = true
        };

        _doAfterSystem.TryStartDoAfter(doAfterArgs);
    }

    private void OnKeySetupDoAfterEvent(Entity<UniversalLockComponent> entity, ref UniversalKeySetupDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (args.Used is not { } used)
            return;

        if (!TryComp<UniversalKeyComponent>(used, out var universalKeyComponent))
            return;

        if (universalKeyComponent.IsSetuped)
            return;

        universalKeyComponent.Code = entity.Comp.Code;
        universalKeyComponent.IsSetuped = true;
        _appearanceSystem.SetData(used, MedievalDoorKeyCheckVisual.State, "key_ready");
        _audioSystem.PlayPvs(entity.Comp.KeySetupSound, used);
    }

    private void OnSetCodeReceived(Entity<UniversalLockComponent> lockEntity, ref UniversalLockSetCodeMessage args)
    {
        if (lockEntity.Comp.IsSetuped)
            return;
        if (args.NewCode.Length != lockEntity.Comp.Length)
            return;

        for (int i = 0; i < args.NewCode.Length; i++)
        {
            if (args.NewCode[i] < 0)
                return;
            if (args.NewCode[i] > lockEntity.Comp.MaxValue)
                return;
        }

        lockEntity.Comp.Code = args.NewCode;
        lockEntity.Comp.IsSetuped = true;
        _audioSystem.PlayPvs(lockEntity.Comp.LockSetupSound, lockEntity);
    }
}