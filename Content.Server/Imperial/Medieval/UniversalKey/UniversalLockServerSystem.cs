using System.Threading;
using Content.Server.DoAfter;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.Ships.Anchor;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Robust.Server.Audio;
using Robust.Server.GameObjects;

namespace Content.Server.Imperial.Medieval.UniversalLock;

public sealed class UniversalLockServerSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly AppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly AudioSystem _audioSystem = default!;
    [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly UniversalKeyServerSystem _keySystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UniversalLockComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<UniversalLockComponent, UniversalLockSetCodeMessage>(OnSetCodeReceived);
    }

    private void OnUseInHand(Entity<UniversalLockComponent> lockEntity, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (lockEntity.Comp.IsSetuped)
            return;

        var state = new UniversalLockBuiState(lockEntity.Comp.MaxValue, lockEntity.Comp.Length);
        _uiSystem.SetUiState(lockEntity.Owner, UniversalSecurityUiKey.Lock, state);
        _uiSystem.TryOpenUi(lockEntity.Owner, UniversalSecurityUiKey.Lock, args.User);

        args.Handled = true;
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
