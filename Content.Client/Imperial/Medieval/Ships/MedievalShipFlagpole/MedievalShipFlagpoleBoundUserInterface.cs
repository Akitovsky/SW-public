using Content.Client.UserInterface.Controls;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Shared.Imperial.Medieval.Ships;
using Content.Shared.Imperial.Medieval.Ships.Flagpole;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Сlient.Imperial.Medieval.Ships.Flagpole;

public sealed class MedievalShipFlagpoleBoundUserInterface : BoundUserInterface
{
    private SimpleRadialMenu? _menu;
    [Dependency] private readonly IPlayerManager _player = default!;

    public MedievalShipFlagpoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<SimpleRadialMenu>();

        var entMan = IoCManager.Resolve<IEntityManager>();

        // Инициализируем список с заранее заданным размером (около 18 элементов),
        // чтобы избежать реаллокаций памяти.
        var buttons = new List<RadialMenuActionOption<MedievalShipFlagpoleMenuAction>>(18)
        {
            new(SendAction, MedievalShipFlagpoleMenuAction.Black)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "blackflag")),
                ToolTip = Loc.GetString("ship-flagpole-color-black"),
            },
            new(SendAction, MedievalShipFlagpoleMenuAction.Red)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "redflag")),
                ToolTip = Loc.GetString("ship-flagpole-color-red"),
            },
            new(SendAction, MedievalShipFlagpoleMenuAction.White)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "whiteflag")),
                ToolTip = Loc.GetString("ship-flagpole-color-white"),
            },
            new(SendAction, MedievalShipFlagpoleMenuAction.Brown)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "brownflag")),
                ToolTip = Loc.GetString("ship-flagpole-color-brown"),
            },
            new(SendAction, MedievalShipFlagpoleMenuAction.Cyan)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "cyanflag")),
                ToolTip = Loc.GetString("ship-flagpole-color-cyan"),
            },
            new(SendAction, MedievalShipFlagpoleMenuAction.DarkRed)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "darkredflag")),
                ToolTip = Loc.GetString("ship-flagpole-color-darkred"),
            },
            new(SendAction, MedievalShipFlagpoleMenuAction.Gray)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "grayflag")),
                ToolTip = Loc.GetString("ship-flagpole-color-gray"),
            },
            new(SendAction, MedievalShipFlagpoleMenuAction.Green)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "greenflag")),
                ToolTip = Loc.GetString("ship-flagpole-color-green"),
            },
            new(SendAction, MedievalShipFlagpoleMenuAction.Orange)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "orangeflag")),
                ToolTip = Loc.GetString("ship-flagpole-color-orange"),
            },
            new(SendAction, MedievalShipFlagpoleMenuAction.Pink)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "pinkflag")),
                ToolTip = Loc.GetString("ship-flagpole-color-pink"),
            },
            new(SendAction, MedievalShipFlagpoleMenuAction.Purple)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "purpleflag")),
                ToolTip = Loc.GetString("ship-flagpole-color-purple"),
            },
            new(SendAction, MedievalShipFlagpoleMenuAction.Yellow)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "yellowflag")),
                ToolTip = Loc.GetString("ship-flagpole-color-yellow"),
            },
            new(SendAction, MedievalShipFlagpoleMenuAction.Blue)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "blueflag")),
                ToolTip = Loc.GetString("ship-flagpole-color-blue"),
            },
            new(SendAction, MedievalShipFlagpoleMenuAction.None)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "transparent")),
                ToolTip = Loc.GetString("ship-flagpole-color-none"),
            }
        };

        if (true)
        {
            buttons.Add(new(SendAction, MedievalShipFlagpoleMenuAction.Pirate)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                    new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "pirateflag")),
                ToolTip = Loc.GetString("ship-flagpole-color-pirate"),
            });
        }

        if (_player.LocalSession is not null && entMan.TryGetComponent<MedievalFactionMemberComponent>(_player.LocalSession.AttachedEntity, out var factionComponent))
        {
            if (factionComponent.Faction == "Legion")
            {
                buttons.Add(new(SendAction, MedievalShipFlagpoleMenuAction.Legion)
                {
                    IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                        new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "legionflag")),
                    ToolTip = Loc.GetString("ship-flagpole-color-legion"),
                });
            }

            if (factionComponent.Faction == "Insurgency")
            {
                buttons.Add(new(SendAction, MedievalShipFlagpoleMenuAction.Insurgency)
                {
                    IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                        new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "foxflag")),
                    ToolTip = Loc.GetString("ship-flagpole-color-insurgency"),
                });
            }

            if (factionComponent.Faction == "Collegium")
            {
                buttons.Add(new(SendAction, MedievalShipFlagpoleMenuAction.Collegium)
                {
                    IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                        new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "wizflag")),
                    ToolTip = Loc.GetString("ship-flagpole-color-collegium"),
                });
            }

            if (factionComponent.Faction == "Merc")
            {
                buttons.Add(new(SendAction, MedievalShipFlagpoleMenuAction.Mercenary)
                {
                    IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(
                        new ResPath("/Textures/Imperial/Medieval/Decor/flagpole.rsi"), "mercflag")),
                    ToolTip = Loc.GetString("ship-flagpole-color-mercenary"),
                });
            }
        }

        _menu.SetButtons(buttons);
        _menu.OpenOverMouseScreenPosition();
    }

    private void SendAction(MedievalShipFlagpoleMenuAction action)
    {
        SendMessage(new MedievalShipFlagpoleSelectedMessage(action));
    }
}
