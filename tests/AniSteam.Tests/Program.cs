using System.Text;
using System.Text.Json;
using AniSteam;
using SteamKit2;

int count = 0;
void Check(bool condition, string message) { count++; if (!condition) throw new Exception(message); }
var normal = new Watching(1, 2, "葬送的芙莉莲", "3", "魔法", 492000, 1440000, false, false, false);
string name = StatusFormatter.Format(normal, new());
Check(name == "在看 葬送的芙莉莲 第3集 8:12/24:00", "normal Chinese status");
Check(StatusFormatter.Format(null, new()) == "", "idle clears");
Check(StatusFormatter.Format(normal with { Loading = true }, new()) == "", "loading clears");
Check(StatusFormatter.Format(normal with { Paused = true }, new()).EndsWith("[暂停]"), "pause shown");
Check(StatusFormatter.Format(normal with { Paused = true }, new(ShowPaused: false)) == "", "pause can clear");
Check(StatusFormatter.Format(normal with { Buffering = true }, new()).EndsWith("[缓冲]"), "buffer shown");
Check(!StatusFormatter.Format(normal, new(ShowProgress: false)).Contains('/'), "progress optional");
Check(StatusFormatter.Truncate("好😀你", 6) == "好", "UTF-8 truncate emoji boundary");
Check(StatusFormatter.Truncate("好😀你", 7) == "好😀", "UTF-8 preserve complete emoji");
foreach (string title in new[] { "😀" + new string('漫', 100), new string('A', 500), "\0坏\n名", "" })
foreach (string prefix in new[] { "在看 ", new string('P', 80), "😀😀😀😀😀", "" })
foreach (string episode in new[] { "SP1", "12.5", new string('9', 300), "" }) {
    var output = StatusFormatter.Format(normal with { SubjectName = title, EpisodeSort = episode, Paused = true }, new(prefix));
    Check(Encoding.UTF8.GetByteCount(output) <= 63, "Steam byte budget");
    Check(!output.Any(char.IsControl), "no control characters");
    Check(!output.Contains('\uFFFD'), "no broken surrogate");
}
var feed = new PlaybackFeed();
var longTitle = normal with { SubjectName = new string('漫', 30) };
string episodeFirst = StatusFormatter.Format(longTitle, new(Priority: DisplayPriority.Episode));
string progressFirst = StatusFormatter.Format(longTitle, new(Priority: DisplayPriority.Progress));
Check(episodeFirst.Contains("第3集") && !episodeFirst.Contains("8:12"), "episode priority keeps the episode");
Check(progressFirst.Contains("8:12/24:00") && !progressFirst.Contains("第3集"), "progress priority keeps complete timestamps");
foreach (var policy in Enum.GetValues<DisplayPriority>())
foreach (string prefix in new[] { "在看 ", new string('P', 80) })
foreach (string episode in new[] { "3", new string('9', 300) })
foreach (int state in new[] { 0, 1, 2 }) {
    var output = StatusFormatter.Format(longTitle with { SubjectName = "😀" + longTitle.SubjectName, EpisodeSort = episode,
        Paused = state == 1, Buffering = state == 2 }, new(prefix, Priority: policy));
    Check(Encoding.UTF8.GetByteCount(output) <= 63, "fixed UTF-8 budget across priorities");
    Check(output.Contains("😀") && !output.Contains('\uFFFD'), "both priorities always retain a complete title character");
    Check(state != 1 || output.EndsWith("[暂停]"), "pause state is retained under both priorities");
    Check(state != 2 || output.EndsWith("[缓冲]"), "buffering state is retained under both priorities");
}
var migrated = JsonSerializer.Deserialize<Settings>("{\"Prefix\":\"追番 \",\"ShowProgress\":false,\"ShowPaused\":true}")!;
Check(migrated.Priority == DisplayPriority.Episode && !migrated.ShowProgress, "0.1 settings migrate with defaults");
var oldTitle = JsonSerializer.Deserialize<Settings>("{\"ByteLimit\":16,\"Priority\":1}")!.Normalize();
Check(oldTitle.Priority == DisplayPriority.Episode && StatusFormatter.Format(normal, oldTitle) == name,
    "legacy title priority falls back to episode and custom byte limit is ignored");
