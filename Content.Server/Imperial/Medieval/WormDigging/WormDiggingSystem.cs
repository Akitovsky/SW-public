using Content.Shared.DoAfter;
using Content.Shared.Fluids.Components;
using Content.Shared.Imperial.Medieval.WormDigging;
using Content.Shared.Interaction;
using Content.Shared.Kitchen.Components;
using Content.Shared.Maps;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Medieval.WormDigging;

public sealed class WormDiggingSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SharpComponent, ComponentStartup>(OnSharpStartup);
        SubscribeLocalEvent<SharpComponent, ComponentShutdown>(OnSharpShutdown);
        SubscribeLocalEvent<WormDiggingComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<WormDiggingComponent, WormDiggingDoAfterEvent>(OnDiggingComplete);
    }

    private void OnSharpStartup(Entity<SharpComponent> sharp, ref ComponentStartup args)
    {
        EnsureComp<WormDiggingComponent>(sharp);
    }

    private void OnSharpShutdown(Entity<SharpComponent> sharp, ref ComponentShutdown args)
    {
        RemCompDeferred<WormDiggingComponent>(sharp);
    }

    private void OnAfterInteract(Entity<WormDiggingComponent> sharp, ref AfterInteractEvent args)
    {
        if (args.Handled ||
            !args.CanReach ||
            !HasComp<SharpComponent>(sharp) ||
            args.Target != null && !HasComp<PuddleComponent>(args.Target) ||
            !IsValidTile(args.ClickLocation, sharp.Comp))
        {
            return;
        }

        var diggingEvent = new WormDiggingDoAfterEvent(GetNetCoordinates(args.ClickLocation));
        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            args.User,
            sharp.Comp.DiggingDuration,
            diggingEvent,
            sharp.Owner,
            used: sharp.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            DuplicateCondition = DuplicateConditions.SameTool | DuplicateConditions.SameEvent,
            BlockDuplicate = true,
            CancelDuplicate = false,
        };

        args.Handled = true;
        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnDiggingComplete(Entity<WormDiggingComponent> sharp, ref WormDiggingDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || !HasComp<SharpComponent>(sharp))
            return;

        args.Handled = true;

        var coordinates = GetCoordinates(args.ClickLocation);
        if (!coordinates.IsValid(EntityManager) || !IsValidTile(coordinates, sharp.Comp))
            return;

        Spawn(sharp.Comp.WormPrototype, coordinates);
    }

    private bool IsValidTile(EntityCoordinates coordinates, WormDiggingComponent component)
    {
        if (_turf.GetTileRef(coordinates) is not { } tile)
            return false;

        return component.ValidTiles.Contains(_turf.GetContentTileDefinition(tile).ID);
    }
}
