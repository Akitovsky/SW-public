using System.Collections.Generic;

namespace Content.Server.Imperial.Medieval.Ships.Helm;

[RegisterComponent]
public sealed partial class HelmGridComponent : Component
{
    public readonly HashSet<EntityUid> Helms = new();

    public readonly HashSet<EntityUid> Sails = new();

    public readonly HashSet<EntityUid> SteeringOars = new();

    public int TileCount;

    public bool TileCountInitialized;

    public float SteeringPower;

    public float SailsEfficiency;

    public float TotalWeight;
}
