using System.Linq;
using System.Threading.Tasks;
using Content.Server.Imperial.Medieval.PlayerCreations.Administration;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.Medieval.CCVar;
using Content.Shared.Imperial.Medieval.PlayerCreations;
using Content.Shared.Imperial.Medieval.PlayerCreations.Books;
using Content.Shared.Paper;
using Robust.Shared.Configuration;
using Robust.Shared.Random;


namespace Content.Server.Imperial.Medieval.PlayerCreations.Books;
public sealed class RandomCreationsBookSystem : EntitySystem
{

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly CreationsSystem _creations = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    private Dictionary<string, int> _booksSpawned = new();
    private List<CreationBook>? _acceptedBooksCache;
    private Task<List<CreationBook>>? _acceptedBooksLoading;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RandomCreationsBookComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStart);
    }

    public void OnRoundStart(RoundStartedEvent args)
    {
        _booksSpawned = new();
        _acceptedBooksCache = null;
        _acceptedBooksLoading = null;
    }

    private async Task<List<CreationBook>> GetAcceptedBooksCachedAsync()
    {
        if (_acceptedBooksCache != null)
            return _acceptedBooksCache;

        _acceptedBooksLoading ??= _creations.GetAcceptedCreationBooks();

        var result = await _acceptedBooksLoading;
        _acceptedBooksCache = result;
        return result;
    }

    public async void OnStartup(EntityUid uid, RandomCreationsBookComponent comp, ComponentStartup args)
    {
        var maxPaintings = _cfg.GetCVar(MedievalCCVars.CreationsMaxBooks);
        var cached = await GetAcceptedBooksCachedAsync();
        var acceptedBooks = new List<CreationBook>(cached);

        foreach (var spawned in _booksSpawned)
        {
            if (spawned.Value < maxPaintings)
                continue;
            var toRemove = acceptedBooks
                .FirstOrDefault(v => v.Text == spawned.Key);

            if (toRemove != null)
                acceptedBooks.Remove(toRemove);
        }

        if (acceptedBooks.Count <= 0)
        {
            QueueDel(uid);
            return;
        }

        var selected = _random.Pick(acceptedBooks);
        _booksSpawned.TryAdd(selected.Text, 0);
        _booksSpawned[selected.Text] += 1;

        if (TryComp<PaperComponent>(uid, out var paper))
        {
            paper.Content = selected.Text;
        }
        _metaData.SetEntityName(uid, $"{selected.Name}");
        _metaData.SetEntityDescription(uid, $"{selected.Description} - {selected.Author}");

    }
}
