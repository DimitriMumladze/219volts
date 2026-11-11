using Microsoft.Extensions.Time.Testing;
using TwoNineteenVolts.Venue.Application.Auth;

namespace TwoNineteenVolts.Venue.Tests.Auth;

public class LoginThrottleTests
{
    private const int UserId = 7;

    private readonly FakeTimeProvider _time = new();

    [Fact]
    public void First_four_attempts_need_no_wait()
    {
        var throttle = new LoginThrottle(_time);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            Assert.Null(throttle.TryStartAttempt(UserId));
        }
    }

    [Fact]
    public void Waits_grow_1_2_4_8_16_then_cap_30()
    {
        var throttle = new LoginThrottle(_time);
        StartAttempts(throttle, 4);

        foreach (var seconds in new[] { 1, 2, 4, 8, 16, 30, 30 })
        {
            var wait = TimeSpan.FromSeconds(seconds);
            Assert.Equal(wait, throttle.TryStartAttempt(UserId));

            _time.Advance(wait);
            Assert.Null(throttle.TryStartAttempt(UserId));
        }
    }

    [Fact]
    public void Wait_counts_down_with_time()
    {
        var throttle = new LoginThrottle(_time);
        StartAttempts(throttle, 4);

        _time.Advance(TimeSpan.FromMilliseconds(400));
        Assert.Equal(TimeSpan.FromMilliseconds(600), throttle.TryStartAttempt(UserId));

        _time.Advance(TimeSpan.FromMilliseconds(600));
        Assert.Null(throttle.TryStartAttempt(UserId));
    }

    [Fact]
    public void Early_attempt_is_refused_without_extending_the_wait()
    {
        var throttle = new LoginThrottle(_time);
        StartAttempts(throttle, 4);

        _time.Advance(TimeSpan.FromMilliseconds(500));
        Assert.NotNull(throttle.TryStartAttempt(UserId));

        _time.Advance(TimeSpan.FromMilliseconds(500));
        Assert.Null(throttle.TryStartAttempt(UserId));
    }

    [Fact]
    public void Success_resets()
    {
        var throttle = new LoginThrottle(_time);
        StartAttempts(throttle, 4);

        throttle.RecordSuccess(UserId);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            Assert.Null(throttle.TryStartAttempt(UserId));
        }
    }

    [Fact]
    public void Other_users_are_not_slowed_down()
    {
        var throttle = new LoginThrottle(_time);
        StartAttempts(throttle, 4);

        Assert.NotNull(throttle.TryStartAttempt(UserId));
        Assert.Null(throttle.TryStartAttempt(UserId + 1));
    }

    [Fact]
    public void Parallel_burst_gets_no_more_attempts_than_one_by_one()
    {
        var throttle = new LoginThrottle(_time);
        var allowed = 0;

        Parallel.For(0, 50, _ =>
        {
            if (throttle.TryStartAttempt(UserId) is null)
            {
                Interlocked.Increment(ref allowed);
            }
        });

        Assert.Equal(4, allowed);
        Assert.Equal(TimeSpan.FromSeconds(1), throttle.TryStartAttempt(UserId));
    }

    private static void StartAttempts(LoginThrottle throttle, int count)
    {
        for (var attempt = 1; attempt <= count; attempt++)
        {
            Assert.Null(throttle.TryStartAttempt(UserId));
        }
    }
}
