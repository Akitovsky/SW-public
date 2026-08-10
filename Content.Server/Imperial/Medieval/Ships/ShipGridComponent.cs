using System.Collections.Generic;

namespace Content.Server.Imperial.Medieval.Ships;

/// <summary>
/// Server-side cache for structural and aggregate ship data.
/// The full grid is scanned only once when this component starts; subsequent changes are event-driven.
/// </summary>
[RegisterComponent]
public sealed partial class ShipGridComponent : Component
{
    public readonly HashSet<EntityUid> Sails = new();

    public readonly HashSet<EntityUid> Anchors = new();

    public EntityUid? Helm;

    public int TileCount;

    public int FloodContribution;

    public float SteeringPower;

    public float SailsEfficiency;

    public float TotalWeight;

    public bool HasLoweredAnchor;

    public TimeSpan? WavesDisabledAt;
}

/// <summary>
/// Raised when the aggregate anchor state of a ship changes.
/// </summary>
[ByRefEvent]
public readonly record struct ShipAnchorStateChangedEvent(bool HasLoweredAnchors);
