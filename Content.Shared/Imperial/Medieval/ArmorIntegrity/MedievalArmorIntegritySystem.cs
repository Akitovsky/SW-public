using Content.Shared.Armor;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.ArmorIntegrity;

public sealed class MedievalArmorIntegritySystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalArmorIntegrityComponent, ComponentInit>(OnComponentInit);
    }

    private void OnComponentInit(Entity<MedievalArmorIntegrityComponent> ent, ref ComponentInit args)
    {
        if (!_net.IsServer || !TryComp<ArmorComponent>(ent, out var armor))
            return;

        if (ent.Comp.IntactResistances.Count == 0)
        {
            CopyArmorResistances(armor.Modifiers, ent.Comp.IntactResistances);
            Dirty(ent);
            return;
        }

        armor.Modifiers = CreateModifierSet(ent.Comp.IntactResistances);
        Dirty(ent.Owner, armor);
    }

    private static void CopyArmorResistances(
        DamageModifierSet modifiers,
        Dictionary<ProtoId<DamageTypePrototype>, MedievalArmorResistance> resistances)
    {
        foreach (var (damageType, coefficient) in modifiers.Coefficients)
        {
            resistances[damageType] = new MedievalArmorResistance
            {
                Coefficient = coefficient,
            };
        }

        foreach (var (damageType, flatReduction) in modifiers.FlatReduction)
        {
            if (!resistances.TryGetValue(damageType, out var resistance))
            {
                resistance = new MedievalArmorResistance();
                resistances[damageType] = resistance;
            }

            resistance.FlatReduction = flatReduction;
        }
    }

    private static DamageModifierSet CreateModifierSet(
        Dictionary<ProtoId<DamageTypePrototype>, MedievalArmorResistance> resistances)
    {
        var modifiers = new DamageModifierSet();

        foreach (var (damageType, resistance) in resistances)
        {
            if (!MathHelper.CloseTo(resistance.Coefficient, 1f))
                modifiers.Coefficients[damageType] = resistance.Coefficient;

            if (!MathHelper.CloseTo(resistance.FlatReduction, 0f))
                modifiers.FlatReduction[damageType] = resistance.FlatReduction;
        }

        return modifiers;
    }
}
