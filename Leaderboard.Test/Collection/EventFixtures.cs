using System.Text.Json;
using Leaderboard.Contracts.Collection.V1;

namespace Leaderboard.Test.Collection;

internal static class EventFixtures
{
    private static readonly string DirectoryPath = Path.Combine(
        AppContext.BaseDirectory,
        "CollectionFixtures"
    );

    public static GameEventEnvelope Load(string file) =>
        GameEventEnvelope.Parser.ParseJson(File.ReadAllText(Path.Combine(DirectoryPath, file)));

    public static IEnumerable<GameEventEnvelope> Catalogue()
    {
        using var manifest = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(DirectoryPath, "manifest.json"))
        );
        foreach (var definition in GameEventContract.Events)
        {
            var fixture = manifest
                .RootElement.GetProperty("fixtures")
                .EnumerateArray()
                .First(x =>
                    x.GetProperty("expected").GetString() == "valid"
                    && x.GetProperty("routingKey").GetString() == definition.RoutingKey
                );
            yield return Load(fixture.GetProperty("file").GetString()!);
        }
    }
}
