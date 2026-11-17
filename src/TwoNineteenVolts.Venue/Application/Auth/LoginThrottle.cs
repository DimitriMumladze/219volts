namespace TwoNineteenVolts.Venue.Application.Auth;

public class LoginThrottle(TimeProvider time)
{
    private const int FreeAttempts = 4;
    private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(30);

    private readonly Lock _lock = new();
    private readonly Dictionary<int, (int Count, DateTimeOffset LastAt)> _attempts = [];

    public bool TryStartAttempt(int userId, out TimeSpan retryAfter)
    {
        lock (_lock)
        {
            var now = time.GetUtcNow();
            var (count, lastAt) = _attempts.GetValueOrDefault(userId);

            var wait = WaitAfter(count);
            var remaining = lastAt + wait - now;
            if (remaining > TimeSpan.Zero)
            {
                retryAfter = remaining;
                return false;
            }

            _attempts[userId] = (count + 1, now);
            retryAfter = TimeSpan.Zero;
            return true;
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
