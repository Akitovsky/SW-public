using Content.Shared.Imperial.Medieval.Praises;
using Robust.Shared.Network;

namespace Content.Client.Imperial.Medieval.Praises;

public sealed class PraiseSystem : EntitySystem
{
    private PraiseWindow? _praiseWindow;
    private PraiseViewWindow? _viewWindow;
    private PraiseRatingWindow? _ratingWindow;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PraiseWindowMessage>(OnPraiseWindowMessage);
        SubscribeNetworkEvent<PraiseViewMessage>(OnPraiseViewMessage);
        SubscribeNetworkEvent<PraiseRatingMessage>(OnPraiseRatingMessage);
    }

    private void OnPraiseWindowMessage(PraiseWindowMessage ev)
    {
        if (ev.Open && _praiseWindow != null)
        {
            _praiseWindow.Dispose();
            _praiseWindow = null;
        }

        _praiseWindow = new();
        _praiseWindow.OnSendButtonPressed += reason => RaiseNetworkEvent(new PraiseWindowPraiseMessage { Reason = reason });
        _praiseWindow.Update(ev);
        _praiseWindow.OpenCentered();
    }

    private void OnPraiseViewMessage(PraiseViewMessage ev)
    {
        if (_viewWindow != null)
            return;

        _viewWindow = new(ev.Records, ev.Admin);
        _viewWindow.OnEditWeightButtonPressed += record => RaiseNetworkEvent(new PraiseViewEditMessage { Target = ev.Target, Record = record });
        _viewWindow.OnDeleteButtonPressed += record => RaiseNetworkEvent(new PraiseViewDeleteMessage { Target = ev.Target, Record = record });
        _viewWindow.OpenCentered();
    }

    private void OnPraiseRatingMessage(PraiseRatingMessage ev)
    {
        if (_ratingWindow != null)
            return;

        _ratingWindow = new(ev.Rating);
        _ratingWindow.OpenCentered();
    }

    public void ToggleView(NetUserId target)
    {
        if (_viewWindow != null)
        {
            _viewWindow.Dispose();
            _viewWindow = null;
        }

        RaiseNetworkEvent(new PraiseViewOpenedMessage { Target = target });
    }

    public void ToggleRating()
    {
        if (_ratingWindow != null)
        {
            _ratingWindow.Dispose();
            _ratingWindow = null;
        }

        RaiseNetworkEvent(new PraiseRatingOpenedMessage());
    }
}
