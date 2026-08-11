using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Ships.BoardingHook;

/// <summary>
/// A two-handed boarding hook which keeps a spawned hook connected to the item.
/// </summary>
[RegisterComponent]
public sealed partial class BoardingHookComponent : Component
{
    [DataField]
    public float BaseThrowDistance = 5f;

    [DataField]
    public float ThrowDistancePerStrength = 0.015f;

    [DataField]
    public float MaxTetherDistance = 7f;

    [DataField]
    public float Power = 10f;

    [DataField]
    public float OverloadCeilPerTile = 20f;

    [DataField]
    public SpriteSpecifier RopeSprite =
        new SpriteSpecifier.Rsi(new ResPath("Objects/Weapons/Guns/Launchers/grappling_gun.rsi"), "rope");

    public EntityUid? Projectile;

    public EntityUid? User;
}
