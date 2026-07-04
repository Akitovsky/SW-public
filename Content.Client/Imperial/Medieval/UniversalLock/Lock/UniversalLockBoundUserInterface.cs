using Content.Shared.Imperial.Medieval.Ships.Anchor;

namespace Imperial.Medieval.UniversalLock.Lock;

public sealed class UniversalLockBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private UniversalLockWindow? _window;

    public UniversalLockBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _window = new UniversalLockWindow();

        _window.OpenCentered();

        _window.OnSetCode += code => SendMessage(new UniversalLockSetCodeMessage(code));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not UniversalLockBuiState lockState)
            return;

        _window?.UpdateState(lockState.MaxValue, lockState.Length);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _window?.Close();
        _window?.Dispose();
    }
}
