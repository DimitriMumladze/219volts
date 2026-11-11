namespace TwoNineteenVolts.Venue.Application.Auth;

// Slows down PIN guessing per user. An attempt is counted when it starts, inside the same lock
// as the check, so a burst of parallel requests gets no more tries than requests sent one by one.
public class LoginThrottle(TimeProvider time)
{
    private const int FreeAttempts = 4;
    private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(30);

    private readonly Lock _lock = new();
    private readonly Dictionary<int, (int Count, DateTimeOffset LastAt)> _attempts = [];

    // Returns null when the attempt may go ahead (and counts it), or how long to wait before the next one.
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

    // Attempts 1-4 follow each other freely; after that the waits are 1, 2, 4, 8, 16, then 30 seconds.
    private static TimeSpan WaitAfter(int attempts)
    {
        if (attempts < FreeAttempts)
        {
            return TimeSpan.Zero;
        }

        var seconds = Math.Pow(2, attempts - FreeAttempts);
        return seconds >= MaxWait.TotalSeconds ? MaxWait : TimeSpan.FromSeconds(seconds);
    }
}
