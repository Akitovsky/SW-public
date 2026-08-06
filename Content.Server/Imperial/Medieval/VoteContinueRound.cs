using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Events;
using Content.Server.Voting;
using Content.Server.Voting.Managers;
using Content.Shared.GameTicking;
using Robust.Shared.Timing;

namespace Content.Server.GameTicking.Systems;

public sealed partial class AutoRoundEndSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IVoteManager _voteManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private bool _isEnded;
    private bool _leadEventTriggered;

    private TimeSpan _targetDuration;
    private readonly TimeSpan _maxDuration = TimeSpan.FromHours(5);
    private readonly TimeSpan _voteLeadTime = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _extensionTime = TimeSpan.FromHours(1);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStart);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundCleanup);
    }

    private void OnRoundStart(RoundStartingEvent ev)
    {
        ResetState();
    }

    private void OnRoundCleanup(RoundRestartCleanupEvent ev)
    {
        ResetState();
    }

    private void ResetState()
    {
        _isEnded = false;
        _leadEventTriggered = false;
        _targetDuration = TimeSpan.FromHours(3);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_isEnded || _ticker.RunLevel != GameRunLevel.InRound)
            return;

        var currentDuration = _ticker.RoundDuration();

        if (currentDuration >= _targetDuration)
        {
            _isEnded = true;
            _ticker.EndRound(Loc.GetString("auto-round-end-reason"));
            return;
        }

        if (!_leadEventTriggered && currentDuration >= _targetDuration - _voteLeadTime)
        {
            _leadEventTriggered = true;

            if (_targetDuration < _maxDuration)
            {
                StartExtensionVote();
            }
            else
            {
                ArmyAttack();
            }
        }
    }

    private void StartExtensionVote()
    {
        var options = new VoteOptions
        {
            Title = Loc.GetString("ui-vote-extend-round-title"),
            Options =
            {
                (Loc.GetString("ui-vote-extend-yes"), "yes"),
                (Loc.GetString("ui-vote-extend-no"), "no")
            },
            Duration = TimeSpan.FromMinutes(3),
            DisplayVotes = true
        };

        var vote = _voteManager.CreateVote(options);

        vote.OnFinished += (_, _) =>
        {
            var yes = vote.VotesPerOption["yes"];
            var no = vote.VotesPerOption["no"];

            if (yes > no)
            {
                _targetDuration += _extensionTime;
                _leadEventTriggered = false;
                _chatManager.DispatchServerAnnouncement(Loc.GetString("ui-vote-extend-success"));
            }
            else
            {
                _chatManager.DispatchServerAnnouncement(Loc.GetString("ui-vote-extend-fail"));
            }
        };

        _chatManager.DispatchServerAnnouncement(Loc.GetString("ui-vote-extend-announcement"));
    }

    private void ArmyAttack()
    {
        _chatManager.DispatchServerAnnouncement(Loc.GetString("auto-round-end-army-attack"));

        // TODO: Army attack logic
    }
}
