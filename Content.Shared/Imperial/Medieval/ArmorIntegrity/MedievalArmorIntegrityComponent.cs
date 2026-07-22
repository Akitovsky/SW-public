using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.ArmorIntegrity;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MedievalArmorIntegrityComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<DamageTypePrototype>, MedievalArmorResistance> IntactResistances = new();

    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<DamageTypePrototype>, MedievalArmorResistance> BrokenResistances = new();

    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<DamageTypePrototype>, float> BreakageMultipliers = new()
    {
        { "Piercing", 1f },
        { "Blunt", 1f },
        { "Slash", 1f },
        { "Heat", 1f },
    };

    [DataField, AutoNetworkedField]
    public bool IsBroken;

    [DataField, AutoNetworkedField]
    public MedievalArmorRepairType RepairType = MedievalArmorRepairType.Smithing;

    // Reserved for broken armor effect
    [DataField, AutoNetworkedField]
    public EntProtoId? ArmorBrokenEffect;

    [DataField, AutoNetworkedField]
    public float ContainerArmorHP = 100f;

    [DataField, AutoNetworkedField]
    public float MaxArmorHP = 100f;

    [DataField, AutoNetworkedField]
    public float CurrentArmorHP = 100f;
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class MedievalArmorResistance
{
    [DataField]
    public float Coefficient = 1f;

    [DataField]
    public float FlatReduction;
}
