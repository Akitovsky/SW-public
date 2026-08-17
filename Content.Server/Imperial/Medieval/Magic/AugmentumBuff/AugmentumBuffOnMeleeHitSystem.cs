using Content.Shared.Imperial.Medieval.Magic.AugmentumBuff;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server.Imperial.Medieval.Magic.AugmentumBuff;

public sealed class AugmentumBuffOnMeleeHitSystem : EntitySystem
{
    [Dependency] private readonly AugmentumBuffSystem _augmentum = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AugmentumBuffOnMeleeHitComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<AugmentumBuffOnMeleeHitComponent> entity, ref MeleeHitEvent args)
    {
        foreach (var target in args.HitEntities)
        {
            _augmentum.ApplyAugmentumBuff(target, TimeSpan.FromSeconds(entity.Comp.Duration));
        }
    }
}
