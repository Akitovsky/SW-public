using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Ships.Anchor;

[Serializable, NetSerializable]
public enum UniversalSecurityUiKey : byte
{
    Lock,
    Lockpick,
    Key
}

[Serializable, NetSerializable]
public sealed class UniversalLockBuiState : BoundUserInterfaceState
{
    public int MaxValue;
    public int Length;

    public UniversalLockBuiState(int maxValue, int length)
    {
        MaxValue = maxValue;
        Length = length;
    }
}

[Serializable, NetSerializable]
public sealed class UniversalKeyBuiState : BoundUserInterfaceState
{
    public UniversalKeyBuiState() { }
}

[Serializable, NetSerializable]
public sealed class UniversalLockpickBuiState : BoundUserInterfaceState
{
    public int MaxValue;
    public int Length;
    public int[] CodeState;

    public UniversalLockpickBuiState(int maxValue, int length, int[] codeState)
    {
        MaxValue = maxValue;
        Length = length;
        CodeState = codeState;
    }
}

[Serializable, NetSerializable]
public sealed class UniversalLockSetCodeMessage : BoundUserInterfaceMessage
{
    public int[] NewCode;

    public UniversalLockSetCodeMessage(int[] newCode)
    {
        NewCode = newCode;
    }
}

[Serializable, NetSerializable]
public sealed class UniversalLockpickSetCodeMessage : BoundUserInterfaceMessage
{
    public int[] NewCode;
    public UniversalLockpickSetCodeMessage(int[] newCode)
    {
        NewCode = newCode;
    }
}

[Serializable, NetSerializable]
public sealed class UniversalKeySetCodeMessage : BoundUserInterfaceMessage
{
    public int[] NewCode;
    public UniversalKeySetCodeMessage(int[] newCode)
    {
        NewCode = newCode;
    }
}

[Serializable, NetSerializable]
public enum MedievalDoorKeyCheckVisual : byte
{
    State
}
