using System;
using Content.Server.Imperial.Medieval.Ships.PlayerDrowning;
using Content.Server.Shuttles.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval.Administration.Ships;
using Content.Shared.Imperial.Medieval.Ships.Helm;
using Content.Shared.Imperial.Medieval.Ships.Sail;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.UserInterface;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Ships.Helm;

public sealed class HelmSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly HelmWeightSystem _weight = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly AudioSystem _audio = default!;

    private TimeSpan _nextCheckTime;

    public override void Initialize()
    {
        SubscribeLocalEvent<HelmComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<HelmComponent, ComponentShutdown>(OnHelmShutdown);
        SubscribeLocalEvent<HelmComponent, GridUidChangedEvent>(OnHelmGridChanged);
        SubscribeLocalEvent<HelmComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<HelmComponent, HelmActionDoAfterEvent>(OnHelmActionDoAfter);
        SubscribeLocalEvent<HelmComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);
        SubscribeLocalEvent<HelmComponent, BoundUIClosedEvent>(OnAfterUiClosed);
        SubscribeLocalEvent<HelmComponent, HelmMenuActionMessage>(OnMenuActionMessage);
        SubscribeLocalEvent<HelmComponent, HelmRotationChangeMessage>(OnRotationChangeMessage);

        SubscribeLocalEvent<MedievalPilotComponent, UpdateCanMoveEvent>(OnUpdateCanMove);

        SubscribeLocalEvent<SailComponent, MapInitEvent>(OnSailMapInit);
        SubscribeLocalEvent<SailComponent, ComponentShutdown>(OnSailShutdown);
        SubscribeLocalEvent<SailComponent, GridUidChangedEvent>(OnSailGridChanged);
        SubscribeLocalEvent<SailComponent, SailEfficiencyChangedEvent>(OnSailEfficiencyChanged);

        SubscribeLocalEvent<SteeringOarComponent, ComponentStartup>(OnSteeringOarStartup);
        SubscribeLocalEvent<SteeringOarComponent, ComponentShutdown>(OnSteeringOarShutdown);
        SubscribeLocalEvent<SteeringOarComponent, GridUidChangedEvent>(OnSteeringOarGridChanged);

        SubscribeLocalEvent<HelmGridComponent, ComponentStartup>(OnGridCacheStartup);
        SubscribeLocalEvent<HelmGridComponent, TileChangedEvent>(OnGridTileChanged);
    }

    private void OnUpdateCanMove(EntityUid uid, MedievalPilotComponent component, ref UpdateCanMoveEvent args)
    {
        args.Cancel();
    }

    private void OnStartup(EntityUid uid, HelmComponent component, ComponentStartup args)
    {
        _metaData.AddFlag(uid, MetaDataFlags.ExtraTransformEvents);
        component.HelmRotation = NormalizeHelmRotation(component.HelmRotation);
        RegisterHelm(uid, component, GetGridUid(uid, Transform(uid)));
    }

    private void OnHelmShutdown(EntityUid uid, HelmComponent component, ComponentShutdown args)
    {
        UnregisterHelm(uid, component);
    }

    private void OnHelmGridChanged(EntityUid uid, HelmComponent component, ref GridUidChangedEvent args)
    {
        RegisterHelm(uid, component, args.NewGrid);
    }

    private void OnSailMapInit(EntityUid uid, SailComponent component, MapInitEvent args)
    {
        _metaData.AddFlag(uid, MetaDataFlags.ExtraTransformEvents);
        RegisterSail(uid, component, GetGridUid(uid, Transform(uid)));
    }

    private void OnSailShutdown(EntityUid uid, SailComponent component, ComponentShutdown args)
    {
        UnregisterSail(uid, component);
    }

    private void OnSailGridChanged(EntityUid uid, SailComponent component, ref GridUidChangedEvent args)
    {
        RegisterSail(uid, component, args.NewGrid);
    }

    private void OnSailEfficiencyChanged(EntityUid uid, SailComponent component, ref SailEfficiencyChangedEvent args)
    {
        if (component.HelmGridUid == null ||
            !TryComp<HelmGridComponent>(component.HelmGridUid.Value, out var gridCache))
        {
            return;
        }

        var delta = args.NewValue - args.OldValue;
        gridCache.SailsEfficiency += delta;
        foreach (var helmUid in gridCache.Helms)
        {
            if (TryComp<HelmComponent>(helmUid, out var helm) && helm.Sails.Contains(uid))
                helm.CachedSailsEfficiency += delta;
        }
    }

    private void OnSteeringOarStartup(EntityUid uid, SteeringOarComponent component, ComponentStartup args)
    {
        _metaData.AddFlag(uid, MetaDataFlags.ExtraTransformEvents);
        RegisterSteeringOar(uid, component, GetGridUid(uid, Transform(uid)));
    }

    private void OnSteeringOarShutdown(EntityUid uid, SteeringOarComponent component, ComponentShutdown args)
    {
        UnregisterSteeringOar(uid, component);
    }

    private void OnSteeringOarGridChanged(EntityUid uid, SteeringOarComponent component, ref GridUidChangedEvent args)
    {
        RegisterSteeringOar(uid, component, args.NewGrid);
    }

    private void OnGridTileChanged(EntityUid uid, HelmGridComponent component, ref TileChangedEvent args)
    {
        if (!component.TileCountInitialized)
            return;

        foreach (var change in args.Changes)
        {
            if (!change.EmptyChanged)
                continue;

            component.TileCount += change.NewTile.IsEmpty ? -1 : 1;
        }

        component.TileCount = Math.Max(0, component.TileCount);
    }

    private void OnGridCacheStartup(EntityUid uid, HelmGridComponent component, ComponentStartup args)
    {
        InitializeGridCache(uid, component);
    }

    private void InitializeGridCache(EntityUid uid, HelmGridComponent component)
    {
        if (component.TileCountInitialized || !TryComp<MapGridComponent>(uid, out var mapGrid))
            return;

        var tiles = _map.GetAllTilesEnumerator(uid, mapGrid);
        while (tiles.MoveNext(out _))
        {
            component.TileCount++;
        }

        component.TileCountInitialized = true;
    }

    private void OnBeforeUiOpen(EntityUid uid, HelmComponent component, BeforeActivatableUIOpenEvent args)
    {
        var pilotComp = EnsureComp<MedievalPilotComponent>(args.User);
        if (pilotComp.UsingSound != null)
            StopUsingSound(pilotComp);

        pilotComp.HelmEntity = uid;
        pilotComp.LastRotationUpdate = _timing.CurTime;
        pilotComp.RotationBudget = 0f;
        _actionBlocker.UpdateCanMove(args.User);

        UpdateUi(uid, component);
    }

    private void OnAfterUiClosed(EntityUid uid, HelmComponent component, BoundUIClosedEvent args)
    {
        if (TryComp<MedievalPilotComponent>(args.Actor, out var pilot))
            StopUsingSound(pilot);

        RemComp<MedievalPilotComponent>(args.Actor);
        _actionBlocker.UpdateCanMove(args.Actor);
    }

    private void OnMenuActionMessage(EntityUid uid, HelmComponent component, HelmMenuActionMessage msg)
    {
        var player = msg.Actor;
        if (!_actionBlocker.CanInteract(player, uid) ||
            !_actionBlocker.CanComplexInteract(player) ||
            !_interaction.InRangeAndAccessible(player, uid))
            return;

        TryStartHelmActionDoAfter(player, uid, msg.Action);
    }

    private void OnRotationChangeMessage(EntityUid uid, HelmComponent component, HelmRotationChangeMessage msg)
    {
        var player = msg.Actor;
        if (!TryComp<MedievalPilotComponent>(player, out var pilot) ||
            pilot.HelmEntity != uid ||
            !_actionBlocker.CanInteract(player, uid) ||
            !_actionBlocker.CanComplexInteract(player) ||
            !_interaction.InRangeAndAccessible(player, uid) ||
            !float.IsFinite(msg.HelmRotation))
        {
            return;
        }

        var curTime = _timing.CurTime;
        var elapsed = Math.Max(0f, (float) (curTime - pilot.LastRotationUpdate).TotalSeconds);
        var rotationStep = MathF.Abs(component.RotationStep);
        var maxBudget = rotationStep * MathF.Max(0f, component.RotationSyncMaxBudgetSeconds);
        pilot.RotationBudget = MathF.Min(maxBudget, pilot.RotationBudget + rotationStep * elapsed);
        pilot.LastRotationUpdate = curTime;

        var requestedRotation = Math.Clamp(msg.HelmRotation, -180f, 180f);
        var requestedDelta = requestedRotation - component.HelmRotation;
        var appliedDelta = Math.Clamp(requestedDelta, -pilot.RotationBudget, pilot.RotationBudget);
        component.HelmRotation = Math.Clamp(component.HelmRotation + appliedDelta, -180f, 180f);
        pilot.RotationBudget = MathF.Max(0f, pilot.RotationBudget - MathF.Abs(appliedDelta));

        if (msg.Turning)
            StartUsingSound(uid, pilot);
        else
            StopUsingSound(pilot);
    }

    private void StartUsingSound(EntityUid helm, MedievalPilotComponent pilot)
    {
        if (pilot.UsingSound != null)
            return;

        var audioParams = AudioParams.Default.WithLoop(true);
        pilot.UsingSound = _audio.PlayPvs(
            new SoundPathSpecifier("/Audio/Imperial/Medieval/hitting_wood_4times.ogg"),
            helm,
            audioParams)?.Entity;
    }

    private void StopUsingSound(MedievalPilotComponent pilot)
    {
        pilot.UsingSound = _audio.Stop(pilot.UsingSound);
    }

    private void OnExamine(EntityUid uid, HelmComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (component.HelmRotation == 0f)
        {
            args.PushMarkup(Loc.GetString("helm-examine-center"));
        }
        else
        {
            var degrees = MathF.Abs(component.HelmRotation).ToString("0.##");
            if (component.HelmRotation > 0f)
                args.PushMarkup(Loc.GetString("helm-examine-right", ("degrees", degrees)));
            else
                args.PushMarkup(Loc.GetString("helm-examine-left", ("degrees", degrees)));
        }

        args.PushMarkup(Loc.GetString(
            "helm-examine-sails-efficiency",
            ("efficiency", FormatEfficiency(component.CachedSailsEfficiency))));

        if (TryGetShipLoad(component, out var weight, out var overloadCeil))
        {
            args.PushMarkup(Loc.GetString(
                "helm-examine-ship-load",
                ("weight", FormatWeight(weight)),
                ("overloadCeil", FormatWeight(overloadCeil))));
        }
    }

    private void TryStartHelmActionDoAfter(EntityUid player, EntityUid helm, HelmMenuAction action)
    {
        var doAfterArgs = new DoAfterArgs(EntityManager, player, 0.5f, new HelmActionDoAfterEvent(action), helm, helm)
        {
            MovementThreshold = 0.5f,
            BreakOnMove = true,
            CancelDuplicate = true,
            DistanceThreshold = 2,
            BreakOnDamage = true,
            RequireCanInteract = false,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnHelmActionDoAfter(EntityUid uid, HelmComponent component, HelmActionDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        ApplyHelmAction(uid, component, args.Action);
        args.Handled = true;
    }

    private void ApplyHelmAction(EntityUid helm, HelmComponent helmComponent, HelmMenuAction action)
    {
        switch (action)
        {
            case HelmMenuAction.RotateLeft:
                helmComponent.HelmRotation -= helmComponent.RotationStep;
                break;
            case HelmMenuAction.RotateRight:
                helmComponent.HelmRotation += helmComponent.RotationStep;
                break;
            case HelmMenuAction.Center:
                helmComponent.HelmRotation = 0f;
                break;
        }

        helmComponent.HelmRotation = NormalizeHelmRotation(helmComponent.HelmRotation);
        UpdateUi(helm, helmComponent);
    }

    private void UpdateUi(EntityUid uid, HelmComponent component)
    {
        _ui.SetUiState(
            uid,
            HelmUiKey.Key,
            new HelmBoundUserInterfaceState(component.HelmRotation, component.RotationStep));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        if (curTime <= _nextCheckTime)
            return;

        _nextCheckTime = curTime + TimeSpan.FromSeconds(_cfg.GetCVar(ShipsCCVars.WindDelay));
        var windEnabled = _cfg.GetCVar(ShipsCCVars.WindEnabled);

        var query = EntityQueryEnumerator<HelmComponent>();
        while (query.MoveNext(out _, out var helmComponent))
        {
            if (helmComponent.GridUid is not { } boat)
                continue;

            if (curTime >= helmComponent.NextCacheUpdate)
            {
                RefreshCache(helmComponent, boat);
                helmComponent.NextCacheUpdate = curTime + GetCacheRefreshInterval(helmComponent);
            }

            if (windEnabled)
                RotateShip(boat, helmComponent);
        }
    }

    private void RotateShip(EntityUid boat, HelmComponent helmComponent)
    {
        if (TryComp<ShuttleComponent>(boat, out var shuttle) && !shuttle.Enabled)
            return;

        var steeringPower = helmComponent.CachedSteeringPower;
        if (steeringPower <= 0f)
            return;

        if (!TryComp<PhysicsComponent>(boat, out var body))
            return;

        var steeringInput = GetSteeringInput(helmComponent);
        if (MathF.Abs(steeringInput) < 0.001f)
        {
            if (MathF.Abs(body.AngularVelocity) < 0.001f)
                return;

            var weight = MathF.Max(helmComponent.MinShipWeight, helmComponent.CachedShipWeight);
            var weightDivider = 1f + weight * 0.01f;
            StabilizeShipRotation(boat, helmComponent, steeringPower, weightDivider, body);
            return;
        }

        var shipWeight = MathF.Max(helmComponent.MinShipWeight, helmComponent.CachedShipWeight);
        var shipWeightDivider = 1f + shipWeight * 0.01f;
        var angularImpulse = steeringInput * helmComponent.MinMotionFactor * steeringPower * helmComponent.TurnImpulseScalar / shipWeightDivider;

        _physics.ApplyAngularImpulse(boat, angularImpulse, body: body);
    }

    private void StabilizeShipRotation(
        EntityUid boat,
        HelmComponent helmComponent,
        float steeringPower,
        float weightDivider,
        PhysicsComponent body)
    {
        var angularVelocity = body.AngularVelocity;
        if (body.InvI <= 0f)
            return;

        var stabilizingImpulseMagnitude = helmComponent.MinMotionFactor * steeringPower * helmComponent.StabilizingImpulseScalar / weightDivider;
        if (stabilizingImpulseMagnitude <= 0f)
            return;

        var desiredImpulse = -MathF.Sign(angularVelocity) * stabilizingImpulseMagnitude;
        var stopImpulse = -angularVelocity / body.InvI;
        var stopNow = MathF.Abs(desiredImpulse) >= MathF.Abs(stopImpulse);
        var angularImpulse = stopNow ? stopImpulse : desiredImpulse;

        _physics.ApplyAngularImpulse(boat, angularImpulse, body: body);

        if (stopNow)
            _physics.SetAngularVelocity(boat, 0f, body: body);
    }

    private void RefreshCache(HelmComponent helmComponent, EntityUid boat)
    {
        helmComponent.CachedShipWeight = _weight.GetTotalOnGrid(boat);

        if (!TryComp<HelmGridComponent>(boat, out var gridCache))
        {
            helmComponent.CachedOverloadCeil = 0f;
            return;
        }

        var overloadCeilPerTile = _cfg.GetCVar(ShipsCCVars.OverloadCeilPerTile);
        if (TryComp<ShipWeightComponent>(boat, out var shipWeight))
            overloadCeilPerTile = shipWeight.OverloadCeilPerTile;

        helmComponent.CachedOverloadCeil = gridCache.TileCount * overloadCeilPerTile;
    }

    private bool TryGetShipLoad(HelmComponent helmComponent, out float weight, out float overloadCeil)
    {
        if (helmComponent.GridUid == null)
        {
            weight = 0f;
            overloadCeil = 0f;
            return false;
        }

        weight = helmComponent.CachedShipWeight;
        overloadCeil = helmComponent.CachedOverloadCeil;
        return true;
    }

    private void RegisterHelm(EntityUid uid, HelmComponent component, EntityUid? gridUid)
    {
        if (gridUid != null && !HasComp<MapGridComponent>(gridUid.Value))
            gridUid = null;

        if (component.GridUid == gridUid)
            return;

        UnregisterHelm(uid, component);
        if (gridUid == null)
            return;

        var gridCache = EnsureGridCache(gridUid.Value);
        gridCache.Helms.Add(uid);
        component.GridUid = gridUid;
        component.Sails.UnionWith(gridCache.Sails);
        component.SteeringOars.UnionWith(gridCache.SteeringOars);
        component.CachedSailsEfficiency = gridCache.SailsEfficiency;
        component.CachedSteeringPower = gridCache.SteeringPower;
        RefreshCache(component, gridUid.Value);
        component.NextCacheUpdate = _timing.CurTime + GetCacheRefreshInterval(component);
    }

    private void UnregisterHelm(EntityUid uid, HelmComponent component)
    {
        if (component.GridUid != null && TryComp<HelmGridComponent>(component.GridUid.Value, out var gridCache))
            gridCache.Helms.Remove(uid);

        component.GridUid = null;
        component.NextCacheUpdate = TimeSpan.Zero;
        component.Sails.Clear();
        component.SteeringOars.Clear();
        component.CachedShipWeight = 0f;
        component.CachedOverloadCeil = 0f;
        component.CachedSteeringPower = 0f;
        component.CachedSailsEfficiency = 0f;
    }

    private void RegisterSail(EntityUid uid, SailComponent component, EntityUid? gridUid)
    {
        if (gridUid != null && !HasComp<MapGridComponent>(gridUid.Value))
            gridUid = null;

        if (component.HelmGridUid == gridUid)
            return;

        UnregisterSail(uid, component);
        if (gridUid == null)
            return;

        var gridCache = EnsureGridCache(gridUid.Value);
        if (gridCache.Sails.Add(uid))
            gridCache.SailsEfficiency += component.LastSailEfficencyMod;

        component.HelmGridUid = gridUid;

        foreach (var helmUid in gridCache.Helms)
        {
            if (!TryComp<HelmComponent>(helmUid, out var helm))
                continue;

            if (helm.Sails.Add(uid))
                helm.CachedSailsEfficiency += component.LastSailEfficencyMod;
        }
    }

    private void UnregisterSail(EntityUid uid, SailComponent component)
    {
        if (component.HelmGridUid == null ||
            !TryComp<HelmGridComponent>(component.HelmGridUid.Value, out var gridCache))
        {
            component.HelmGridUid = null;
            return;
        }

        if (!gridCache.Sails.Remove(uid))
        {
            component.HelmGridUid = null;
            return;
        }

        gridCache.SailsEfficiency -= component.LastSailEfficencyMod;
        foreach (var helmUid in gridCache.Helms)
        {
            if (!TryComp<HelmComponent>(helmUid, out var helm) || !helm.Sails.Remove(uid))
                continue;

            helm.CachedSailsEfficiency -= component.LastSailEfficencyMod;
        }

        component.HelmGridUid = null;
    }

    private void RegisterSteeringOar(EntityUid uid, SteeringOarComponent component, EntityUid? gridUid)
    {
        if (gridUid != null && !HasComp<MapGridComponent>(gridUid.Value))
            gridUid = null;

        if (component.HelmGridUid == gridUid)
            return;

        UnregisterSteeringOar(uid, component);
        if (gridUid == null)
            return;

        var gridCache = EnsureGridCache(gridUid.Value);
        if (gridCache.SteeringOars.Add(uid))
            gridCache.SteeringPower += component.Power;

        component.HelmGridUid = gridUid;

        foreach (var helmUid in gridCache.Helms)
        {
            if (!TryComp<HelmComponent>(helmUid, out var helm))
                continue;

            if (helm.SteeringOars.Add(uid))
                helm.CachedSteeringPower += component.Power;
        }
    }

    private void UnregisterSteeringOar(EntityUid uid, SteeringOarComponent component)
    {
        if (component.HelmGridUid == null ||
            !TryComp<HelmGridComponent>(component.HelmGridUid.Value, out var gridCache))
        {
            component.HelmGridUid = null;
            return;
        }

        if (!gridCache.SteeringOars.Remove(uid))
        {
            component.HelmGridUid = null;
            return;
        }

        gridCache.SteeringPower -= component.Power;
        foreach (var helmUid in gridCache.Helms)
        {
            if (!TryComp<HelmComponent>(helmUid, out var helm) || !helm.SteeringOars.Remove(uid))
                continue;

            helm.CachedSteeringPower -= component.Power;
        }

        component.HelmGridUid = null;
    }

    private HelmGridComponent EnsureGridCache(EntityUid gridUid)
    {
        var gridCache = EnsureComp<HelmGridComponent>(gridUid);
        InitializeGridCache(gridUid, gridCache);
        return gridCache;
    }

    private EntityUid? GetGridUid(EntityUid uid, TransformComponent xform)
    {
        var gridUid = _transform.GetMoverCoordinates(uid, xform).EntityId;
        return HasComp<MapGridComponent>(gridUid) ? gridUid : null;
    }

    private static TimeSpan GetCacheRefreshInterval(HelmComponent component)
    {
        var seconds = float.IsFinite(component.CacheRefreshInterval)
            ? MathF.Max(1f, component.CacheRefreshInterval)
            : 1f;
        return TimeSpan.FromSeconds(seconds);
    }

    private static float GetSteeringInput(HelmComponent helmComponent)
    {
        var diffDegrees = helmComponent.HelmRotation;
        var maxTurnAngle = MathF.Max(1f, MathF.Abs(helmComponent.SteeringAngleForMaxTurn));
        return Math.Clamp(-diffDegrees / maxTurnAngle, -1f, 1f);
    }

    private static string FormatEfficiency(float value)
    {
        return value.ToString("0.##");
    }

    private static string FormatWeight(float value)
    {
        return value.ToString("0.##");
    }

    private static float NormalizeHelmRotation(float helmRotation)
    {
        if (helmRotation > 180f)
            helmRotation -= 360f;

        if (helmRotation < -180f)
            helmRotation += 360f;

        return helmRotation;
    }
}
