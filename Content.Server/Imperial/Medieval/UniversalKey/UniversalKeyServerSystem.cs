using Content.Server.DoAfter;
using Content.Server.Hands.Systems;
using Content.Server.Imperial.Medieval.UniversalLock;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Imperial.Medieval.Ships.Anchor;
using Content.Shared.Interaction;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Tag;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

public sealed class UniversalKeyServerSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly HandsSystem _handsSystem = default!;
    [Dependency] private readonly AppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly AudioSystem _audioSystem = default!;
    [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UniversalKeyComponent, InteractUsingEvent>(OnAfterInteractUsing);

        SubscribeLocalEvent<UniversalKeyComponent, UniversalKeySetCodeMessage>(OnSetCodeReceived);

        SubscribeLocalEvent<UniversalLockComponent, InteractUsingEvent>(OnInteractUsing);

        SubscribeLocalEvent<UniversalLockComponent, UniversalKeySetupDoAfterEvent>(OnKeySetupDoAfterEvent);
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

    private void OnKeySetupDoAfterEvent(Entity<UniversalLockComponent> lockEntity, ref UniversalKeySetupDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (args.Used is not { } used)
            return;

        if (!TryComp<UniversalKeyComponent>(used, out var universalKeyComponent))
            return;

        if (universalKeyComponent.IsSetuped)
            return;

        SetupKey((used, universalKeyComponent), lockEntity.Comp.Code);
    }

    private void OnAfterInteractUsing(Entity<UniversalKeyComponent> keyEntity, ref InteractUsingEvent args)
    {
        if (_tags.HasTag(args.Used, (ProtoId<TagPrototype>)"Knife"))
        {
            OnKnifeUsed(keyEntity, args.User, args.Used);
            args.Handled = true;
        }
    }

    private void OnKnifeUsed(Entity<UniversalKeyComponent> keyEntity, EntityUid userUid, EntityUid knifeUid)
    {
        var state = new UniversalKeyBuiState();
        _uiSystem.SetUiState(keyEntity.Owner, UniversalSecurityUiKey.Key, state);

        _uiSystem.TryOpenUi(keyEntity.Owner, UniversalSecurityUiKey.Key, userUid);
        keyEntity.Comp.User = userUid;
        keyEntity.Comp.Knife = knifeUid;
    }

    private void OnSetCodeReceived(Entity<UniversalKeyComponent> keyEntity, ref UniversalKeySetCodeMessage args)
    {
        if (keyEntity.Comp.User is not { } user || !Exists(user) ||
            keyEntity.Comp.Knife is not { } knife || !Exists(knife) ||
            keyEntity.Comp.IsSetuped ||
            !_handsSystem.IsHolding(user, knife))
        {
            _uiSystem.CloseUi(keyEntity.Owner, UniversalSecurityUiKey.Key);
            return;
        }

        SetupKey(keyEntity, args.NewCode);
    }

    public void SetupKey(Entity<UniversalKeyComponent> keyEntity, int[] code)
    {
        keyEntity.Comp.Code = code;
        keyEntity.Comp.IsSetuped = true;
        _appearanceSystem.SetData(keyEntity, MedievalDoorKeyCheckVisual.State, "key_ready");
        _audioSystem.PlayPvs(keyEntity.Comp.KeySetupSound, keyEntity);
    }
}
