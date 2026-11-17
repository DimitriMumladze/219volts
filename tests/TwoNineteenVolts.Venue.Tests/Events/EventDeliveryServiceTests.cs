using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using TwoNineteenVolts.Venue.Application.Events;
using TwoNineteenVolts.Venue.Infrastructure.Events;

namespace TwoNineteenVolts.Venue.Tests.Events;

public class EventDeliveryServiceTests
{
    private readonly FakeLogger<EventDeliveryService> _logger = new();

    [Fact]
    public async Task Events_reach_every_listener_in_order()
    {
        var first = new RecordingListener();
        var second = new RecordingListener();
        using var services = NewServices(first, second);
        var queue = services.GetRequiredService<IEventQueue>();
        var a = new TestEvent("A");
        var b = new TestEvent("B");

        queue.Publish(a);
        queue.Publish(b);
        await NewWorker(services).RunOnceAsync();

        Assert.Equal([a, b], first.Events);
        Assert.Equal([a, b], second.Events);
    }

    [Fact]
    public async Task Failing_listener_does_not_stop_the_others()
    {
        var failing = new RecordingListener { Throws = true };
        var working = new RecordingListener();
        using var services = NewServices(failing, working);
        var queue = services.GetRequiredService<IEventQueue>();
        var a = new TestEvent("A");

        queue.Publish(a);
        await NewWorker(services).RunOnceAsync();

        var errors = _logger.Collector.GetSnapshot().Where(r => r.Level == LogLevel.Error).ToList();
        Assert.Equal([a], working.Events);
        Assert.Single(errors);
    }

    [Fact]
    public async Task Listener_that_fails_to_build_does_not_stop_the_worker()
    {
        var services = new ServiceCollection();
        services.AddSingleton<EventQueue>();
        services.AddSingleton<IEventQueue>(provider => provider.GetRequiredService<EventQueue>());
        services.AddScoped<IEventListener>(_ => throw new InvalidOperationException("cannot build"));
        using var provider = services.BuildServiceProvider();
        var queue = provider.GetRequiredService<IEventQueue>();
        var a = new TestEvent("A");
        var b = new TestEvent("B");

        queue.Publish(a);
        queue.Publish(b);
        await NewWorker(provider).RunOnceAsync();

        var errors = _logger.Collector.GetSnapshot().Where(r => r.Level == LogLevel.Error).ToList();
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public async Task Publish_never_waits_for_listeners()
    {
        var blocked = new RecordingListener { Gate = new TaskCompletionSource() };
        using var services = NewServices(blocked);
        var queue = services.GetRequiredService<IEventQueue>();
        var a = new TestEvent("A");
        var b = new TestEvent("B");
        queue.Publish(a);
        var delivery = NewWorker(services).RunOnceAsync();

        var publish = Task.Run(() => queue.Publish(b));
        await publish.WaitAsync(TimeSpan.FromSeconds(5));
        var isDeliveryStillBlocked = !delivery.IsCompleted;

        blocked.Gate.SetResult();
        await delivery;
        Assert.True(isDeliveryStillBlocked);
        Assert.Equal([a, b], blocked.Events);
    }


    private static ServiceProvider NewServices(params RecordingListener[] listeners)
    {
        var services = new ServiceCollection();
        services.AddSingleton<EventQueue>();
        services.AddSingleton<IEventQueue>(provider => provider.GetRequiredService<EventQueue>());
        foreach (var listener in listeners)
        {
            services.AddSingleton<IEventListener>(listener);
        }

        return services.BuildServiceProvider();
    }

    private EventDeliveryService NewWorker(ServiceProvider services)
    {
        var queue = services.GetRequiredService<EventQueue>();
        var scopes = services.GetRequiredService<IServiceScopeFactory>();
        return new EventDeliveryService(queue, scopes, _logger);
    }
}
