using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using TwoNineteenVolts.Venue.Endpoints.Live;

namespace TwoNineteenVolts.Venue.Tests.Live;

public sealed class HubClient : IAsyncDisposable
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    private readonly HubConnection _connection;
    private readonly Channel<int> _spotChanges = Channel.CreateUnbounded<int>();
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static async Task<HubClient> ConnectAsync(VenueAppFactory factory, string? cookie)
    {
        var client = new HubClient(factory, cookie);
        await client._connection.StartAsync();
        return client;
    }

    public static string CookieOf(HttpResponseMessage loginResponse)
    {
        var setCookie = loginResponse.Headers.GetValues("Set-Cookie").First();
        return setCookie.Split(';')[0];
    }

    public async Task<int> NextSpotChangeAsync()
    {
        var reading = _spotChanges.Reader.ReadAsync();
        var readingTask = reading.AsTask();
        return await readingTask.WaitAsync(Wait);
    }

    public List<int> SpotChangesSoFar()
    {
        var changes = new List<int>();
        while (_spotChanges.Reader.TryRead(out var spotId))
        {
            changes.Add(spotId);
        }

        return changes;
    }

    public Task WaitUntilClosedAsync()
    {
        return _closed.Task.WaitAsync(Wait);
    }

    public async Task<bool> ClosesWithinAsync(TimeSpan wait)
    {
        var delay = Task.Delay(wait);
        var first = await Task.WhenAny(_closed.Task, delay);
        return first == _closed.Task;
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }


    private HubClient(VenueAppFactory factory, string? cookie)
    {
        var url = new Uri(factory.Server.BaseAddress, "hubs/venue");
        _connection = new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                if (cookie is not null)
                {
                    options.Headers["Cookie"] = cookie;
                }
            })
            .Build();
        _connection.On<int>(VenueHub.SpotChanged, spotId => _spotChanges.Writer.TryWrite(spotId));
        _connection.Closed += _ =>
        {
            _closed.TrySetResult();
            return Task.CompletedTask;
        };
    }
}
