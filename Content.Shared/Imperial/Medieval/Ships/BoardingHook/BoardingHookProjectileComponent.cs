using System.Numerics;

namespace Content.Shared.Imperial.Medieval.Ships.BoardingHook;

/// <summary>
/// Runtime state of the hook thrown by a <see cref="BoardingHookComponent"/>.
/// </summary>
[RegisterComponent]
public sealed partial class BoardingHookProjectileComponent : Component
{
    public EntityUid HookItem;

    public EntityUid User;

    public EntityUid OriginGrid;

    public Vector2 ThrowOrigin;

    public float MaxThrowDistance;

    public bool Anchored;
}

