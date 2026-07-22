using Content.Server._CP14.Workbench;
using Content.Shared.Clothing.Components;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval.ArmorIntegrity;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.Medieval.ArmorIntegrity;

public sealed class MedievalArmorRepairSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MedievalArmorIntegritySystem _armorIntegrity = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalRepairArmorComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<MedievalRepairArmorComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<MedievalRepairArmorComponent, ExaminedEvent>(OnRepairToolExamined);
        SubscribeLocalEvent<MedievalRepairStationComponent, ExaminedEvent>(OnRepairStationExamined);
        SubscribeLocalEvent<MedievalArmorIntegrityComponent, MedievalArmorRepairDoAfterEvent>(OnRepairDoAfter);
    }

    private void OnAfterInteract(Entity<MedievalRepairArmorComponent> repairTool, ref AfterInteractEvent args)
    {
        if (args.Handled ||
            !args.CanReach ||
            args.Target is not { } target ||
            !TryComp<MedievalArmorIntegrityComponent>(target, out var armor))
        {
            return;
        }

        if (IsArmorEquipped(target))
        {
            args.Handled = true;
            return;
        }

        if (repairTool.Comp.RepairType != armor.RepairType)
        {
            _popup.PopupEntity(
                Loc.GetString("armor-repair-wrong-tool-popup"),
                args.User,
                args.User,
                PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        if (MathHelper.CloseTo(armor.MaxArmorHP, 0f))
        {
            _popup.PopupEntity(
                Loc.GetString("armor-repair-irreparable-popup"),
                args.User,
                args.User,
                PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        if (MathHelper.CloseTo(armor.CurrentArmorHP, armor.MaxArmorHP))
        {
            _popup.PopupEntity(
                Loc.GetString("armor-repair-fully-repaired-popup"),
                args.User,
                args.User);
            args.Handled = true;
            return;
        }

        if (repairTool.Comp.IsSpendable && repairTool.Comp.Charges <= 0)
            return;

        var station = FindRepairStation(
            target,
            armor.RepairType,
            repairTool.Comp.RepairStationSearchRange);
        var stationMaxArmorRemovalModifier = station?.Comp.StationMaxArmorRemovalModifier ?? 1f;
        var repairDelayModifier = station?.Comp.RepairDelayModifier ?? 1f;
        var repairEvent = new MedievalArmorRepairDoAfterEvent(
            stationMaxArmorRemovalModifier,
            repairDelayModifier);
        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            args.User,
            GetRepairDelay(args.User, repairTool.Comp, repairDelayModifier),
            repairEvent,
            target,
            target: target,
            used: repairTool.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
            BlockDuplicate = true,
            CancelDuplicate = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            return;

        args.Handled = true;
        PlayUseSound(repairTool, target);
    }

    private void OnInteractUsing(Entity<MedievalRepairArmorComponent> donor, ref InteractUsingEvent args)
    {
        if (args.Handled ||
            !donor.Comp.IsSpendable ||
            !TryComp<MedievalRepairArmorComponent>(args.Used, out var recipient) ||
            !recipient.IsSpendable ||
            MetaData(donor).EntityPrototype?.ID != MetaData(args.Used).EntityPrototype?.ID)
        {
            return;
        }

        recipient.Charges = Math.Max(0, recipient.Charges) + Math.Max(0, donor.Comp.Charges);
        QueueDel(donor);
        args.Handled = true;
    }

    private void OnRepairDoAfter(
        Entity<MedievalArmorIntegrityComponent> armor,
        ref MedievalArmorRepairDoAfterEvent args)
    {
        if (args.Handled ||
            args.Cancelled ||
            args.Used is not { } used ||
            !TryComp<MedievalRepairArmorComponent>(used, out var repairTool) ||
            repairTool.RepairType != armor.Comp.RepairType ||
            IsArmorEquipped(armor) ||
            MathHelper.CloseTo(armor.Comp.CurrentArmorHP, armor.Comp.MaxArmorHP) ||
            repairTool.IsSpendable && repairTool.Charges <= 0)
        {
            return;
        }

        var oldCurrentArmorHp = armor.Comp.CurrentArmorHP;
        var oldMaxArmorHp = armor.Comp.MaxArmorHP;

        _armorIntegrity.SetCurrentArmorHP(armor, armor.Comp.CurrentArmorHP + repairTool.RepairAmount);

        var maxArmorRemoval = repairTool.MaxArmorRemove * args.StationMaxArmorRemovalModifier;
        if (HasComp<CrafterTraitComponent>(args.User))
            maxArmorRemoval *= repairTool.SkilledCrafterMaxArmorRemovalModifier;

        _armorIntegrity.SetMaxArmorHP(armor, armor.Comp.MaxArmorHP - maxArmorRemoval);

        var toolSpent = false;
        if (repairTool.IsSpendable)
        {
            repairTool.Charges = Math.Max(0, repairTool.Charges - 1);

            if (repairTool.Charges == 0)
            {
                QueueDel(used);
                toolSpent = true;
            }
        }

        args.Handled = true;

        var armorChanged = !MathHelper.CloseTo(oldCurrentArmorHp, armor.Comp.CurrentArmorHP) ||
            !MathHelper.CloseTo(oldMaxArmorHp, armor.Comp.MaxArmorHP);
        if (toolSpent ||
            !armorChanged ||
            MathHelper.CloseTo(armor.Comp.CurrentArmorHP, armor.Comp.MaxArmorHP))
        {
            return;
        }

        args.Args.Delay = TimeSpan.FromSeconds(GetRepairDelay(
            args.User,
            repairTool,
            args.RepairDelayModifier));
        args.Repeat = true;
        PlayUseSound((used, repairTool), armor.Owner);
    }

    private void OnRepairToolExamined(Entity<MedievalRepairArmorComponent> repairTool, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var maxArmorRemoval = repairTool.Comp.MaxArmorRemove;
        if (HasComp<CrafterTraitComponent>(args.Examiner))
            maxArmorRemoval *= repairTool.Comp.SkilledCrafterMaxArmorRemovalModifier;

        using (args.PushGroup(nameof(MedievalRepairArmorComponent)))
        {
            if (MathHelper.CloseTo(maxArmorRemoval, 0f))
            {
                args.PushMarkup(Loc.GetString("armor-repair-tool-no-max-durability-cost"));
            }
            else
            {
                args.PushMarkup(Loc.GetString(
                    "armor-repair-tool-max-durability-cost",
                    ("amount", FormatNumber(maxArmorRemoval))));
            }

            args.PushMarkup(Loc.GetString(GetRepairTypeLocKey(repairTool.Comp.RepairType)));

            if (repairTool.Comp.IsSpendable)
            {
                args.PushMarkup(Loc.GetString(
                    "armor-repair-tool-charges",
                    ("charges", repairTool.Comp.Charges)));
            }
        }
    }

    private void OnRepairStationExamined(Entity<MedievalRepairStationComponent> station, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(MedievalRepairStationComponent)))
        {
            args.PushMarkup(Loc.GetString(
                "armor-repair-station-speed",
                ("modifier", FormatInverse(station.Comp.RepairDelayModifier))));
            args.PushMarkup(Loc.GetString(
                "armor-repair-station-max-durability-cost",
                ("modifier", FormatInverse(station.Comp.StationMaxArmorRemovalModifier))));
            args.PushMarkup(Loc.GetString(GetRepairTypeLocKey(station.Comp.RepairType)));
        }
    }

    private Entity<MedievalRepairStationComponent>? FindRepairStation(
        EntityUid armor,
        MedievalArmorRepairType repairType,
        float searchRange)
    {
        foreach (var station in _lookup.GetEntitiesInRange<MedievalRepairStationComponent>(
                     Transform(armor).Coordinates,
                     searchRange))
        {
            if (station.Comp.RepairType == repairType)
                return station;
        }

        return null;
    }

    private bool IsArmorEquipped(EntityUid armor)
    {
        return TryComp<ClothingComponent>(armor, out var clothing) &&
            clothing.InSlotFlag is { } slotFlag &&
            (clothing.Slots & slotFlag) != 0;
    }

    private float GetRepairDelay(
        EntityUid user,
        MedievalRepairArmorComponent repairTool,
        float stationModifier)
    {
        var intelligence = repairTool.BaselineIntelligence;
        if (TryComp<SkillsComponent>(user, out var skills))
        {
            intelligence = skills.Levels.GetValueOrDefault(
                SharedSkillsSystem.IntelligenceId,
                repairTool.BaselineIntelligence);
        }

        var delay = repairTool.RepairTime;
        if (intelligence > repairTool.BaselineIntelligence)
            delay *= 1f - 0.05f * intelligence;
        else if (intelligence < repairTool.BaselineIntelligence)
            delay *= 1f + 0.15f * (repairTool.BaselineIntelligence - intelligence);

        return Math.Max(repairTool.MinimumRepairDelay, delay * stationModifier);
    }

    private void PlayUseSound(Entity<MedievalRepairArmorComponent> repairTool, EntityUid target)
    {
        if (repairTool.Comp.UseSound != null)
            _audio.PlayPvs(repairTool.Comp.UseSound, target);
    }

    private static string GetRepairTypeLocKey(MedievalArmorRepairType repairType)
    {
        return repairType == MedievalArmorRepairType.Sewing
            ? "armor-repair-type-sewing"
            : "armor-repair-type-smithing";
    }

    private static object FormatInverse(float value)
    {
        return MathHelper.CloseTo(value, 0f)
            ? "∞"
            : FormatNumber(1f / value);
    }

    private static float FormatNumber(float value)
    {
        return MathF.Round(value, 2);
    }
}
