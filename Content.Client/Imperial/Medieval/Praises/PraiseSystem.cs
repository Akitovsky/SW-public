using Content.Shared.Imperial.Medieval.Praises;
using Robust.Shared.Network;

namespace Content.Client.Imperial.Medieval.Praises;

public sealed class PraiseSystem : EntitySystem
{
    private PraiseViewWindow? _viewWindow;
    private PraiseRatingWindow? _ratingWindow;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PraiseViewMessage>(OnPraiseViewMessage);
        SubscribeNetworkEvent<PraiseRatingMessage>(OnPraiseRatingMessage);
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
