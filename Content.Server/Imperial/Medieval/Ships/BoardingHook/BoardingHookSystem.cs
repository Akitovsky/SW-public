using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Shared.CombatMode;
using Content.Shared.DoAfter;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Ships.BoardingHook;
using Content.Shared.Imperial.Medieval.Ships.Islands;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server.Imperial.Medieval.Ships.BoardingHook;

public sealed class BoardingHookSystem : EntitySystem
{
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedSkillsSystem _skills = default!;
    [Dependency] private readonly ShipGridSystem _shipGrid = default!;
    [Dependency] private readonly ThrownItemSystem _thrown = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BoardingHookComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<BoardingHookComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<BoardingHookComponent, OnEmptyGunShotEvent>(OnEmptyGunShot);
        SubscribeLocalEvent<BoardingHookComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<BoardingHookComponent, GotUnequippedHandEvent>(OnUnequipped);
        SubscribeLocalEvent<BoardingHookComponent, ItemUnwieldedEvent>(OnUnwielded);
        SubscribeLocalEvent<BoardingHookComponent, ComponentShutdown>(OnHookItemShutdown);
        SubscribeLocalEvent<BoardingHookComponent, BoardingHookPullDoAfterEvent>(OnPullDoAfter);

        SubscribeLocalEvent<BoardingHookProjectileComponent, LandEvent>(OnProjectileLand);
        SubscribeLocalEvent<BoardingHookProjectileComponent, InteractHandEvent>(OnProjectileInteract);
        SubscribeLocalEvent<BoardingHookProjectileComponent, BoardingHookRemoveDoAfterEvent>(OnRemoveDoAfter);
        SubscribeLocalEvent<BoardingHookProjectileComponent, ComponentShutdown>(OnProjectileShutdown);
    }

    private void OnAttemptShoot(Entity<BoardingHookComponent> ent, ref AttemptShootEvent args)
    {
        // An empty shot is how an already anchored hook starts pulling.
        if (ent.Comp.Projectile != null)
            return;

        if (!_combatMode.IsInCombatMode(args.User) ||
            _hands.GetActiveItem(args.User) != ent.Owner ||
            !TryComp<WieldableComponent>(ent, out var wieldable) ||
            !wieldable.Wielded ||
            !_skills.HasSkill(args.User, SharedSkillsSystem.StrengthId) ||
            !TryGetGrid(args.User, out _))
        {
            args.Cancelled = true;
            args.Message = Loc.GetString("boarding-hook-cannot-throw");
            return;
        }

        // The ammunition entity is the visible hook itself and must use item throwing
        // so it receives a landing event at the cursor position.
        args.ThrowItems = true;
    }

    private void OnGunShot(Entity<BoardingHookComponent> ent, ref GunShotEvent args)
    {
        if (!TryGetGrid(args.User, out var originGrid))
            return;

        foreach (var (projectileUid, _) in args.Ammo)
        {
            if (projectileUid is not { } projectile ||
                !TryComp<BoardingHookProjectileComponent>(projectile, out var projectileComp))
            {
                continue;
            }

            ent.Comp.Projectile = projectile;
            ent.Comp.User = args.User;

            projectileComp.HookItem = ent.Owner;
            projectileComp.User = args.User;
            projectileComp.OriginGrid = originGrid;
            projectileComp.ThrowOrigin = _transform.GetMapCoordinates(args.User).Position;
            projectileComp.MaxThrowDistance = GetThrowDistance(ent.Comp, args.User);

            var visuals = EnsureComp<JointVisualsComponent>(projectile);
            visuals.Sprite = ent.Comp.RopeSprite;
            visuals.OffsetA = new Vector2(0f, 0.5f);
            visuals.Target = GetNetEntity(ent.Owner);
            Dirty(projectile, visuals);
            break;
        }
    }

    private void OnEmptyGunShot(Entity<BoardingHookComponent> ent, ref OnEmptyGunShotEvent args)
    {
        if (ent.Comp.Projectile is not { } projectile ||
            !TryComp<BoardingHookProjectileComponent>(projectile, out var projectileComp) ||
            !projectileComp.Anchored ||
            projectileComp.User != args.User ||
            !_combatMode.IsInCombatMode(args.User) ||
            _hands.GetActiveItem(args.User) != ent.Owner ||
            !TryComp<WieldableComponent>(ent, out var wieldable) ||
            !wieldable.Wielded ||
            !_skills.HasSkill(args.User, SharedSkillsSystem.StrengthId))
        {
            return;
        }

        var time = Math.Max(1f, 7f - _skills.GetSkillLevel(args.User, "Agility") * 0.3f);
        var doAfter = new DoAfterArgs(
            EntityManager,
            args.User,
            time,
            new BoardingHookPullDoAfterEvent(),
            eventTarget: ent.Owner,
            target: projectile,
            used: ent.Owner)
        {
            MovementThreshold = 0.1f,
            BreakOnMove = true,
            BlockDuplicate = true,
            DistanceThreshold = null,
            BreakOnDamage = true,
            RequireCanInteract = false,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnActivate(Entity<BoardingHookComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || ent.Comp.Projectile == null)
            return;

        DeleteProjectile(ent);
        args.Handled = true;
    }

    private void OnUnequipped(Entity<BoardingHookComponent> ent, ref GotUnequippedHandEvent args)
    {
        DeleteProjectile(ent);
    }

    private void OnUnwielded(Entity<BoardingHookComponent> ent, ref ItemUnwieldedEvent args)
    {
        DeleteProjectile(ent);
    }

    private void OnHookItemShutdown(Entity<BoardingHookComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Projectile is { } projectile)
            QueueDel(projectile);

        ent.Comp.Projectile = null;
        ent.Comp.User = null;
    }

    private void OnProjectileLand(Entity<BoardingHookProjectileComponent> ent, ref LandEvent args)
    {
        if (ent.Comp.Anchored || !TryGetGrid(ent.Owner, out var grid) || grid == ent.Comp.OriginGrid)
            return;

        if (!TryComp<MapGridComponent>(grid, out var mapGrid) ||
            !_map.TryGetTileRef(grid, mapGrid, Transform(ent).Coordinates, out var tile) ||
            tile.Tile.IsEmpty)
        {
            return;
        }

        _transform.AnchorEntity((ent.Owner, Transform(ent)), (grid, mapGrid), tile.GridIndices);
        ent.Comp.Anchored = true;
    }

    private void OnProjectileInteract(Entity<BoardingHookProjectileComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !ent.Comp.Anchored || ent.Comp.User == args.User ||
            !_skills.HasSkill(args.User, SharedSkillsSystem.StrengthId))
        {
            return;
        }

        var strength = Math.Clamp(_skills.GetSkillLevel(args.User, SharedSkillsSystem.StrengthId), 1, 20);
        var timeMultiplier = strength >= 10
            ? 1f - (strength - 10) * 0.05f
            : 1f + (10 - strength) * 0.05f;
        var doAfter = new DoAfterArgs(
            EntityManager,
            args.User,
            2f * timeMultiplier,
            new BoardingHookRemoveDoAfterEvent(),
            eventTarget: ent.Owner,
            target: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BreakOnHandChange = true,
            NeedHand = true,
            BlockDuplicate = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            args.Handled = true;
    }

    private void OnRemoveDoAfter(Entity<BoardingHookProjectileComponent> ent, ref BoardingHookRemoveDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !ent.Comp.Anchored)
            return;

        args.Handled = true;
        QueueDel(ent);
    }

    private void OnPullDoAfter(Entity<BoardingHookComponent> ent, ref BoardingHookPullDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled ||
            ent.Comp.Projectile is not { } projectile ||
            !TryComp<BoardingHookProjectileComponent>(projectile, out var projectileComp) ||
            !projectileComp.Anchored ||
            projectileComp.User != args.User ||
            _hands.GetActiveItem(args.User) != ent.Owner ||
            !TryComp<WieldableComponent>(ent, out var wieldable) ||
            !wieldable.Wielded ||
            !_skills.HasSkill(args.User, SharedSkillsSystem.StrengthId) ||
            !TryGetGrid(args.User, out var userGrid) ||
            !TryGetGrid(projectile, out var hookGrid) ||
            userGrid == hookGrid)
        {
            return;
        }

        var userGridIsIsland = HasComp<IslandComponent>(userGrid);
        var hookGridIsIsland = HasComp<IslandComponent>(hookGrid);
        if (userGridIsIsland && hookGridIsIsland)
        {
            args.Handled = true;
            QueueDel(projectile);
            return;
        }

        var userPosition = _transform.GetMapCoordinates(args.User);
        var hookPosition = _transform.GetMapCoordinates(projectile);
        if (userPosition.MapId != hookPosition.MapId)
            return;

        var strengthPower = ent.Comp.Power *
            (1f + (_skills.GetSkillLevel(args.User, SharedSkillsSystem.StrengthId) - 10) * 0.03f);
        bool success;

        if (hookGridIsIsland)
        {
            success = TryPushGrid(userGrid, hookPosition.Position - userPosition.Position,
                strengthPower, ent.Comp.OverloadCeilPerTile);
        }
        else if (userGridIsIsland)
        {
            success = TryPushGrid(hookGrid, userPosition.Position - hookPosition.Position,
                strengthPower, ent.Comp.OverloadCeilPerTile);
        }
        else
        {
            var power = strengthPower * 0.75f;
            if (TryGetGridImpulse(userGrid, hookPosition.Position - userPosition.Position,
                    power, ent.Comp.OverloadCeilPerTile, out var userBody, out var userImpulse) &&
                TryGetGridImpulse(hookGrid, userPosition.Position - hookPosition.Position,
                    power, ent.Comp.OverloadCeilPerTile, out var hookBody, out var hookImpulse))
            {
                ApplyGridImpulse(userGrid, userBody, userImpulse);
                ApplyGridImpulse(hookGrid, hookBody, hookImpulse);
                success = true;
            }
            else
                success = false;
        }

        if (!success)
            return;

        args.Handled = true;
        args.Repeat = true;
    }

    private void OnProjectileShutdown(Entity<BoardingHookProjectileComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent.Comp.HookItem) ||
            !TryComp<BoardingHookComponent>(ent.Comp.HookItem, out var hook) ||
            hook.Projectile != ent.Owner)
        {
            return;
        }

        hook.Projectile = null;
        hook.User = null;
        _gun.UpdateBasicEntityAmmoCount(ent.Comp.HookItem, 1);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BoardingHookProjectileComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var projectile, out var xform))
        {
            if (TerminatingOrDeleted(projectile.HookItem) ||
                TerminatingOrDeleted(projectile.User) ||
                !TryComp<BoardingHookComponent>(projectile.HookItem, out var hook) ||
                hook.Projectile != uid)
            {
                QueueDel(uid);
                continue;
            }

            var projectileMap = _transform.GetMapCoordinates(uid, xform);
            var userMap = _transform.GetMapCoordinates(projectile.User);
            if (projectileMap.MapId != userMap.MapId)
            {
                QueueDel(uid);
                continue;
            }

            var distanceFromUser = Vector2.Distance(projectileMap.Position, userMap.Position);
            if (projectile.Anchored)
            {
                if (distanceFromUser > hook.MaxTetherDistance)
                    QueueDel(uid);

                continue;
            }

            if (Vector2.Distance(projectileMap.Position, projectile.ThrowOrigin) < projectile.MaxThrowDistance)
                continue;

            if (!TryComp<ThrownItemComponent>(uid, out var thrown) ||
                !TryComp<PhysicsComponent>(uid, out var body))
            {
                continue;
            }

            var throwDirection = projectileMap.Position - projectile.ThrowOrigin;
            var landingPosition = projectile.ThrowOrigin +
                                  Vector2.Normalize(throwDirection) * projectile.MaxThrowDistance;
            var landingMap = new MapCoordinates(landingPosition, projectileMap.MapId);
            var landingCoordinates = _mapManager.TryFindGridAt(landingMap, out var landingGrid, out _)
                ? _transform.ToCoordinates(landingGrid, landingMap)
                : _transform.ToCoordinates(_map.GetMapOrInvalid(projectileMap.MapId), landingMap);
            _transform.SetCoordinates(uid, xform, landingCoordinates);

            _physics.SetLinearVelocity(uid, Vector2.Zero, body: body);
            _physics.SetAngularVelocity(uid, 0f, body: body);
            _thrown.LandComponent(uid, thrown, body, thrown.PlayLandSound);
            _thrown.StopThrow(uid, thrown);
        }
    }

    private bool TryPushGrid(EntityUid gridUid, Vector2 direction, float power, float overloadCeilPerTile)
    {
        if (!TryGetGridImpulse(gridUid, direction, power, overloadCeilPerTile, out var body, out var impulse))
            return false;

        ApplyGridImpulse(gridUid, body, impulse);
        return true;
    }

    private bool TryGetGridImpulse(
        EntityUid gridUid,
        Vector2 direction,
        float power,
        float overloadCeilPerTile,
        out PhysicsComponent body,
        out Vector2 impulse)
    {
        body = default!;
        impulse = Vector2.Zero;
        var lengthSquared = direction.LengthSquared();
        if (!float.IsFinite(lengthSquared) || lengthSquared <= 0.0001f ||
            !float.IsFinite(power) || power <= 0f ||
            TryComp<ShuttleComponent>(gridUid, out var shuttle) && !shuttle.Enabled ||
            !_shipGrid.TryGetGrid(gridUid, out var grid) ||
            grid.HasLoweredAnchor || grid.TileCount <= 0 ||
            !TryComp<PhysicsComponent>(gridUid, out var foundBody))
        {
            return false;
        }

        body = foundBody!;

        var overloadCeil = ShipGridSystem.GetMaxWeight(grid, overloadCeilPerTile);
        var impulsePower = grid.TotalWeight <= 0f || grid.TotalWeight <= overloadCeil
            ? power
            : power * overloadCeil / grid.TotalWeight;
        impulse = direction / MathF.Sqrt(lengthSquared) * impulsePower;
        return true;
    }

    private void ApplyGridImpulse(EntityUid gridUid, PhysicsComponent body, Vector2 impulse)
    {
        _physics.WakeBody(gridUid);
        _physics.ApplyLinearImpulse(gridUid, impulse, body: body);
    }

    private float GetThrowDistance(BoardingHookComponent component, EntityUid user)
    {
        var strength = _skills.GetSkillLevel(user, SharedSkillsSystem.StrengthId);
        return component.BaseThrowDistance * (1f + strength * component.ThrowDistancePerStrength);
    }

    private bool TryGetGrid(EntityUid uid, out EntityUid grid)
    {
        grid = _transform.GetMoverCoordinates(uid).EntityId;
        return HasComp<MapGridComponent>(grid);
    }

    private void DeleteProjectile(Entity<BoardingHookComponent> ent)
    {
        if (ent.Comp.Projectile is not { } projectile)
            return;

        ent.Comp.Projectile = null;
        ent.Comp.User = null;
        _gun.UpdateBasicEntityAmmoCount(ent.Owner, 1);
        QueueDel(projectile);
    }
}
