[RegisterComponent]
public sealed partial class UniversalKeyComponent : Component
{
    [DataField]
    public float DoAfterSetupTime = 5;

    [DataField]
    public int[] Code = new int[3];

    [DataField]
    public bool IsSetuped = false;

    [DataField]
    public bool IsSuperKey = false;
}
