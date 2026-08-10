using Content.Server.Imperial.Medieval.Ships;
using Content.Shared.Imperial.Medieval.Ships.Anchor;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests;

[TestFixture]
[TestOf(typeof(ShipGridSystem))]
public sealed class ShipGridAnchorTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ShipGridAnchorTestAnchor
  components:
  - type: Appearance
  - type: MedievalAnchor
";

    [Test]
    public async Task RequiresEveryAnchorToBeRaised()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entityManager = server.EntMan;
        var shipGridSystem = entityManager.System<ShipGridSystem>();

        await server.WaitAssertion(() =>
        {
            var firstUid = entityManager.SpawnEntity(
                "ShipGridAnchorTestAnchor",
                new EntityCoordinates(map.Grid, 0f, 0f));
            var secondUid = entityManager.SpawnEntity(
                "ShipGridAnchorTestAnchor",
                new EntityCoordinates(map.Grid, 0f, 0f));
            var first = entityManager.GetComponent<MedievalAnchorComponent>(firstUid);
            var second = entityManager.GetComponent<MedievalAnchorComponent>(secondUid);

            first.Lowered = true;
            shipGridSystem.NotifyAnchorChanged(firstUid, first);
            second.Lowered = true;
            shipGridSystem.NotifyAnchorChanged(secondUid, second);

            var grid = entityManager.GetComponent<ShipGridComponent>(map.Grid);
            Assert.That(grid.HasLoweredAnchor, Is.True);

            first.Lowered = false;
            shipGridSystem.NotifyAnchorChanged(firstUid, first);
            Assert.That(grid.HasLoweredAnchor, Is.True,
                "Raising one anchor must not release a ship while another anchor is lowered.");

            second.Lowered = false;
            shipGridSystem.NotifyAnchorChanged(secondUid, second);
            Assert.That(grid.HasLoweredAnchor, Is.False);
        });

        await pair.CleanReturnAsync();
    }
}
