using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Praises;

[Serializable, NetSerializable]
public sealed class PraiseWindowMessage : EntityEventArgs
{
    public bool Open;
    public string Message = default!;
    public bool SendButtonDisabled;
}

[Serializable, NetSerializable]
public sealed class PraiseWindowPraiseMessage : EntityEventArgs
{
    public string Reason = default!;
}
