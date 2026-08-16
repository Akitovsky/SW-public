using Content.Shared.EntityEffects;
using Content.Shared.Imperial.Medieval.Magic.AugmentumBuff;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Magic.AugmentumBuff;

public sealed partial class ApplyAugmentumBuff : EntityEffect
{
    [DataField(required: true)]
    public float Duration;

    public override void Effect(EntityEffectBaseArgs args)
    {
        args.EntityManager.System<AugmentumBuffSystem>()
            .ApplyAugmentumBuff(args.TargetEntity, TimeSpan.FromSeconds(Duration));
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => "";
}
