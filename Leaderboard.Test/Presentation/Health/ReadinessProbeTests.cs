using System.Net;
using FluentAssertions;
using Leaderboard.Presentation.Health;

namespace Leaderboard.Test.Presentation.Health;

public sealed class ReadinessProbeTests
{
    [Theory]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, false)]
    public async Task IsReadyAsync_ReturnsWhetherTheReadinessEndpointSucceeds(
        HttpStatusCode status,
        bool expected
    )
    {
        using var handler = new StubHandler(status);
        using var client = new HttpClient(handler);

        bool ready = await ReadinessProbe.IsReadyAsync(client);

        ready.Should().Be(expected);
        handler.RequestUri.Should().Be("http://127.0.0.1:8080/health/ready");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IsReadyAsync_ReturnsFalseWhenTheRequestFails(bool timedOut)
    {
        using var handler = new StubHandler(
            timedOut ? new TaskCanceledException() : new HttpRequestException()
        );
        using var client = new HttpClient(handler);

        bool ready = await ReadinessProbe.IsReadyAsync(client);

        ready.Should().BeFalse();
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode? status;
        private readonly Exception? failure;

        public StubHandler(HttpStatusCode status) => this.status = status;

        public StubHandler(Exception failure) => this.failure = failure;

        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            RequestUri = request.RequestUri;
            return failure is null
                ? Task.FromResult(new HttpResponseMessage(status!.Value))
                : Task.FromException<HttpResponseMessage>(failure);
        }
    }
}
