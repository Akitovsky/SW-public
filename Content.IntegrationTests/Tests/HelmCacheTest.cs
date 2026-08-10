using System.Numerics;
using Content.Server.Imperial.Medieval.Ships.Helm;
using Content.Shared.Imperial.Medieval.Ships.Helm;
using Content.Shared.Imperial.Medieval.Ships.Sail;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

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
        EntityUid sailUid = default;
        EntityUid oarUid = default;

        await server.WaitAssertion(() =>
        {
            secondGrid = server.MapMan.CreateGridEntity(map.MapId);
            transformSystem.SetWorldPosition(secondGrid, new Vector2(10f, 0f));

            helmUid = entityManager.SpawnEntity("HelmCacheTestHelm", new EntityCoordinates(map.Grid, 0f, 0f));
            sailUid = entityManager.SpawnEntity("HelmCacheTestSail", new EntityCoordinates(map.Grid, 0f, 0f));
            oarUid = entityManager.SpawnEntity("HelmCacheTestSteeringOar", new EntityCoordinates(map.Grid, 0f, 0f));

            var helm = entityManager.GetComponent<HelmComponent>(helmUid);
            Assert.Multiple(() =>
            {
                Assert.That(helm.Sails, Does.Contain(sailUid));
                Assert.That(helm.SteeringOars, Does.Contain(oarUid));
                Assert.That(helm.CachedSteeringPower, Is.EqualTo(12f));
            });

            transformSystem.SetCoordinates(sailUid, new EntityCoordinates(secondGrid, 0f, 0f));
            Assert.That(helm.Sails, Does.Not.Contain(sailUid));

            transformSystem.SetCoordinates(sailUid, new EntityCoordinates(map.Grid, 0f, 0f));
            Assert.That(helm.Sails, Does.Contain(sailUid));

            entityManager.DeleteEntity(sailUid);
            entityManager.DeleteEntity(oarUid);

            Assert.Multiple(() =>
            {
                Assert.That(helm.Sails, Is.Empty);
                Assert.That(helm.SteeringOars, Is.Empty);
                Assert.That(helm.CachedSteeringPower, Is.Zero);
            });
        });

        await pair.CleanReturnAsync();
    }
}
