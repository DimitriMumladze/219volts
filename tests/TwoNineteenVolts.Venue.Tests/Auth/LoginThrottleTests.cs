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
            var wait = throttle.TryStartAttempt(UserId);
            Assert.Null(wait);
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
            var tooEarly = throttle.TryStartAttempt(UserId);
            Assert.Equal(wait, tooEarly);

            _time.Advance(wait);
            var onTime = throttle.TryStartAttempt(UserId);
            Assert.Null(onTime);
        }
    }

    [Fact]
    public void Wait_counts_down_with_time()
    {
        var throttle = new LoginThrottle(_time);
        StartAttempts(throttle, 4);

        var fourHundredMilliseconds = TimeSpan.FromMilliseconds(400);
        var sixHundredMilliseconds = TimeSpan.FromMilliseconds(600);

        _time.Advance(fourHundredMilliseconds);
        var remaining = throttle.TryStartAttempt(UserId);
        Assert.Equal(sixHundredMilliseconds, remaining);

        _time.Advance(sixHundredMilliseconds);
        var afterWaiting = throttle.TryStartAttempt(UserId);
        Assert.Null(afterWaiting);
    }

    [Fact]
    public void Early_attempt_is_refused_without_extending_the_wait()
    {
        var throttle = new LoginThrottle(_time);
        StartAttempts(throttle, 4);

        var halfSecond = TimeSpan.FromMilliseconds(500);

        _time.Advance(halfSecond);
        var early = throttle.TryStartAttempt(UserId);
        Assert.NotNull(early);

        _time.Advance(halfSecond);
        var onTime = throttle.TryStartAttempt(UserId);
        Assert.Null(onTime);
    }

    [Fact]
    public void Success_resets()
    {
        var throttle = new LoginThrottle(_time);
        StartAttempts(throttle, 4);

        throttle.RecordSuccess(UserId);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var wait = throttle.TryStartAttempt(UserId);
            Assert.Null(wait);
        }
    }

    [Fact]
    public void Other_users_are_not_slowed_down()
    {
        var throttle = new LoginThrottle(_time);
        StartAttempts(throttle, 4);

        var slowedUser = throttle.TryStartAttempt(UserId);
        var otherUser = throttle.TryStartAttempt(UserId + 1);
        Assert.NotNull(slowedUser);
        Assert.Null(otherUser);
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

        var oneSecond = TimeSpan.FromSeconds(1);
        var nextWait = throttle.TryStartAttempt(UserId);
        Assert.Equal(4, allowed);
        Assert.Equal(oneSecond, nextWait);
    }


    private static void StartAttempts(LoginThrottle throttle, int count)
    {
        for (var attempt = 1; attempt <= count; attempt++)
        {
            var wait = throttle.TryStartAttempt(UserId);
            Assert.Null(wait);
        }
    }
}
