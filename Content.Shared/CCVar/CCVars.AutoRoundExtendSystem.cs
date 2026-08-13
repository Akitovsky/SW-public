using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Начальное время до завершения раунда (в минутах).
    /// </summary>
    public static readonly CVarDef<int> AutoRoundInitialDuration =
        CVarDef.Create("game.auto_round_initial_duration", 180, CVar.SERVERONLY);

    /// <summary>
    /// Максимальное время раунда со всеми продлениями (в минутах).
    /// </summary>
    public static readonly CVarDef<int> AutoRoundMaxDuration =
        CVarDef.Create("game.auto_round_max_duration", 300, CVar.SERVERONLY);

    /// <summary>
    /// Время, на которое продлевается раунд при успешном голосовании (в минутах).
    /// </summary>
    public static readonly CVarDef<int> AutoRoundExtensionTime =
        CVarDef.Create("game.auto_round_extension_time", 60, CVar.SERVERONLY);

    /// <summary>
    /// За сколько минут до конца запускается голосование.
    /// </summary>
    public static readonly CVarDef<int> AutoRoundVoteLeadTime =
        CVarDef.Create("game.auto_round_vote_lead_time", 15, CVar.SERVERONLY);
}
