using System.Collections.Concurrent;

namespace ForumApi.Services;

/// <summary>进程内滑动窗口限流（单机足够；多实例需换 Redis）。</summary>
public class RateLimitService
{
    private readonly ConcurrentDictionary<string, Queue<long>> _buckets = new();

    /// <summary>若未超限返回 true 并记一次；超限返回 false。</summary>
    public bool TryAcquire(string key, int maxCount, TimeSpan window)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var cutoff = now - (long)window.TotalMilliseconds;
        var q = _buckets.GetOrAdd(key, _ => new Queue<long>());
        lock (q)
        {
            while (q.Count > 0 && q.Peek() < cutoff)
                q.Dequeue();
            if (q.Count >= maxCount)
                return false;
            q.Enqueue(now);
            return true;
        }
    }

    public string? CheckOrNull(string key, int maxCount, TimeSpan window, string message)
        => TryAcquire(key, maxCount, window) ? null : message;
}
