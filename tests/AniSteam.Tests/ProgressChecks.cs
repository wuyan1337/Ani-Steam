using AniSteam;

internal static class ProgressChecks
{
    public static void Run(Action<bool, string> check)
    {
        var watching = new Watching(1, 2, "测试番剧", "3", "", 60000, 1440000, false, false, false);
        Snapshot Wrap(Watching value) => new(1, 42, true, null, value);
        var feed = new PlaybackFeed();
        feed.Accept(Wrap(watching), 0);
        check(feed.Current(1000)?.PositionMillis == 61000, "monotonic interpolation advances one second");
        feed.Accept(Wrap(watching), 1250);
        feed.Accept(Wrap(watching), 1750);
        feed.Accept(Wrap(watching), 2500);
        check(feed.Current(3000)?.PositionMillis == 62000, "duplicate heartbeats neither reset clock nor extrapolate forever");
        feed.Accept(Wrap(watching with { Paused = true, PositionMillis = 61500 }), 3100);
        check(feed.Current(4500)?.PositionMillis == 61500, "pause stops interpolation");
        feed.Accept(Wrap(watching with { Buffering = true, PositionMillis = 61500 }), 4600);
        check(feed.Current(6000)?.PositionMillis == 61500, "buffering stops interpolation");
        feed.Accept(Wrap(watching with { PositionMillis = 300000 }), 6100);
        check(feed.Current(6100)?.PositionMillis == 300000, "forward seek snaps to source");
        feed.Accept(Wrap(watching with { PositionMillis = 15000 }), 6200);
        check(feed.Current(6200)?.PositionMillis == 15000, "backward seek snaps to source");
        feed.Accept(Wrap(watching with { PositionMillis = 10000, PlaybackRate = 2 }), 6300);
        check(feed.Current(7300)?.PositionMillis == 12000, "playback speed is respected");
        feed.Accept(Wrap(watching with { EpisodeId = 4, PositionMillis = 0 }), 7400);
        check(feed.Current(7400)?.PositionMillis == 0, "episode change resets progress clock");
        feed.Accept(Wrap(watching with { PositionMillis = 1439500 }), 7500);
        check(feed.Current(8500)?.PositionMillis == 1440000, "interpolation never exceeds duration");
        check(feed.Current(16000) is null, "expired transport stops and clears progress");

        var smooth = new PlaybackFeed();
        var publisher = new PresencePublisher();
        publisher.Reset();
        var secondsSent = new List<long>();
        int report = 0;
        for (long now = 0; now <= 30000; now += 100) {
            long arrivingAt = report * 1000 + report % 3 * 70;
            if (now >= arrivingAt) {
                smooth.Accept(Wrap(watching with { PositionMillis = 60000 + report * 1000, PositionAtMillis = 100000 + report * 1000 }), now);
                report++;
            }
            var current = smooth.Current(now);
            if (publisher.Next(smooth.DesiredName(new(), now), now, false) is { Length: > 0 })
                secondsSent.Add(current!.PositionMillis / 1000);
        }
        check(secondsSent.SequenceEqual(Enumerable.Range(60, 31).Select(x => (long)x)), "sampler jitter produces consecutive Steam seconds, without 5s skips");
    }
}
