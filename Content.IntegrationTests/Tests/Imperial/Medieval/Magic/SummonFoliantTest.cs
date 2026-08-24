using System.Linq;
using System.Collections.Generic;
using Content.Server.Imperial.Medieval.Magic.BindStoreOnEquip;
using Content.Server.Imperial.Medieval.Magic.MedievalSpawnInFreeSlot;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Inventory;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Imperial.Medieval.Magic;

[TestFixture]
public sealed class SummonFoliantTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestSummonFoliantPouch
  components:
  - type: ContainerContainer
    containers:
      pouch: !type:Container

- type: entity
  id: TestSummonFoliantProjectile
  components:
  - type: FoliantToHandTeleporter
";

    [Test]
    public async Task SummonDoesNotDisplaceHeldItems()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false
        });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await pair.RunTicksSync(5);

        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var handsSystem = server.System<SharedHandsSystem>();
        var containerSystem = server.System<SharedContainerSystem>();
        var inventorySystem = server.System<InventorySystem>();
        var placementSystem = server.System<MedievalSpawnInFreeSlotSystem>();

        await server.WaitAssertion(() =>
        {
            var player = playerMan.Sessions.First().AttachedEntity!.Value;
            var hands = entMan.GetComponent<HandsComponent>(player);
            var heldItems = new List<EntityUid>();
            var heldPrototypes = new[] { "MedievalCommsCrystallColl", "MedievalKeyWizard" };

            Assert.That(hands.Count, Is.EqualTo(heldPrototypes.Length));
            foreach (var prototype in heldPrototypes)
            {
                var held = entMan.SpawnEntity(prototype, map.GridCoords);
                Assert.That(handsSystem.TryPickupAnyHand(player, held), Is.True);
                heldItems.Add(held);
            }

            var pouch = entMan.SpawnEntity("TestSummonFoliantPouch", map.GridCoords);
            var foliant = entMan.SpawnEntity("Crowbar", map.GridCoords);
            var bind = entMan.EnsureComponent<BindStoreOnEquipComponent>(foliant);
#pragma warning disable RA0002
            bind.BindedEntity = player;
#pragma warning restore RA0002
            Assert.That(containerSystem.TryGetContainer(pouch, "pouch", out var container), Is.True);
            Assert.That(containerSystem.Insert(foliant, container), Is.True);

            var projectile = entMan.SpawnEntity("TestSummonFoliantProjectile", map.GridCoords);
            var spellEvent = new MedievalAfterSpawnEntityBySpellEvent
            {
                Performer = player,
                SpawnedEntity = projectile
            };
            entMan.EventBus.RaiseLocalEvent(projectile, spellEvent);

            Assert.That(handsSystem.EnumerateHeld((player, hands)).ToList(), Is.EquivalentTo(heldItems));
            Assert.That(container.Contains(foliant), Is.False);
            Assert.That(containerSystem.TryGetContainingContainer(foliant, out var carriedContainer), Is.True);
            Assert.That(inventorySystem.GetHandOrInventoryEntities(player), Does.Contain(carriedContainer.Owner));

            entMan.EventBus.RaiseLocalEvent(projectile, spellEvent);
            Assert.That(containerSystem.TryGetContainingContainer(foliant, out var sameContainer), Is.True);
            Assert.That(sameContainer.Owner, Is.EqualTo(carriedContainer.Owner));

            Assert.That(containerSystem.Insert(foliant, container), Is.True);

            Assert.That(handsSystem.TryDrop(player, heldItems[0]), Is.True);
            entMan.EventBus.RaiseLocalEvent(projectile, spellEvent);

            Assert.That(handsSystem.IsHolding(player, foliant), Is.True);
            Assert.That(container.Contains(foliant), Is.False);

            var heldWithFoliant = handsSystem.EnumerateHeld((player, hands)).ToList();
            entMan.EventBus.RaiseLocalEvent(projectile, spellEvent);
            Assert.That(handsSystem.EnumerateHeld((player, hands)).ToList(), Is.EquivalentTo(heldWithFoliant));

            var directlyPlacedItem = entMan.SpawnEntity("Crowbar", map.GridCoords);
            Assert.That(placementSystem.TryPlaceInFreeSlot(player, directlyPlacedItem), Is.True);
            Assert.That(containerSystem.TryGetContainingContainer(directlyPlacedItem, out var directContainer), Is.True);
            Assert.That(inventorySystem.GetHandOrInventoryEntities(player), Does.Contain(directContainer.Owner));

            Assert.That(placementSystem.TryPlaceInFreeSlot(player, directlyPlacedItem), Is.True);
            Assert.That(containerSystem.TryGetContainingContainer(directlyPlacedItem, out var unchangedContainer), Is.True);
            Assert.That(unchangedContainer.Owner, Is.EqualTo(directContainer.Owner));
        });

        await pair.CleanReturnAsync();
    }
}
