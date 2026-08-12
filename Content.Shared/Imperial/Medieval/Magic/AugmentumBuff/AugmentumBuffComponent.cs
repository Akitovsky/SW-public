using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Magic.AugmentumBuff;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause, AutoGenerateComponentState]
public sealed partial class AugmentumBuffComponent : Component
{
    [ViewVariables, AutoPausedField, AutoNetworkedField]
    public TimeSpan EndTime;

    [ViewVariables, AutoNetworkedField]
    public bool OwnsStaminaModifier;

    [DataField, AutoNetworkedField]
    public float SpeedModifier = 1.3f;

    [ViewVariables]
    public bool TimerRunning;
}
