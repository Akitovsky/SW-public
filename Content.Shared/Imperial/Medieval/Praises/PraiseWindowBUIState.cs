using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Praises;

[Serializable, NetSerializable]
public sealed class PraiseWindowBoundUserInterfaceState : BoundUserInterfaceState
{
    public string Message;
    public bool SendButtonDisabled;

    public PraiseWindowBoundUserInterfaceState(string message, bool buttonDisabled)
    {
        Message = message;
        SendButtonDisabled = buttonDisabled;
    }
}

[Serializable, NetSerializable]
public sealed class PraiseWindowMessage : BoundUserInterfaceMessage
{
    public string Reason;

    public PraiseWindowMessage(string reason)
    {
        Reason = reason;
    }
}

[Serializable, NetSerializable]
public enum PraiseWindowUiKey
{
    Key
}
