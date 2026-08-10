using System.Numerics;
using Content.Server.Imperial.Medieval.Ships;
using Content.Shared._RD.Weight.Components;
using Content.Shared._RD.Weight.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests;

[TestFixture]
[TestOf(typeof(ShipGridSystem))]
public sealed class HelmGridWeightTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: HelmGridWeightTestEntity
  components:
  - type: RDWeight
    value: 2
";

    [Test]
    public async Task TracksNestedWeightsAcrossGrids()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entityManager = server.EntMan;
        var mapManager = server.MapMan;
        var mapSystem = entityManager.System<SharedMapSystem>();
        var transformSystem = entityManager.System<SharedTransformSystem>();
        var weightSystem = entityManager.System<RDWeightSystem>();
        var shipGridSystem = entityManager.System<ShipGridSystem>();

        Entity<MapGridComponent> firstGrid = default;
        Entity<MapGridComponent> secondGrid = default;
        EntityUid parent = default;
        EntityUid child = default;

        await server.WaitAssertion(() =>
        {
            firstGrid = map.Grid;
            secondGrid = mapManager.CreateGridEntity(map.MapId);
            shipGridSystem.EnsureGrid(firstGrid);
            shipGridSystem.EnsureGrid(secondGrid);
            transformSystem.SetWorldPosition(secondGrid, new Vector2(10f, 0f));
            mapSystem.SetTile(secondGrid, secondGrid, Vector2i.Zero, new Tile(1));

            parent = entityManager.SpawnEntity(
                "HelmGridWeightTestEntity",
                new EntityCoordinates(firstGrid, 0.5f, 0.5f));
            child = entityManager.SpawnEntity(
                "HelmGridWeightTestEntity",
                new EntityCoordinates(firstGrid, 0.5f, 0.5f));
            transformSystem.SetCoordinates(child, new EntityCoordinates(parent, 0f, 0f));

            Assert.Multiple(() =>
            {
                Assert.That(weightSystem.GetTotal(child), Is.EqualTo(2f));
                Assert.That(weightSystem.GetTotal(parent), Is.EqualTo(4f));
                Assert.That(entityManager.GetComponent<TransformComponent>(parent).GridUid, Is.EqualTo(firstGrid.Owner));
                Assert.That(entityManager.HasComponent<ShipGridComponent>(firstGrid));
                Assert.That(shipGridSystem.GetTotalWeight(firstGrid), Is.EqualTo(4f));
            });
            Assert.That(shipGridSystem.GetTotalWeight(secondGrid), Is.Zero);

            transformSystem.SetCoordinates(parent, new EntityCoordinates(secondGrid, 0.5f, 0.5f));

            var secondGridCache = entityManager.GetComponent<ShipGridComponent>(secondGrid);
            Assert.Multiple(() =>
            {
                Assert.That(shipGridSystem.GetTotalWeight(firstGrid), Is.Zero);
                Assert.That(shipGridSystem.GetTotalWeight(secondGrid), Is.EqualTo(4f));
                Assert.That(secondGridCache.TileCount, Is.EqualTo(1));
            });

            var childWeight = entityManager.GetComponent<RDWeightComponent>(child);
            weightSystem.ChangeWeightWithMod((child, childWeight), 2f);

            Assert.That(shipGridSystem.GetTotalWeight(secondGrid), Is.EqualTo(6f));

            entityManager.DeleteEntity(child);
            Assert.That(shipGridSystem.GetTotalWeight(secondGrid), Is.EqualTo(2f));

            entityManager.DeleteEntity(parent);
            Assert.That(shipGridSystem.GetTotalWeight(secondGrid), Is.Zero);
        });

        await pair.CleanReturnAsync();
    }
}
