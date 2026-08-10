using System;
using System.Collections.Generic;

namespace Content.Shared.Imperial.Medieval.Ships.Helm;

[RegisterComponent]
public sealed partial class HelmComponent : Component
{
    [DataField("helmRotation")]
    public float HelmRotation;

    [DataField("rotationStep")]
    public float RotationStep = 50f;

    [DataField("steeringAngleForMaxTurn")]
    public float SteeringAngleForMaxTurn = 45f;

    [DataField("turnImpulseScalar")]
    public float TurnImpulseScalar = 20f;

    [DataField("stabilizingImpulseScalar")]
    public float StabilizingImpulseScalar = 80f;

    [DataField("minMotionFactor")]
    public float MinMotionFactor = 0.25f;

    [DataField("minShipWeight")]
    public float MinShipWeight = 10f;

    [DataField]
    public float RotationSyncMaxBudgetSeconds = 1.5f;

    [DataField]
    public float CacheRefreshInterval = 1f;

    public EntityUid? GridUid;

    public TimeSpan NextCacheUpdate;

    public readonly HashSet<EntityUid> Sails = new();

    public readonly HashSet<EntityUid> SteeringOars = new();

    public float CachedShipWeight;

    public float CachedOverloadCeil;

    public float CachedSteeringPower;

    public float CachedSailsEfficiency;
}
