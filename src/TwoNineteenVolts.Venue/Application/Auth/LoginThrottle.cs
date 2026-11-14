namespace TwoNineteenVolts.Venue.Application.Auth;

public class LoginThrottle(TimeProvider time)
{
    private const int FreeAttempts = 4;
    private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(30);

    private readonly Lock _lock = new();
    private readonly Dictionary<int, (int Count, DateTimeOffset LastAt)> _attempts = [];

    public TimeSpan? TryStartAttempt(int userId)
    {
        lock (_lock)
        {
            var now = time.GetUtcNow();
            var (count, lastAt) = _attempts.GetValueOrDefault(userId);

            var remaining = lastAt + WaitAfter(count) - now;
            if (remaining > TimeSpan.Zero)
            {
                return remaining;
            }

            _attempts[userId] = (count + 1, now);
            return null;
        }
    }

    public void RecordSuccess(int userId)
    {
        lock (_lock)
        {
            _attempts.Remove(userId);
        }
    }


    private static TimeSpan WaitAfter(int attempts)
    {
        if (attempts < FreeAttempts)
        {
            return TimeSpan.Zero;
        }

        var seconds = Math.Pow(2, attempts - FreeAttempts);
        if (seconds >= MaxWait.TotalSeconds)
        {
            return MaxWait;
        }

        return TimeSpan.FromSeconds(seconds);
    }
}
