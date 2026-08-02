using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.WormDigging;

[Serializable, NetSerializable]
public sealed partial class WormDiggingDoAfterEvent : DoAfterEvent
{
    [DataField(required: true)]
    public NetCoordinates ClickLocation { get; private set; }

    private WormDiggingDoAfterEvent()
    {
    }

    public WormDiggingDoAfterEvent(NetCoordinates clickLocation)
    {
        ClickLocation = clickLocation;
    }

    public override DoAfterEvent Clone()
    {
        return new WormDiggingDoAfterEvent(ClickLocation);
    }
}
