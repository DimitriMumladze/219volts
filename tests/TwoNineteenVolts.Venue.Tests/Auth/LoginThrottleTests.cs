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
            var isAllowed = throttle.TryStartAttempt(UserId, out _);
            Assert.True(isAllowed);
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
            var isAllowedTooEarly = throttle.TryStartAttempt(UserId, out var retryAfter);
            Assert.False(isAllowedTooEarly);
            Assert.Equal(wait, retryAfter);

            _time.Advance(wait);
            var isAllowedOnTime = throttle.TryStartAttempt(UserId, out _);
            Assert.True(isAllowedOnTime);
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
        var isAllowedEarly = throttle.TryStartAttempt(UserId, out var remaining);
        Assert.False(isAllowedEarly);
        Assert.Equal(sixHundredMilliseconds, remaining);

        _time.Advance(sixHundredMilliseconds);
        var isAllowedAfterWaiting = throttle.TryStartAttempt(UserId, out _);
        Assert.True(isAllowedAfterWaiting);
    }

    [Fact]
    public void Early_attempt_is_refused_without_extending_the_wait()
    {
        var throttle = new LoginThrottle(_time);
        StartAttempts(throttle, 4);

        var halfSecond = TimeSpan.FromMilliseconds(500);

        _time.Advance(halfSecond);
        var isAllowedEarly = throttle.TryStartAttempt(UserId, out _);
        Assert.False(isAllowedEarly);

        _time.Advance(halfSecond);
        var isAllowedOnTime = throttle.TryStartAttempt(UserId, out _);
        Assert.True(isAllowedOnTime);
    }

    [Fact]
    public void Success_resets()
    {
        var throttle = new LoginThrottle(_time);
        StartAttempts(throttle, 4);

        throttle.RecordSuccess(UserId);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var isAllowed = throttle.TryStartAttempt(UserId, out _);
            Assert.True(isAllowed);
        }
    }

    [Fact]
    public void Other_users_are_not_slowed_down()
    {
        var throttle = new LoginThrottle(_time);
        StartAttempts(throttle, 4);

        var isSlowedUserAllowed = throttle.TryStartAttempt(UserId, out _);
        var isOtherUserAllowed = throttle.TryStartAttempt(UserId + 1, out _);
        Assert.False(isSlowedUserAllowed);
        Assert.True(isOtherUserAllowed);
    }

    [Fact]
    public void Parallel_burst_gets_no_more_attempts_than_one_by_one()
    {
        var throttle = new LoginThrottle(_time);
        var allowedCount = 0;

        Parallel.For(0, 50, attempt =>
        {
            if (throttle.TryStartAttempt(UserId, out _))
            {
                Interlocked.Increment(ref allowedCount);
            }
        });

        var oneSecond = TimeSpan.FromSeconds(1);
        var isNextAllowed = throttle.TryStartAttempt(UserId, out var nextWait);
        Assert.Equal(4, allowedCount);
        Assert.False(isNextAllowed);
        Assert.Equal(oneSecond, nextWait);
    }


    private static void StartAttempts(LoginThrottle throttle, int count)
    {
        for (var attempt = 1; attempt <= count; attempt++)
        {
            var isAllowed = throttle.TryStartAttempt(UserId, out _);
            Assert.True(isAllowed);
        }
    }
}
