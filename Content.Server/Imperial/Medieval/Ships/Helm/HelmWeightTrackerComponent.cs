namespace Content.Server.Imperial.Medieval.Ships.Helm;

[RegisterComponent]
[Access(typeof(HelmWeightSystem))]
public sealed partial class HelmWeightTrackerComponent : Component
{
    public EntityUid? GridUid;

    public float Contribution;
}
