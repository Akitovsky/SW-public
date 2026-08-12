using System.Threading.Tasks;
using Content.Shared.Bed.Sleep;
using Content.Shared.Damage.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Robust.Shared.Timing;
using NewBeforeStatusEffectAddedEvent = Content.Shared.StatusEffectNew.BeforeStatusEffectAddedEvent;
using NewStatusEffectsSystem = Content.Shared.StatusEffectNew.StatusEffectsSystem;
using OldStatusEffectsSystem = Content.Shared.StatusEffect.StatusEffectsSystem;

namespace Content.Shared.Imperial.Medieval.Magic.AugmentumBuff;

public sealed class AugmentumBuffSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly NewStatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly OldStatusEffectsSystem _oldStatusEffects = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AugmentumBuffComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<AugmentumBuffComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<AugmentumBuffComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<AugmentumBuffComponent, KnockDownAttemptEvent>(OnKnockdownAttempt);
        SubscribeLocalEvent<AugmentumBuffComponent, BeforeOldStatusEffectAddedEvent>(OnBeforeOldStatusEffectAdded);
        SubscribeLocalEvent<AugmentumBuffComponent, NewBeforeStatusEffectAddedEvent>(OnBeforeStatusEffectAdded);
        SubscribeLocalEvent<KnockedDownComponent, ComponentStartup>(OnKnockedDownStartup);
        SubscribeLocalEvent<StunnedComponent, ComponentStartup>(OnStunnedStartup);
    }

    public void ApplyAugmentumBuff(EntityUid target, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero || TerminatingOrDeleted(target))
            return;

        var component = EnsureComp<AugmentumBuffComponent>(target);
        var startTime = component.EndTime > _timing.CurTime
            ? component.EndTime
            : _timing.CurTime;

        component.EndTime = startTime + duration;
        EnsureStaminaProtection(target, component);
        CleanseDisablingEffects(target);
        Dirty(target, component);
        _movement.RefreshMovementSpeedModifiers(target);

        if (!component.TimerRunning)
        {
            component.TimerRunning = true;
            _ = RunBuffTimer(target, component);
        }
    }

    private async Task RunBuffTimer(EntityUid target, AugmentumBuffComponent component)
    {
        while (!TerminatingOrDeleted(target) &&
               TryComp<AugmentumBuffComponent>(target, out var current) &&
               ReferenceEquals(current, component))
        {
            var delay = component.EndTime - _timing.CurTime;
            if (delay <= TimeSpan.Zero)
                break;

            await Timer.Delay(delay);
        }

        if (!TerminatingOrDeleted(target) &&
            TryComp<AugmentumBuffComponent>(target, out var finalComponent) &&
            ReferenceEquals(finalComponent, component))
        {
            RemComp<AugmentumBuffComponent>(target);
        }
    }

    private void OnStartup(Entity<AugmentumBuffComponent> entity, ref ComponentStartup args)
    {
        _movement.RefreshMovementSpeedModifiers(entity.Owner);
    }

    private void OnShutdown(Entity<AugmentumBuffComponent> entity, ref ComponentShutdown args)
    {
        _movement.RefreshMovementSpeedModifiers(entity.Owner);

        if (entity.Comp.OwnsStaminaModifier && !TerminatingOrDeleted(entity.Owner))
            RemComp<StaminaModifierComponent>(entity.Owner);
    }

    private void OnRefreshMovementSpeed(
        Entity<AugmentumBuffComponent> entity,
        ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(entity.Comp.SpeedModifier, entity.Comp.SpeedModifier);
    }

    private void OnKnockdownAttempt(Entity<AugmentumBuffComponent> entity, ref KnockDownAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnKnockedDownStartup(Entity<KnockedDownComponent> entity, ref ComponentStartup args)
    {
        if (HasComp<AugmentumBuffComponent>(entity.Owner))
            RemCompDeferred<KnockedDownComponent>(entity.Owner);
    }

    private void OnStunnedStartup(Entity<StunnedComponent> entity, ref ComponentStartup args)
    {
        if (HasComp<AugmentumBuffComponent>(entity.Owner))
            RemCompDeferred<StunnedComponent>(entity.Owner);
    }

    private void OnBeforeStatusEffectAdded(
        Entity<AugmentumBuffComponent> entity,
        ref NewBeforeStatusEffectAddedEvent args)
    {
        if (args.Effect == SharedStunSystem.StunId ||
            args.Effect == SleepingSystem.StatusEffectForcedSleeping)
        {
            args.Cancelled = true;
        }
    }

    private void OnBeforeOldStatusEffectAdded(
        Entity<AugmentumBuffComponent> entity,
        ref BeforeOldStatusEffectAddedEvent args)
    {
        if (args.EffectKey is "Stun" or "KnockedDown")
            args.Cancelled = true;
    }

    private void EnsureStaminaProtection(EntityUid target, AugmentumBuffComponent component)
    {
        if (HasComp<StaminaModifierComponent>(target))
            return;

        EnsureComp<StaminaModifierComponent>(target);
        component.OwnsStaminaModifier = true;
        Dirty(target, component);
    }

    private void CleanseDisablingEffects(EntityUid target)
    {
        _statusEffects.TryRemoveStatusEffect(target, SharedStunSystem.StunId);
        _statusEffects.TryRemoveStatusEffect(target, SleepingSystem.StatusEffectForcedSleeping);
        _stun.TryUnstun(target);
        RemComp<KnockedDownComponent>(target);

        _oldStatusEffects.TryRemoveStatusEffect(target, "Stun");
        _oldStatusEffects.TryRemoveStatusEffect(target, "KnockedDown");
    }
}