var oldProgress = JsonSerializer.Deserialize<Settings>("{\"Prefix\":\"追番 \",\"ByteLimit\":42,\"Priority\":2}")!.Normalize();
Check(oldProgress.Priority == DisplayPriority.Progress && oldProgress.Prefix == "追番 ", "legacy progress priority and prefix remain intact");
Check(!JsonSerializer.Serialize(oldProgress).Contains("ByteLimit"), "saved settings no longer expose a byte limit");
Check(new Settings(Priority: (DisplayPriority)99).Normalize().Priority == DisplayPriority.Episode, "invalid priority uses the default");
var snapshot = new Snapshot(1, 42, true, null, normal);
Check(feed.DesiredName(new(), 0) == "", "no initial data");
feed.Accept(snapshot, 1000);
Check(feed.DesiredName(new(), 8999) != "", "heartbeat still fresh");
Check(feed.DesiredName(new(), 9000) == "", "heartbeat expiry clears");
feed.Accept(snapshot with { Error = "unsupported" }, 9500);
Check(feed.DesiredName(new(), 9600) == "", "hook failure clears");
feed.Accept(snapshot, 9700);
feed.End();
Check(feed.DesiredName(new(), 9800) == "", "EOF clears");
var publisher = new PresencePublisher();
publisher.Reset();
Check(publisher.Next(name, 100, false) == name, "initial publish");
Check(publisher.Next(name, 200, false) is null, "duplicate suppressed");
Check(publisher.Next(name + "2", 201, false) is null, "progress throttled");
Check(publisher.Next("", 202, false) == "", "clear immediate");
Check(publisher.Next(name, 203, false) == name, "resume immediate");
Check(publisher.Next(name, 204, true) == "", "other game clears this session");
Check(publisher.Next(name, 205, true) is null, "other game not overridden");
Check(publisher.Next(name, 206, false) == name, "restore after other game");
publisher.Reset();
Check(publisher.Next(name, 207, false) == name, "reconnect resends unchanged title");
var message = PresencePublisher.CreateMessage(name);
Check(message.Body.games_played.Count == 1, "one shortcut");
Check(new GameID(message.Body.games_played[0].game_id).AppType == GameID.GameType.Shortcut, "non-Steam game type");
Check(message.Body.games_played[0].game_extra_info == name, "exact Steam game name");
Check(PresencePublisher.CreateMessage("").Body.games_played.Count == 0, "empty GamesPlayed clears Steam");
var outgoing = new PresencePublisher();
bool sendCompleted = false;
var previewEvents = new List<string>();
outgoing.PublishedNameChanged += value => {
    Check(sendCompleted || value == "", "published preview never precedes transport submission");
    previewEvents.Add(value);
};
outgoing.Publish(name, 1000, false, request => {
    Check(request.Body.games_played.Single().game_extra_info == name, "published name comes from the exact outgoing Steam payload");
    Check(outgoing.PublishedName == "", "pending send is not displayed");
    sendCompleted = true;
});
Check(outgoing.PublishedName == name && previewEvents.SequenceEqual([name]), "successful submission updates the preview once");
outgoing.Publish(name + "2", 1100, false, _ => throw new Exception("throttled message reached transport"));
Check(outgoing.PublishedName == name && previewEvents.Count == 1, "throttled draft does not update the published preview");
try {
    outgoing.Publish(name + "2", 1600, false, _ => throw new IOException("simulated send failure"));
    throw new Exception("send failure was swallowed");
} catch (IOException) { count++; }
Check(outgoing.PublishedName == name && previewEvents.Count == 1, "failed send never appears as a successful publication");
outgoing.Reset();
Check(outgoing.PublishedName == "" && previewEvents.Last() == "", "connection reset clears the previously published preview");
string json = JsonSerializer.Serialize(snapshot);
Check(Snapshot.Parse(json, 42).Watching == normal, "protocol roundtrip");
try { Snapshot.Parse(json, 43); throw new Exception("wrong PID accepted"); } catch (InvalidDataException) { count++; }
try { Snapshot.Parse(json.Replace("\"Schema\":1", "\"Schema\":2"), 42); throw new Exception("wrong schema accepted"); } catch (InvalidDataException) { count++; }
ProgressChecks.Run(Check);
UiChecks.Run(Check, args.Length == 2 && args[0] == "--ui-snapshot" ? args[1] : null);
if (args.Length == 1) {
    var lines = File.ReadAllLines(args[0]).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
    var snapshots = lines.Select(line => Snapshot.Parse(line, JsonDocument.Parse(line).RootElement.GetProperty("sourcePid").GetInt32())).ToArray();
    Check(snapshots.Any(s => s.HookReady), "real release hook installed");
    Check(snapshots.Any(s => s.Watching?.SubjectName.Contains("😀") == true), "real JVM Unicode protocol");
    Check(snapshots.Any(s => s.Watching?.Paused == true), "real pause");
    Check(snapshots.Any(s => s.Watching?.Buffering == true), "real buffering");
    Check(snapshots.Any(s => s.Watching?.Loading == true), "real loading");
    Check(snapshots.Any(s => s.Watching?.EpisodeSort == "4"), "real episode switch");
    Check(snapshots.Any(s => s.Watching?.PlaybackRate == 2), "real JVM playback speed");
    Check(snapshots.Any(s => s.Watching?.PositionAtMillis > 0), "real JVM report timestamp");
    Check(snapshots.Last().Watching is null, "real exit clear");
    Check(snapshots.All(s => s.Error is null), "no observer errors");
    foreach (var s in snapshots) Check(Encoding.UTF8.GetByteCount(StatusFormatter.Format(s.Watching, new())) <= 63, "real status budget");
}
Console.WriteLine($"PASS: {count} assertions");
