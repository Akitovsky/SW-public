using Content.Shared.Movement.Pulling.Components;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

[RegisterComponent]
public sealed partial class UniversalLockComponent : Component
{

    [DataField]
    public bool IsLocked = false;

    [DataField]
    public bool IsSetuped = false;



    // Sound

    [DataField]
    public SoundPathSpecifier LockSetupSound = new SoundPathSpecifier("/Audio/Imperial/Medieval/lockpick_next.ogg");
    [DataField]
    public SoundPathSpecifier KeySetupSound = new SoundPathSpecifier("/Audio/Imperial/Medieval/lockpick_next.ogg");



    // Hack
    [DataField]
    public int MaxValue = 5;

    [DataField]
    public int[] Code = new int[3];
    [DataField]
    public int Length = 3;

    [DataField]
    public int HackProgress = 0;

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>)), ViewVariables(VVAccess.ReadOnly)]
    public string EffectSoundOnNewLock = "/Audio/Imperial/Medieval/new_lock.ogg";
}
