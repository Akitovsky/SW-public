using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Magic.AugmentumBuff;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause, AutoGenerateComponentState]
public sealed partial class AugmentumBuffComponent : Component
{
    [ViewVariables, AutoPausedField, AutoNetworkedField]
    public TimeSpan EndTime;

    [DataField, AutoNetworkedField]
    public float SpeedModifier = 1.3f;

    [DataField, AutoNetworkedField]
    public float StaminaModifier = 2f;
}
