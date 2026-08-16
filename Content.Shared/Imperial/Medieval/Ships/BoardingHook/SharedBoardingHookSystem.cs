using Content.Shared.Timing;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Shared.Imperial.Medieval.Ships.BoardingHook;

public abstract class SharedBoardingHookSystem : EntitySystem
{
    [Dependency] protected readonly UseDelaySystem UseDelay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BoardingHookComponent, ShotAttemptedEvent>(OnShotAttempted);
    }

    protected virtual void OnShotAttempted(Entity<BoardingHookComponent> ent, ref ShotAttemptedEvent args)
    {
        if (UseDelay.IsDelayed(ent.Owner) ||
            TryComp<BasicEntityAmmoProviderComponent>(ent, out var ammo) && ammo.Count == 0)
        {
            args.Cancel();
        }
    }
}
