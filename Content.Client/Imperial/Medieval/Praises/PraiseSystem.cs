using Content.Shared.Imperial.Medieval.Praises;
using Robust.Shared.Network;

namespace Content.Client.Imperial.Medieval.Praises;

public sealed class PraiseSystem : EntitySystem
{
    private PraiseViewWindow? _window;
    private Dictionary<NetUserId, List<PraiseViewRecord>> _records = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PraiseViewMessage>(OnPraiseViewMessage);
    }

    private void OnPraiseViewMessage(PraiseViewMessage ev)
    {
        _records[ev.Target] = ev.Records ?? new();

        if (_window != null)
            return;

        _window = new(_records[ev.Target]);
        _window.Open();
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
