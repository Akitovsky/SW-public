using Content.Shared.Imperial.Medieval.Praises;
using Robust.Shared.Network;

namespace Content.Client.Imperial.Medieval.Praises;

public sealed class PraiseSystem : EntitySystem
{
    private PraiseViewWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PraiseViewMessage>(OnPraiseViewMessage);
    }

    private void OnPraiseViewMessage(PraiseViewMessage ev)
    {
        if (_window != null)
            return;

        _window = new(ev.Records, ev.Admin);
        _window.OnEditWeightButtonPressed += record => RaiseNetworkEvent(new PraiseViewEditMessage { Target = ev.Target, Record = record });
        _window.OnDeleteButtonPressed += record => RaiseNetworkEvent(new PraiseViewDeleteMessage { Target = ev.Target, Record = record });
        _window.OpenCentered();
    }

    public void ToggleView(NetUserId target)
    {
        if (_window != null)
        {
            _window.Dispose();
            _window = null;
        }

        RaiseNetworkEvent(new PraiseViewOpenedMessage { Target = target });
    }
}
