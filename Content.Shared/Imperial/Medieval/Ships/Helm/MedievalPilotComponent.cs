using System;
using Robust.Shared.GameObjects;

namespace Content.Server.Imperial.Medieval.Ships.Helm;

[RegisterComponent]
public sealed partial class MedievalPilotComponent : Component
{
    [DataField]
    public EntityUid? HelmEntity;

    [DataField]
    public EntityUid? UsingSound;

    [DataField]
    public TimeSpan LastRotationUpdate;

    [DataField]
    public float RotationBudget;
}
