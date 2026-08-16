using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.CartographerTable;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared.Imperial.Medieval.Ships.Flagpole;

public sealed class MedievalShipFlagpoleSystemShared : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MedievalShipFlagpoleComponent, MedievalShipFlagpoleSelectedMessage>(OnMenuAction);

        SubscribeLocalEvent<MedievalShipFlagpoleComponent, MedievalShipFlagpoleDoAfterEvent>(OnDoAfter);
    }

    private void OnMenuAction(Entity<MedievalShipFlagpoleComponent> ent, ref MedievalShipFlagpoleSelectedMessage args)
    {
        if (ent.Comp.User is not null && ent.Comp.User != args.Actor)
            return;

        if (TryComp<MedievalFactionMemberComponent>(args.Actor, out var factionComponent))
        {
            if (args.FlagColor == MedievalShipFlagpoleMenuAction.Legion && factionComponent.Faction != "Legion")
                return;

            if (args.FlagColor == MedievalShipFlagpoleMenuAction.Insurgency && factionComponent.Faction != "Insurgency")
                return;

            if (args.FlagColor == MedievalShipFlagpoleMenuAction.Collegium && factionComponent.Faction != "Collegium")
                return;

            if (args.FlagColor == MedievalShipFlagpoleMenuAction.Mercenary && factionComponent.Faction != "Merc")
                return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, args.Actor, TimeSpan.FromSeconds(ent.Comp.DoAfterTime), new MedievalShipFlagpoleDoAfterEvent(args.FlagColor), ent, ent)
        {
            MovementThreshold = 0.5f,
            BreakOnMove = true,
            CancelDuplicate = true,
            DistanceThreshold = 1,
            BreakOnDamage = true,
            RequireCanInteract = false,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfterArgs))
            ent.Comp.User = args.Actor;
    }

    private void OnDoAfter(Entity<MedievalShipFlagpoleComponent> ent, ref MedievalShipFlagpoleDoAfterEvent args)
    {
        ent.Comp.User = null;

        if (args.Cancelled || args.Handled)
            return;

        _appearance.SetData(ent, MedievalShipFlagpoleVisuals.State, args.Action);

        if (args.Action == MedievalShipFlagpoleMenuAction.None)
        {
            RemComp<CartographerRadarMarkerComponent>(ent);
            if (_net.IsServer)
                _audio.PlayPvs(MedievalShipSounds.SailClose, ent);
            return;
        }

        EnsureComp<CartographerRadarMarkerComponent>(ent, out var marker);

        marker.RsiPath = ent.Comp.RsiPath;
        marker.State = GetFlagState(args.Action);
        marker.ZoomScaling = ent.Comp.IsZoomingScale;
        marker.Size = ent.Comp.Scale;

        if (_net.IsServer)
            _audio.PlayPvs(MedievalShipSounds.SailOpen, ent);
    }

    public static string GetFlagState(MedievalShipFlagpoleMenuAction action)
    {
        return action switch
        {
            MedievalShipFlagpoleMenuAction.None => "transparent",
            MedievalShipFlagpoleMenuAction.Black => "blackflag",
            MedievalShipFlagpoleMenuAction.Red => "redflag",
            MedievalShipFlagpoleMenuAction.White => "whiteflag",
            MedievalShipFlagpoleMenuAction.Brown => "brownflag",
            MedievalShipFlagpoleMenuAction.Cyan => "cyanflag",
            MedievalShipFlagpoleMenuAction.DarkRed => "darkredflag",
            MedievalShipFlagpoleMenuAction.Gray => "grayflag",
            MedievalShipFlagpoleMenuAction.Green => "greenflag",
            MedievalShipFlagpoleMenuAction.Orange => "orangeflag",
            MedievalShipFlagpoleMenuAction.Pink => "pinkflag",
            MedievalShipFlagpoleMenuAction.Purple => "purpleflag",
            MedievalShipFlagpoleMenuAction.Yellow => "yellowflag",
            MedievalShipFlagpoleMenuAction.Blue => "blueflag",
            MedievalShipFlagpoleMenuAction.Pirate => "pirateflag",
            MedievalShipFlagpoleMenuAction.Legion => "legionflag",
            MedievalShipFlagpoleMenuAction.Insurgency => "foxflag",
            MedievalShipFlagpoleMenuAction.Collegium => "wizflag",
            MedievalShipFlagpoleMenuAction.Mercenary => "mercflag",
            _ => "transparent"
        };
    }
}
