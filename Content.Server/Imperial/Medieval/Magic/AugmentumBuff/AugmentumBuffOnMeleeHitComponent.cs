namespace Content.Server.Imperial.Medieval.Magic.AugmentumBuff;

[RegisterComponent]
public sealed partial class AugmentumBuffOnMeleeHitComponent : Component
{
    [DataField(required: true)]
    public float Duration;
}
