using System.Text.Json;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Leaderboard.Contracts.Collection.V1;

// Same producer example as the package README; no reference to the service projects.
var message = new GameEventEnvelope
{
    EnvelopeVersion = 1,
    MessageId = Guid.NewGuid().ToString("D"),
    TransactionId = Guid.NewGuid().ToString("D"),
    ProducerSquad = ProducerSquad.Player,
    EventType = "player.hero-created",
    EventVersion = 1,
    OccurredAt = Timestamp.FromDateTime(DateTime.UtcNow),
    HeroCreated = new HeroCreated
    {
        Actor = new Actor
        {
            PlayerId = "00000000-0000-4000-8000-000000000001",
            HeroId = "00000000-0000-4000-8000-000000000002",
        },
        HeroClassId = "00000000-0000-4000-8000-000000000080",
    },
};
var errors = GameEventContract.Validate(message);
if (errors.Count != 0)
    throw new InvalidOperationException(string.Join("; ", errors));
byte[] body = message.ToByteArray();
if (!GameEventEnvelope.Parser.ParseFrom(body).Equals(message))
    throw new InvalidOperationException("Producer example did not survive Protobuf round-trip.");

var fixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures");
using var manifest = JsonDocument.Parse(
    File.ReadAllText(Path.Combine(fixtureDirectory, "manifest.json"))
);
var coveredRoutes = new HashSet<string>(StringComparer.Ordinal);
var count = 0;
foreach (var fixture in manifest.RootElement.GetProperty("fixtures").EnumerateArray())
{
    count++;
    var expected = fixture.GetProperty("expected").GetString();
    if (expected == "unreadable")
    {
        try
        {
            GameEventEnvelope.Parser.ParseFrom(
                Convert.FromBase64String(fixture.GetProperty("bodyBase64").GetString()!)
            );
        }
        catch (InvalidProtocolBufferException)
        {
            continue;
        }
        throw new InvalidOperationException("Unreadable fixture was accepted.");
    }

    var original = GameEventEnvelope.Parser.ParseJson(
        File.ReadAllText(Path.Combine(fixtureDirectory, fixture.GetProperty("file").GetString()!))
    );
    var parsed = GameEventEnvelope.Parser.ParseFrom(original.ToByteArray());
    if (
        !original.Equals(parsed)
        || (GameEventContract.Validate(parsed).Count == 0) != (expected == "valid")
    )
        throw new InvalidOperationException(
            $"Unexpected result for {fixture.GetProperty("id").GetString()}."
        );
    if (expected == "valid")
    {
        var route = GameEventContract
            .Events.Single(x => x.EventType == parsed.EventType)
            .RoutingKey;
        if (route != fixture.GetProperty("routingKey").GetString())
            throw new InvalidOperationException("Incorrect routing key.");
        coveredRoutes.Add(route);
    }
}
if (
    coveredRoutes.Count != 16
    || !coveredRoutes.SetEquals(GameEventContract.Events.Select(x => x.RoutingKey))
)
    throw new InvalidOperationException("The package must expose all 16 ingestion routes.");
Console.WriteLine(
    $"PASS: standalone package, producer example, {count} fixtures and {coveredRoutes.Count} routes."
);
