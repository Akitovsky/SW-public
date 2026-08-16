using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Praises;

//used in 'PraisesViewWindow'
[Serializable, NetSerializable]
public sealed class PraiseViewRecord
{
    public string Reason = "";
    public DateTime Date;
}

[Serializable, NetSerializable]
public sealed class PraiseViewOpenedMessage : EntityEventArgs
{
    public NetUserId Target;
}

[Serializable, NetSerializable]
public sealed class PraiseViewMessage : EntityEventArgs
{
    public NetUserId Target;
    public List<PraiseViewRecord>? Records;
}
