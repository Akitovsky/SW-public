using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Content.Server.DoAfter;
using Content.Server.Hands.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.LockDoor.Components;
using Content.Shared.Imperial.Medieval.UniversalSecurity;
using Content.Shared.Interaction;
using Content.Shared.Tag;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

public sealed class UniversalKeyServerSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly HandsSystem _handsSystem = default!;
    [Dependency] private readonly AppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly AudioSystem _audioSystem = default!;
    [Dependency] private readonly DoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly UniversalLockableServerSystem _lockableServerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UniversalKeyComponent, InteractUsingEvent>(OnAfterInteractUsing);

        SubscribeLocalEvent<UniversalKeyComponent, UniversalKeySetCodeMessage>(OnSetCodeReceived);

        SubscribeLocalEvent<UniversalLockComponent, InteractUsingEvent>(OnInteractUsing);

        SubscribeLocalEvent<UniversalLockComponent, UniversalKeySetupDoAfterEvent>(OnKeySetupDoAfterEvent);

        SubscribeLocalEvent<UniversalKeyComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<UniversalKeyComponent> keyEntity, ref MapInitEvent args)
    {
        if (!TryComp<KeyComponent>(keyEntity, out var keyComponent))
            return;

        if (keyComponent.Accesses[0] is not { } accessId)
            return;

        int[] newCode = GenerateSecureDeterministicArray(accessId, _lockableServerSystem.SecretKey, 32, 16);

        SetupKeyFraction(keyEntity, newCode, 9);
    }

    public static int[] GenerateSecureDeterministicArray(string factionId, string secretServerKey, int maxValue, int length)
    {
        if (length <= 0) return Array.Empty<int>();
        if (maxValue < 0) maxValue = 0;

        int[] result = new int[length];

        byte[] password = Encoding.UTF8.GetBytes(secretServerKey);
        byte[] salt = Encoding.UTF8.GetBytes(factionId);

        using (var kdf = new Rfc2898DeriveBytes(password, salt, iterations: 1, HashAlgorithmName.SHA256))
        {
            byte[] buffer = kdf.GetBytes(length * 4);

            for (int i = 0; i < length; i++)
            {
                int rawRandom = BitConverter.ToInt32(buffer, i * 4) & int.MaxValue;
                result[i] = rawRandom % (maxValue + 1);
            }
        }
        return result;
    }

    private void OnInteractUsing(Entity<UniversalLockComponent> lockEntity, ref InteractUsingEvent args)
    {
        if (!TryComp<UniversalKeyComponent>(args.Used, out var universalKeyComponent))
            return;

        if (universalKeyComponent.IsSetuped)
            return;

        if (!lockEntity.Comp.IsSetuped)
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, universalKeyComponent.DoAfterSetupTime, new UniversalKeySetupDoAfterEvent(), lockEntity, lockEntity, args.Used)
        {
            BreakOnMove = true,
            DistanceThreshold = 2.0f,
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

        if (!lockEntity.Comp.IsSetuped)
            return;

        universalKeyComponent.Name = lockEntity.Comp.Name;

        SetupKey((used, universalKeyComponent), lockEntity.Comp.Code, lockEntity.Comp.MaxValue);
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

        if (!_interactionSystem.InRangeUnobstructed(args.Actor, keyEntity.Owner))
        {
            _uiSystem.CloseUi(keyEntity.Owner, UniversalSecurityUiKey.Key);
            return;
        }

        keyEntity.Comp.Name = args.Name;
        SetupKey(keyEntity, args.NewCode, args.NewCode.Max());
    }

    public void SetupKey(Entity<UniversalKeyComponent> keyEntity, int[] code, int maxValue)
    {
        keyEntity.Comp.Code = code;
        keyEntity.Comp.IsSetuped = true;
        keyEntity.Comp.MaxToothValue = maxValue;
        keyEntity.Comp.MaxTeethCount = code.Length;
        _appearanceSystem.SetData(keyEntity, MedievalDoorKeyCheckVisual.State, "key_ready");
        _audioSystem.PlayPvs(keyEntity.Comp.KeySetupSound, keyEntity);
        _metaDataSystem.SetEntityName(keyEntity, keyEntity.Comp.Name + " " + Name(keyEntity));
    }

    public void SetupKeyFraction(Entity<UniversalKeyComponent> keyEntity, int[] code, int maxValue)
    {
        keyEntity.Comp.Code = code;
        keyEntity.Comp.IsSetuped = true;
        keyEntity.Comp.MaxToothValue = maxValue;
        keyEntity.Comp.MaxTeethCount = code.Length;
    }
}
