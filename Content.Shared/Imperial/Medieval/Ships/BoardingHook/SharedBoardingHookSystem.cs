using Content.Shared.Timing;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Shared.Imperial.Medieval.Ships.BoardingHook;

public sealed class SharedBoardingHookSystem : EntitySystem
{
    [Dependency] private readonly UseDelaySystem _useDelay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BoardingHookComponent, ShotAttemptedEvent>(OnShotAttempted);
    }

    private void OnShotAttempted(Entity<BoardingHookComponent> ent, ref ShotAttemptedEvent args)
    {
        if (_useDelay.IsDelayed(ent.Owner) ||
            TryComp<BasicEntityAmmoProviderComponent>(ent, out var ammo) && ammo.Count == 0)
        {
            args.Cancel();
        }
    }
}
