using System.Numerics;
using Content.Server.Imperial.Medieval.Ships;
using Content.Server.Imperial.Medieval.Ships.Helm;
using Content.Shared.Imperial.Medieval.Ships.Helm;
using Content.Shared.Imperial.Medieval.Ships.Sail;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Log;

namespace Content.IntegrationTests.Tests;

[TestFixture]
[TestOf(typeof(HelmSystem))]
public sealed class HelmCacheTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: HelmCacheTestHelm
  components:
  - type: Helm

- type: entity
  id: HelmCacheTestSail
  components:
  - type: Appearance
  - type: Sail

- type: entity
  id: HelmCacheTestSteeringOar
  components:
  - type: SteeringOar
    power: 12
";

    [Test]
    public async Task TracksAndRemovesGridParts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entityManager = server.EntMan;
        var transformSystem = entityManager.System<SharedTransformSystem>();
        var secondGrid = default(Entity<MapGridComponent>);
        EntityUid helmUid = default;
        EntityUid duplicateHelmUid = default;
        EntityUid sailUid = default;
        EntityUid oarUid = default;

        await server.WaitAssertion(() =>
        {
            secondGrid = server.MapMan.CreateGridEntity(map.MapId);
            transformSystem.SetWorldPosition(secondGrid, new Vector2(10f, 0f));

            helmUid = entityManager.SpawnEntity("HelmCacheTestHelm", new EntityCoordinates(map.Grid, 0f, 0f));
            sailUid = entityManager.SpawnEntity("HelmCacheTestSail", new EntityCoordinates(map.Grid, 0f, 0f));
            oarUid = entityManager.SpawnEntity("HelmCacheTestSteeringOar", new EntityCoordinates(map.Grid, 0f, 0f));

            var grid = entityManager.GetComponent<ShipGridComponent>(map.Grid);
            Assert.Multiple(() =>
            {
                Assert.That(grid.Helm, Is.EqualTo(helmUid));
                Assert.That(grid.Sails, Does.Contain(sailUid));
                Assert.That(grid.SteeringPower, Is.EqualTo(12f));
            });

            transformSystem.SetCoordinates(sailUid, new EntityCoordinates(secondGrid, 0f, 0f));
            Assert.That(grid.Sails, Does.Not.Contain(sailUid));

            transformSystem.SetCoordinates(sailUid, new EntityCoordinates(map.Grid, 0f, 0f));
            Assert.That(grid.Sails, Does.Contain(sailUid));

            entityManager.DeleteEntity(sailUid);
            entityManager.DeleteEntity(oarUid);

            Assert.Multiple(() =>
            {
                Assert.That(grid.Sails, Is.Empty);
                Assert.That(grid.SteeringPower, Is.Zero);
            });
        });

        pair.ServerLogHandler.FailureLevel = LogLevel.Fatal;
        await server.WaitAssertion(() =>
        {
            duplicateHelmUid = entityManager.SpawnEntity(
                "HelmCacheTestHelm",
                new EntityCoordinates(map.Grid, 0f, 0f));

            var grid = entityManager.GetComponent<ShipGridComponent>(map.Grid);
            Assert.That(grid.Helm, Is.EqualTo(helmUid));
        });
        pair.ServerLogHandler.FailureLevel = LogLevel.Error;

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entityManager.EntityExists(helmUid), Is.True);
                Assert.That(entityManager.EntityExists(duplicateHelmUid), Is.False);
                Assert.That(entityManager.GetComponent<ShipGridComponent>(map.Grid).Helm, Is.EqualTo(helmUid));
            });
        });

        await pair.CleanReturnAsync();
    }
}
