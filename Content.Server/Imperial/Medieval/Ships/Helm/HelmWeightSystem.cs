using Content.Shared._RD.Weight.Components;
using Content.Shared._RD.Weight.Events;
using Content.Shared._RD.Weight.Systems;
using Content.Shared.Stacks;
using Robust.Shared.Map.Components;

namespace Content.Server.Imperial.Medieval.Ships.Helm;

public sealed class HelmWeightSystem : EntitySystem
{
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly RDWeightSystem _weight = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RDWeightComponent, MapInitEvent>(OnWeightMapInit);
        SubscribeLocalEvent<RDWeightComponent, ComponentShutdown>(OnWeightShutdown);
        SubscribeLocalEvent<HelmWeightTrackerComponent, ComponentShutdown>(OnTrackerShutdown);
        SubscribeLocalEvent<HelmWeightTrackerComponent, GridUidChangedEvent>(OnGridChanged);
        SubscribeLocalEvent<HelmWeightTrackerComponent, StackCountChangedEvent>(OnStackChanged);
        SubscribeLocalEvent<HelmWeightTrackerComponent, RDWeightRefreshEvent>(OnWeightRefresh);
    }

    public float GetTotalOnGrid(EntityUid gridUid)
    {
        return CompOrNull<HelmGridComponent>(gridUid)?.TotalWeight ?? 0f;
    }

    private void OnWeightMapInit(EntityUid uid, RDWeightComponent component, MapInitEvent args)
    {
        _metaData.AddFlag(uid, MetaDataFlags.ExtraTransformEvents);
        var tracker = EnsureComp<HelmWeightTrackerComponent>(uid);
        UpdateWeight(uid, tracker, GetGridUid(uid, Transform(uid)), GetDirectWeight(uid));
    }

    private void OnWeightShutdown(Entity<RDWeightComponent> entity, ref ComponentShutdown args)
    {
        if (TryComp<HelmWeightTrackerComponent>(entity, out var tracker))
            RemoveContribution(tracker);
    }

    private void OnTrackerShutdown(Entity<HelmWeightTrackerComponent> entity, ref ComponentShutdown args)
    {
        RemoveContribution(entity.Comp);
    }

    private void OnGridChanged(Entity<HelmWeightTrackerComponent> entity, ref GridUidChangedEvent args)
    {
        if (!HasComp<RDWeightComponent>(entity))
            return;

        UpdateWeight(entity, entity.Comp, args.NewGrid, GetDirectWeight(entity));
    }

    private void OnStackChanged(Entity<HelmWeightTrackerComponent> entity, ref StackCountChangedEvent args)
    {
        if (!HasComp<RDWeightComponent>(entity))
            return;

        UpdateWeight(entity, entity.Comp, entity.Comp.GridUid, GetDirectWeight(entity));
    }

    private void OnWeightRefresh(Entity<HelmWeightTrackerComponent> entity, ref RDWeightRefreshEvent args)
    {
        var directWeight = args.Total;
        var children = Transform(entity).ChildEnumerator;
        while (children.MoveNext(out var childUid))
        {
            directWeight -= _weight.GetTotal(childUid);
        }

        UpdateWeight(entity, entity.Comp, entity.Comp.GridUid, directWeight);
    }

    private float GetDirectWeight(EntityUid uid)
    {
        var directWeight = _weight.GetTotal(uid);
        var children = Transform(uid).ChildEnumerator;
        while (children.MoveNext(out var childUid))
        {
            directWeight -= _weight.GetTotal(childUid);
        }

        return directWeight;
    }

    private void UpdateWeight(
        EntityUid uid,
        HelmWeightTrackerComponent tracker,
        EntityUid? gridUid,
        float contribution)
    {
        if (gridUid == uid || gridUid != null && !HasComp<MapGridComponent>(gridUid.Value))
            gridUid = null;

        if (tracker.GridUid == gridUid)
        {
            if (gridUid != null)
                AdjustGridWeight(gridUid.Value, contribution - tracker.Contribution);
        }
        else
        {
            RemoveContribution(tracker);

            if (gridUid != null)
                AdjustGridWeight(gridUid.Value, contribution);
        }

        tracker.GridUid = gridUid;
        tracker.Contribution = contribution;
    }

    private void RemoveContribution(HelmWeightTrackerComponent tracker)
    {
        if (tracker.GridUid != null)
            AdjustGridWeight(tracker.GridUid.Value, -tracker.Contribution);

        tracker.GridUid = null;
        tracker.Contribution = 0f;
    }

    private void AdjustGridWeight(EntityUid gridUid, float delta)
    {
        if (MathF.Abs(delta) < float.Epsilon || TerminatingOrDeleted(gridUid))
            return;

        var grid = EnsureComp<HelmGridComponent>(gridUid);
        grid.TotalWeight += delta;
        if (MathF.Abs(grid.TotalWeight) < 0.001f)
            grid.TotalWeight = 0f;
    }

    private EntityUid? GetGridUid(EntityUid uid, TransformComponent xform)
    {
        var gridUid = _transform.GetMoverCoordinates(uid, xform).EntityId;
        return HasComp<MapGridComponent>(gridUid) ? gridUid : null;
    }
}
