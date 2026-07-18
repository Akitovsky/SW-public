using Content.Shared.Imperial.Medieval.UniversalSecurity;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Robust.Server.Audio;
using Robust.Server.GameObjects;

namespace Content.Server.Imperial.Medieval.UniversalLock;

public sealed class UniversalLockServerSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly AudioSystem _audioSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;

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
        if (!_interactionSystem.InRangeUnobstructed(args.Actor, lockEntity.Owner))
            return;
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

        SetLockCode(lockEntity, args.NewCode, args.MaxValue, args.Name);
    }

    public void SetLockCode(Entity<UniversalLockComponent> lockEntity, int[] code, int maxValue, string name = "")
    {
        if (name != "")
            _metaDataSystem.SetEntityName(lockEntity, name + " " + Name(lockEntity));

        lockEntity.Comp.Code = code;
        lockEntity.Comp.IsSetuped = true;
        lockEntity.Comp.Name = name;
        lockEntity.Comp.Length = code.Length;
        lockEntity.Comp.MaxValue = maxValue;
        _audioSystem.PlayPvs(lockEntity.Comp.LockSetupSound, lockEntity);
    }

    public void SetLockCodeFraction(Entity<UniversalLockComponent> lockEntity, int[] code, int maxValue)
    {
        _metaDataSystem.SetEntityName(lockEntity, Name(lockEntity));
        lockEntity.Comp.Code = code;
        lockEntity.Comp.IsSetuped = true;
        lockEntity.Comp.Name = "";
        lockEntity.Comp.Length = code.Length;
        lockEntity.Comp.MaxValue = maxValue;
    }
}
