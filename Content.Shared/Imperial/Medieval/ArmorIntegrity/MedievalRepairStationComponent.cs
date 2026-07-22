namespace Content.Shared.Imperial.Medieval.ArmorIntegrity;

[RegisterComponent]
public sealed partial class MedievalRepairStationComponent : Component
{
    [DataField]
    public MedievalArmorRepairType RepairType = MedievalArmorRepairType.Smithing;

    [DataField]
    public float StationMaxArmorRemovalModifier = 1f;

    [DataField]
    public float RepairDelayModifier = 1f;
}
