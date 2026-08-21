using Content.Shared.Administration;
using Robust.Client.Player;
using Robust.Shared.Console;

namespace Content.Client.Imperial.Medieval.Praises;

[AnyCommand] //should be an admin command but server will check if user is an admin anyway so it's easier to implement it on the client
public sealed class ViewPraisesCommand : IConsoleCommand
{
    [Dependency] private readonly IPlayerManager _playerMan = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;

    public string Command => "viewpraises";

    public string Description => "";

    public string Help => "viewpraises USERNAME";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError("Command requires a single argument (target's username).");
            return;
        }

        if (!_playerMan.TryGetSessionByUsername(args[0], out var target))
        {
            shell.WriteError("Failed to resolve session by username.");
            return;
        }

        _entMan.EntitySysManager.GetEntitySystem<PraiseSystem>().ToggleView(target.UserId);
    }
}
