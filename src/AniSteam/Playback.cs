using System.Text;
using System.Text.Json;

namespace AniSteam;

public sealed record Watching(int SubjectId, int EpisodeId, string SubjectName, string EpisodeSort,
    string EpisodeName, long PositionMillis, long DurationMillis, bool Paused, bool Buffering, bool Loading,
    float PlaybackRate = 1, long PositionAtMillis = 0);

public sealed record Snapshot(int Schema, int SourcePid, bool HookReady, string? Error, Watching? Watching)
{
    public static Snapshot Parse(string line, int expectedPid)
    {
        if (line.Length > 32768) throw new InvalidDataException("播放数据过长");
        var result = JsonSerializer.Deserialize<Snapshot>(line, JsonOptions) ?? throw new InvalidDataException("空播放数据");
        if (result.Schema != 1 || result.SourcePid != expectedPid) throw new InvalidDataException("播放数据来源不匹配");
        return result;
    }

    public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
}

public enum DisplayPriority { Episode = 0, Progress = 2 }

public sealed record Settings(string Prefix = "在看 ", bool ShowProgress = true, bool ShowPaused = true,
    DisplayPriority Priority = DisplayPriority.Episode)
{
    public Settings Normalize() => this with {
        Prefix = StatusFormatter.Clean(Prefix),
        Priority = Enum.IsDefined(Priority) ? Priority : DisplayPriority.Episode
    };
}

public static class StatusFormatter
{
    public const int MaxBytes = 63;

    public static string Format(Watching? watching, Settings settings)
    {
        if (watching is null || watching.Loading || string.IsNullOrWhiteSpace(watching.SubjectName)) return "";
        if (watching.Paused && !settings.ShowPaused) return "";
        settings = settings.Normalize();
        string title = Clean(watching.SubjectName).Trim();
        if (title.Length == 0) return "";
        string episode = Clean(watching.EpisodeSort).Trim();
        episode = episode.Length == 0 ? "" : " 第" + episode + "集";
        string state = watching.Paused ? " [暂停]" : watching.Buffering ? " [缓冲]" : "";
        string progress = state.Length == 0 && settings.ShowProgress && watching.DurationMillis > 0
            ? $" {Time(Math.Clamp(watching.PositionMillis, 0, watching.DurationMillis))}/{Time(watching.DurationMillis)}" : "";
        string prefix = settings.Prefix;
        string full = prefix + title + episode + progress + state;
        if (ByteCount(full) <= MaxBytes) return full;

        int titleMinimum = title.EnumerateRunes().First().Utf8SequenceLength;
        int remaining = MaxBytes - ByteCount(state) - titleMinimum;
        string keptEpisode = "", keptProgress = "";
        string TakeWhole(string field)
        {
            int size = ByteCount(field);
            if (size > remaining) return "";
            remaining -= size;
            return field;
        }
        if (settings.Priority == DisplayPriority.Episode) keptEpisode = TakeWhole(episode);
        else if (settings.Priority == DisplayPriority.Progress) keptProgress = TakeWhole(progress);
        string keptPrefix = Truncate(prefix, Math.Min(12, remaining));
        remaining -= ByteCount(keptPrefix);
        string keptTitle = Truncate(title, titleMinimum + remaining);
        remaining += titleMinimum - ByteCount(keptTitle);
        if (keptEpisode.Length == 0) keptEpisode = TakeWhole(episode);
        if (keptProgress.Length == 0) keptProgress = TakeWhole(progress);
        return keptPrefix + keptTitle + keptEpisode + keptProgress + state;
    }

    public static string Truncate(string text, int bytes)
    {
        var result = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Utf8SequenceLength > bytes) break;
            result.Append(rune.ToString());
            bytes -= rune.Utf8SequenceLength;
        }
        return result.ToString();
    }

    public static int ByteCount(string text) => Encoding.UTF8.GetByteCount(text);
    public static string Clean(string? text) => new((text ?? "").Where(c => !char.IsControl(c)).ToArray());
    private static string Time(long millis)
    {
        long seconds = Math.Clamp(millis / 1000, 0, 359999);
        return seconds >= 3600 ? $"{seconds / 3600}:{seconds / 60 % 60:00}:{seconds % 60:00}" : $"{seconds / 60}:{seconds % 60:00}";
    }
}

public sealed class PlaybackFeed
{
    public Snapshot? Latest { get; private set; }
    private long lastReceived = long.MinValue;
    private long anchorAt;
    private long sourceChangedAt;
    private double anchorPosition;
    public bool Ended { get; private set; }
    public void Accept(Snapshot snapshot, long now)
    {
        var previous = Latest?.Watching;
        var incoming = snapshot.Watching;
        if (incoming is not null) {
            long predicted = Current(now)?.PositionMillis ?? incoming.PositionMillis;
            bool reset = previous is null || previous.SubjectId != incoming.SubjectId || previous.EpisodeId != incoming.EpisodeId
                || previous.Paused != incoming.Paused || previous.Buffering != incoming.Buffering || previous.Loading != incoming.Loading
                || previous.PlaybackRate != incoming.PlaybackRate || incoming.PositionMillis < previous.PositionMillis
                || !IsFresh(now) || Latest?.Error is not null
                || (previous != incoming && Math.Abs((double)predicted - incoming.PositionMillis) > 1500);
            if (reset) { anchorPosition = Math.Max(0, incoming.PositionMillis); anchorAt = now; }
            if (previous != incoming || reset) sourceChangedAt = now;
        }
        Latest = snapshot;
        lastReceived = now;
        Ended = false;
    }
    public void End() { Ended = true; Latest = null; }
    public bool IsFresh(long now) => !Ended && lastReceived != long.MinValue && now - lastReceived < 8000;
    public Watching? Current(long now)
    {
        if (!IsFresh(now) || Latest?.Error is not null) return null;
        var current = Latest?.Watching;
        if (current is null || current.Paused || current.Buffering || current.Loading || current.DurationMillis <= 0
            || !float.IsFinite(current.PlaybackRate) || current.PlaybackRate <= 0 || current.PlaybackRate > 16) return current;
        long elapsed = Math.Max(0, Math.Min(now, sourceChangedAt + 2000) - anchorAt);
        double position = Math.Clamp(anchorPosition + elapsed * (double)current.PlaybackRate, 0, current.DurationMillis);
        return current with { PositionMillis = (long)position };
    }
    public string DesiredName(Settings settings, long now) => StatusFormatter.Format(Current(now), settings);
}
