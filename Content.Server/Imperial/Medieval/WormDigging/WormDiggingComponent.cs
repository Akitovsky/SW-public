using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.WormDigging;

[RegisterComponent]
public sealed partial class WormDiggingComponent : Component
{
    [DataField]
    public TimeSpan DiggingDuration = TimeSpan.FromSeconds(3);

    [DataField]
    public EntProtoId WormPrototype = "Worm";

    [DataField]
    public HashSet<string> ValidTiles =
    [
        "FloorPlanetDirt",
        "FloorDirt",
        "FloorDesert",
        "FloorDesert2",
        "FloorDesert3",
        "FloorDesert4",
        "FloorDesert5",
        "FloorDesert6",
        "FloorDesert7",
        "FloorDesert8",
        "FloorDesert9",
    ];
}
