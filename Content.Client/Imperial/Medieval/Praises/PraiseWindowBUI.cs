using Content.Shared.Imperial.Medieval.Praises;

namespace Content.Client.Imperial.Medieval.Praises;

public sealed class PraiseWindowBoundUserInterface : BoundUserInterface
{
    private PraiseWindow? _window;

    public PraiseWindowBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = new PraiseWindow();
        _window.OpenCentered();
        _window.OnClose += Close;
        _window.OnSendButtonPressed += reason =>
        {
            SendMessage(new PraiseWindowMessage(reason));
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is PraiseWindowBoundUserInterfaceState pwState)
            _window?.UpdateState(pwState);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _window?.Dispose();
    }
}
