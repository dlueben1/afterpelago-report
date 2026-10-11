using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Afterpelago.Api;
using Afterpelago.Data;
using Afterpelago.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Afterpelago.Services;

/// <summary>Imports, publishes, deletes, and repairs persisted Archipelago sessions.</summary>
public sealed partial class SessionService(
    AfterpelagoDbContext db, DataPaths paths, TimeProvider time, ILogger<SessionService> logger, GameCatalogService gameCatalog)
{
    public const long MaximumFileLength = 25L * 1024 * 1024;
    private const int WarningLimit = 100;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>Imports a WebWorld text log and creates a draft session.</summary>
    public async Task<SessionImportResponse> ImportWebLogAsync(Guid requestId, string name, IFormFile file, CancellationToken ct)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 200) throw new SessionInputException("Session name must contain between 1 and 200 characters.");
        if (!string.Equals(Path.GetExtension(file.FileName), ".txt", StringComparison.OrdinalIgnoreCase)) throw new SessionInputException("The WebLog must be a .txt file.");
        if (file.Length is <= 0 or > MaximumFileLength) throw new SessionInputException("The WebLog must be nonempty and no larger than 25 MiB.");

        paths.EnsureCreated();
        var temporaryPath = Path.Combine(paths.ImportsDirectory, $".{requestId:N}.{Guid.NewGuid():N}.tmp");
        string hash;
        await using (var source = file.OpenReadStream())
        await using (var target = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
        {
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), ct);
                hasher.AppendData(buffer, 0, read);
            }
            hash = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
        }

        try
        {
            var prior = await db.SessionImports.AsNoTracking().SingleOrDefaultAsync(i => i.RequestId == requestId, ct);
            if (prior is not null)
            {
                if (prior.Sha256 != hash || !string.Equals((await db.Sessions.Where(s => s.Id == prior.SessionId).Select(s => s.Name).SingleAsync(ct)), name, StringComparison.Ordinal))
                    throw new SessionConflictException("That import request ID was already used with different content.");
                return await GetImportAsync(requestId, ct) ?? throw new InvalidOperationException("The existing import could not be loaded.");
            }

            var watch = Stopwatch.StartNew();
            var discovery = await DiscoverAsync(temporaryPath, ct);
            if (discovery.Slots.Count == 0) throw new SessionInputException("The file contains no recognizable participants or gameplay data.");

            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var session = new Session
            {
                Name = name,
                State = SessionState.Draft,
                CreatedAt = time.GetUtcNow(),
                OriginalStart = discovery.Start,
                OriginalEnd = discovery.End,
            };
            var import = new SessionImport
            {
                Session = session,
                RequestId = requestId,
                ImportedAt = time.GetUtcNow(),
                Method = SessionImportMethod.WebLog,
                OriginalFileName = Path.GetFileName(file.FileName),
                ByteLength = file.Length,
                Sha256 = hash,
            };
            session.Imports.Add(import);
            foreach (var found in discovery.Slots.Values.OrderBy(s => s.Team).ThenBy(s => s.Name, StringComparer.Ordinal))
                session.Slots.Add(new Slot { TeamNumber = found.Team, PlayerName = found.Name, GameName = found.Game });
            db.Sessions.Add(session);
            await db.SaveChangesAsync(ct);

            var permanentPath = GetImportPath(import.Id);
            File.Move(temporaryPath, permanentPath, false);
            try
            {
                await ParseAndPersistAsync(temporaryPath: permanentPath, session, import, discovery.Warnings, ct);
                import.WarningCount = discovery.Warnings.Total;
                import.WarningDetailsJson = JsonSerializer.Serialize(discovery.Warnings.Items);
                await db.SaveChangesAsync(ct);
                await gameCatalog.ObserveSessionAsync(session, ct);
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                TryDeleteFile(permanentPath);
                throw;
            }

            logger.LogInformation("Imported WebLog {ImportId} into draft session {SessionId}: {Slots} slots, {Warnings} warnings in {ElapsedMs} ms.",
                import.Id, session.Id, session.Slots.Count, import.WarningCount, watch.ElapsedMilliseconds);
            db.ChangeTracker.Clear();
            return await GetImportAsync(requestId, ct) ?? throw new InvalidOperationException("The imported session could not be loaded.");
        }
        finally
        {
            TryDeleteFile(temporaryPath);
        }
    }

    /// <summary>Returns review data for a committed import.</summary>
    public async Task<SessionImportResponse?> GetImportAsync(Guid requestId, CancellationToken ct)
    {
        var row = await db.SessionImports.AsNoTracking()
            .Where(i => i.RequestId == requestId && i.Session.State != SessionState.Deleting)
            .Select(i => new
            {
                Import = i, i.Session.Name, i.Session.OriginalStart, i.Session.OriginalEnd,
                Slots = i.Session.Slots.OrderBy(s => s.TeamNumber).ThenBy(s => s.PlayerName).Select(s => new SlotResponse(s.Id, s.TeamNumber, s.PlayerName, s.GameName, s.GoalCompleted)).ToList(),
                Checks = i.Session.Checks.Count, Hints = i.Session.Hints.Count, Events = i.Session.Events.Count,
                Gameplay = i.Session.Connections.Count(c => c.Kind == SlotConnectionKind.Gameplay),
                Trackers = i.Session.Connections.Count(c => c.Kind == SlotConnectionKind.Tracker),
            }).SingleOrDefaultAsync(ct);
        if (row is null) return null;
        return new SessionImportResponse(row.Import.SessionId, row.Import.Id, requestId, row.Name, row.OriginalStart, row.OriginalEnd,
            row.Slots, new(row.Slots.Count, row.Checks, row.Hints, row.Events, row.Gameplay, row.Trackers), row.Import.WarningCount,
            JsonSerializer.Deserialize<List<ImportWarningResponse>>(row.Import.WarningDetailsJson) ?? []);
    }

    /// <summary>Normalizes draft timestamps and makes the session visible to users.</summary>
    public async Task<SessionSummaryResponse?> PublishAsync(int sessionId, int offsetMinutes, string userId, CancellationToken ct)
    {
        if (offsetMinutes is < -840 or > 840) throw new SessionInputException("The source UTC offset must be between -840 and 840 minutes.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var session = await db.Sessions.SingleOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null || session.State == SessionState.Deleting) return null;
        if (session.State == SessionState.Published)
        {
            if (session.SourceUtcOffsetMinutes != offsetMinutes) throw new SessionConflictException("The session was already published with a different UTC offset.");
            await transaction.CommitAsync(ct);
            return await GetSummaryAsync(sessionId, userId, ct);
        }

        var offset = TimeSpan.FromMinutes(offsetMinutes);
        long lastEventId = 0;
        while (true)
        {
            var events = await db.SessionLogEvents.Where(e => e.SessionId == sessionId && e.Id > lastEventId)
                .OrderBy(e => e.Id).Take(500).ToListAsync(ct);
            if (events.Count == 0) break;
            foreach (var logEvent in events) logEvent.TimestampUtc = new DateTimeOffset(logEvent.OriginalTimestamp, offset).ToUniversalTime();
            await db.SaveChangesAsync(ct);
            lastEventId = events[^1].Id;
            foreach (var logEvent in events) db.Entry(logEvent).State = EntityState.Detached;
        }
        session.SourceUtcOffsetMinutes = offsetMinutes;
        session.StartUtc = new DateTimeOffset(session.OriginalStart, offset).ToUniversalTime();
        session.EndUtc = new DateTimeOffset(session.OriginalEnd, offset).ToUniversalTime();
        session.State = SessionState.Published;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await GetSummaryAsync(sessionId, userId, ct);
    }

    /// <summary>Deletes a session and all files imported into it.</summary>
    public async Task<bool> DeleteAsync(int sessionId, CancellationToken ct)
    {
        var session = await db.Sessions.SingleOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null) return true;
        if (session.State != SessionState.Deleting)
        {
            session.State = SessionState.Deleting;
            await db.SaveChangesAsync(ct);
        }
        var importIds = await db.SessionImports.Where(i => i.SessionId == sessionId).Select(i => i.Id).ToListAsync(ct);
        try
        {
            foreach (var importId in importIds) File.Delete(GetImportPath(importId));
            db.Sessions.Remove(session);
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DbUpdateException)
        {
            logger.LogError(ex, "Could not finish deleting session {SessionId}; it remains marked Deleting.", sessionId);
            return false;
        }
    }

    /// <summary>Removes stale staging files and resumes interrupted session deletions.</summary>
    public async Task CleanupAsync(CancellationToken ct)
    {
        paths.EnsureCreated();
        var cutoff = time.GetUtcNow().AddHours(-1).UtcDateTime;
        foreach (var file in Directory.EnumerateFiles(paths.ImportsDirectory, ".*.tmp"))
            if (File.GetLastWriteTimeUtc(file) < cutoff) TryDeleteFile(file);
        var deleting = await db.Sessions.Where(s => s.State == SessionState.Deleting).Select(s => s.Id).ToListAsync(ct);
        foreach (var id in deleting) await DeleteAsync(id, ct);
        var referenced = (await db.SessionImports.Select(i => i.Id).ToListAsync(ct)).Select(GetImportPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(paths.ImportsDirectory, "*.txt"))
            if (!referenced.Contains(file) && File.GetLastWriteTimeUtc(file) < cutoff) TryDeleteFile(file);
    }

    /// <summary>Discovers the roster and time range before importing related events.</summary>
    private async Task<Discovery> DiscoverAsync(string path, CancellationToken ct)
    {
        var result = new Discovery();
        await ReadLinesAsync(path, (line, number) =>
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            if (!TryReadEnvelope(line, out var timestamp, out var message))
            {
                result.Warnings.Add(number, "Line does not have a valid WebLog timestamp.");
                return;
            }
            result.Start = result.Start == default || timestamp < result.Start ? timestamp : result.Start;
            result.End = timestamp > result.End ? timestamp : result.End;
            if (TryMatchJoin(message, out var team, out var player, out var game, out _, out _, out _)) result.Add(team, player, game);
            else if (TryMatchPlayerEvent(message, out team, out player, out _)) result.Add(team, player, null);
        }, ct);
        await ReadLinesAsync(path, (line, lineNumber) =>
        {
            if (!TryReadEnvelope(line, out _, out var message)) return;
            if (TryMatchCheck(message, result.Names, out var team, out var sender, out _, out var receiver, out _)) { result.Add(team, sender, null); result.Add(team, receiver, null); }
            else if (TryMatchCheckLoose(message, out team, out sender, out _, out receiver, out _)) { result.Add(team, sender, null); result.Add(team, receiver, null); }
            else if (TryMatchHint(message, result.Names, out team, out sender, out receiver, out _, out _, out _)) { result.Add(team, sender, null); result.Add(team, receiver, null); }
        }, ct);
        return result;
    }

    /// <summary>Parses the source again and saves its structured facts in bounded batches.</summary>
    private async Task ParseAndPersistAsync(string temporaryPath, Session session, SessionImport import, WarningBag warnings, CancellationToken ct)
    {
        var slots = session.Slots.ToDictionary(s => (s.TeamNumber, s.PlayerName));
        var releasedSlots = new HashSet<int>();
        var pending = 0;
        await ReadLinesAsync(temporaryPath, async (line, number) =>
        {
            if (!TryReadEnvelope(line, out var timestamp, out var message)) return;
            var parsed = ParseMessage(message, slots.Keys.Select(key => (key.TeamNumber, key.PlayerName)).ToArray());
            if (parsed.Category == SessionEventCategory.Unknown && LooksStructured(message))
                warnings.Add(number, "A session event was recognized but could not be parsed reliably.");
            var logEvent = new SessionLogEvent
            {
                SessionId = session.Id, ImportId = import.Id, SourceLineNumber = number,
                OriginalTimestamp = timestamp, Category = parsed.Category, Message = message,
                TeamNumber = parsed.Team, DataJson = parsed.Data is null ? null : JsonSerializer.Serialize(parsed.Data),
            };
            Slot? primary = parsed.Player is null || parsed.Team is null ? null : slots.GetValueOrDefault((parsed.Team.Value, parsed.Player));
            logEvent.SlotId = primary?.Id;
            db.SessionLogEvents.Add(logEvent);

            if (parsed.Check is { } check)
            {
                if (slots.TryGetValue((check.Team, check.Sender), out var sender) && slots.TryGetValue((check.Team, check.Receiver), out var receiver))
                    db.Checks.Add(new Check { SessionId = session.Id, LogEvent = logEvent, SenderSlotId = sender.Id, ReceiverSlotId = receiver.Id, ItemName = check.Item, LocationName = check.Location });
                else warnings.Add(number, "Check participants could not be resolved.");
            }
            if (parsed.Hint is { } hint)
            {
                if (slots.TryGetValue((hint.Team, hint.Sender), out var sender) && slots.TryGetValue((hint.Team, hint.Receiver), out var receiver))
                    db.Hints.Add(new Hint { SessionId = session.Id, LogEvent = logEvent, SenderSlotId = sender.Id, ReceiverSlotId = receiver.Id, ItemName = hint.Item, LocationName = hint.Location, Priority = hint.Priority });
                else warnings.Add(number, "Hint participants could not be resolved.");
            }
            if (parsed.Connection is { } connection && primary is not null)
                db.SlotConnectionEvents.Add(new SlotConnectionEvent { SessionId = session.Id, SlotId = primary.Id, LogEvent = logEvent, Action = connection.Action, Kind = connection.Kind, ClientVersion = connection.Version, TagsJson = JsonSerializer.Serialize(connection.Tags) });
            if (parsed.Category == SessionEventCategory.GoalCompleted && primary is not null) primary.GoalCompleted = true;
            if (parsed.Category == SessionEventCategory.ItemsReleased && primary is not null) releasedSlots.Add(primary.Id);

            if (++pending >= 500)
            {
                await db.SaveChangesAsync(ct);
                foreach (var entry in db.ChangeTracker.Entries().Where(e => e.Entity is SessionLogEvent or Check or Hint or SlotConnectionEvent).ToList()) entry.State = EntityState.Detached;
                pending = 0;
            }
        }, ct);
        session.Status = session.Slots.All(s => s.GoalCompleted || releasedSlots.Contains(s.Id))
            ? SessionStatus.Finished : SessionStatus.Abandoned;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Classifies one message and extracts category-specific fields.</summary>
    private static ParsedEvent ParseMessage(string message, IReadOnlyCollection<(int Team, string Name)> slots)
    {
        if (TryMatchCheck(message, slots, out var team, out var sender, out var item, out var receiver, out var location))
            return new(SessionEventCategory.Check, team, sender, null, new(team, sender, receiver, item, location), null, null);
        if (TryMatchHint(message, slots, out team, out sender, out receiver, out item, out location, out var priority))
            return new(SessionEventCategory.Hint, team, sender, null, null, new(team, sender, receiver, item, location, priority), null);
        if (TryMatchJoin(message, out team, out var player, out var game, out var tracker, out var version, out var tags))
            return new(tracker ? SessionEventCategory.TrackerConnected : SessionEventCategory.GameplayConnected, team, player, new { game }, null, null, new(SlotConnectionAction.Connected, tracker ? SlotConnectionKind.Tracker : SlotConnectionKind.Gameplay, version, tags));
        if (TryMatchDisconnect(message, out team, out player, out tracker, out version, out tags))
            return new(tracker ? SessionEventCategory.TrackerDisconnected : SessionEventCategory.GameplayDisconnected, team, player, null, null, null, new(SlotConnectionAction.Disconnected, tracker ? SlotConnectionKind.Tracker : SlotConnectionKind.Gameplay, version, tags));
        if (TryMatchPlayerEvent(message, out team, out player, out var category)) return new(category, team, player, null, null, null, null);
        if (message == "Shutting down due to inactivity.") return new(SessionEventCategory.ServerShutdown, null, null, null, null, null, null);
        if (message.StartsWith("Hosting game at ", StringComparison.Ordinal)) return new(SessionEventCategory.HostingStarted, null, null, new { host = message[16..] }, null, null, null);
        if (message.StartsWith("Loading embedded data package for game ", StringComparison.Ordinal)) return new(SessionEventCategory.DataPackageLoaded, null, null, new { game = message[39..] }, null, null, null);
        if (message.StartsWith("Loaded save file with ", StringComparison.Ordinal)) return new(SessionEventCategory.SaveLoaded, null, null, null, null, null, null);
        if (message.StartsWith("A client connection was refused", StringComparison.Ordinal)) return new(SessionEventCategory.ConnectionRefused, null, null, null, null, null, null);
        if (PlayerNoticeRegex().IsMatch(message)) return new(SessionEventCategory.PlayerNotice, null, null, null, null, null, null);
        if (ChatRegex().IsMatch(message)) return new(SessionEventCategory.Chat, null, null, null, null, null, null);
        if (message.StartsWith("Notice (all): Team #", StringComparison.Ordinal) && message.Contains("has completed all of their games", StringComparison.Ordinal)) return new(SessionEventCategory.TeamCompleted, null, null, null, null, null, null);
        return new(SessionEventCategory.Unknown, null, null, null, null, null, null);
    }

    /// <summary>Reads the timestamp and message from one WebLog line.</summary>
    private static bool TryReadEnvelope(string line, out DateTime timestamp, out string message)
    {
        var match = EnvelopeRegex().Match(line);
        timestamp = default;
        message = match.Success ? match.Groups["message"].Value : string.Empty;
        return match.Success && DateTime.TryParseExact(match.Groups["timestamp"].Value, "yyyy-MM-dd HH:mm:ss,fff", CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp);
    }

    /// <summary>Reads a gameplay or tracker connection message.</summary>
    private static bool TryMatchJoin(string message, out int team, out string player, out string game, out bool tracker, out string version, out string[] tags)
    {
        var match = JoinRegex().Match(message);
        team = 0; player = game = version = string.Empty; tracker = false; tags = [];
        if (!match.Success) return false;
        team = int.Parse(match.Groups["team"].Value, CultureInfo.InvariantCulture); player = match.Groups["player"].Value;
        game = match.Groups["game"].Value; tracker = match.Groups["verb"].Value == "tracking"; version = match.Groups["version"].Value;
        tags = ParseTags(match.Groups["tags"].Value); return true;
    }

    /// <summary>Reads a gameplay or tracker disconnection message.</summary>
    private static bool TryMatchDisconnect(string message, out int team, out string player, out bool tracker, out string version, out string[] tags)
    {
        var match = DisconnectRegex().Match(message);
        team = 0; player = version = string.Empty; tracker = false; tags = [];
        if (!match.Success) return false;
        team = int.Parse(match.Groups["team"].Value, CultureInfo.InvariantCulture); player = match.Groups["player"].Value;
        tracker = match.Groups["action"].Value == "stopped tracking the game"; version = match.Groups["version"].Value;
        tags = ParseTags(match.Groups["tags"].Value); return true;
    }

    /// <summary>Reads a check by matching sender and receiver against the discovered roster.</summary>
    private static bool TryMatchCheck(string message, IReadOnlyCollection<(int Team, string Name)> slots, out int team, out string sender, out string item, out string receiver, out string location)
    {
        team = 0; sender = item = receiver = location = string.Empty;
        var envelope = CheckEnvelopeRegex().Match(message);
        if (!envelope.Success) return false;
        team = int.Parse(envelope.Groups["team"].Value, CultureInfo.InvariantCulture);
        var body = envelope.Groups["body"].Value;
        var parsedTeam = team;
        var names = slots.Where(slot => slot.Team == parsedTeam).Select(slot => slot.Name).OrderByDescending(name => name.Length).ToArray();
        sender = names.FirstOrDefault(name => body.StartsWith(name + " sent ", StringComparison.Ordinal)) ?? string.Empty;
        if (sender.Length == 0) return false;
        var itemStart = sender.Length + 6;
        foreach (var candidate in names)
        {
            var marker = $" to {candidate} (";
            var markerIndex = body.IndexOf(marker, itemStart, StringComparison.Ordinal);
            if (markerIndex < 0 || !body.EndsWith(')')) continue;
            receiver = candidate;
            item = body[itemStart..markerIndex];
            location = body[(markerIndex + marker.Length)..^1];
            return item.Length > 0 && location.Length > 0;
        }
        return false;
    }

    /// <summary>Extracts participants from a check before a complete roster is available.</summary>
    private static bool TryMatchCheckLoose(string message, out int team, out string sender, out string item, out string receiver, out string location)
    {
        team = 0; sender = item = receiver = location = string.Empty;
        var envelope = CheckEnvelopeRegex().Match(message);
        if (!envelope.Success) return false;
        team = int.Parse(envelope.Groups["team"].Value, CultureInfo.InvariantCulture);
        var body = envelope.Groups["body"].Value;
        var sent = body.IndexOf(" sent ", StringComparison.Ordinal);
        var locationStart = body.IndexOf(" (", sent + 6, StringComparison.Ordinal);
        if (sent <= 0 || locationStart < 0 || !body.EndsWith(')')) return false;
        var recipient = body.LastIndexOf(" to ", locationStart, StringComparison.Ordinal);
        if (recipient <= sent) return false;
        sender = body[..sent]; item = body[(sent + 6)..recipient]; receiver = body[(recipient + 4)..locationStart]; location = body[(locationStart + 2)..^1];
        return sender.Length > 0 && item.Length > 0 && receiver.Length > 0 && location.Length > 0;
    }

    /// <summary>Reads a hint by matching world owners against the discovered roster.</summary>
    private static bool TryMatchHint(string message, IReadOnlyCollection<(int Team, string Name)> slots, out int team, out string sender, out string receiver, out string item, out string location, out string priority)
    {
        team = 0; sender = receiver = item = location = priority = string.Empty;
        var envelope = HintEnvelopeRegex().Match(message);
        if (!envelope.Success) return false;
        team = int.Parse(envelope.Groups["team"].Value, CultureInfo.InvariantCulture);
        var body = envelope.Groups["body"].Value;
        var parsedTeam = team;
        var names = slots.Where(slot => slot.Team == parsedTeam).Select(slot => slot.Name).OrderByDescending(name => name.Length).ToArray();
        receiver = names.FirstOrDefault(name => body.StartsWith(name + "'s ", StringComparison.Ordinal)) ?? string.Empty;
        if (receiver.Length == 0) return false;
        var itemStart = receiver.Length + 3;
        var itemEnd = body.IndexOf(" is at ", itemStart, StringComparison.Ordinal);
        if (itemEnd < 0) return false;
        foreach (var candidate in names)
        {
            var marker = $" in {candidate}'s World";
            var markerIndex = body.LastIndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < itemEnd || !body.EndsWith(')')) continue;
            var priorityStart = body.LastIndexOf(". (", StringComparison.Ordinal);
            if (priorityStart < markerIndex) continue;
            sender = candidate; item = body[itemStart..itemEnd];
            location = body[(itemEnd + 7)..markerIndex];
            priority = body[(priorityStart + 3)..^1];
            return item.Length > 0 && location.Length > 0;
        }
        return false;
    }

    /// <summary>Reads goal, release, collection, and tag-change player events.</summary>
    private static bool TryMatchPlayerEvent(string message, out int team, out string player, out SessionEventCategory category)
    {
        var match = PlayerEventRegex().Match(message); team = 0; player = string.Empty; category = SessionEventCategory.Unknown;
        if (!match.Success) return false;
        team = int.Parse(match.Groups["team"].Value, CultureInfo.InvariantCulture); player = match.Groups["player"].Value;
        category = match.Groups["action"].Value switch { "completed their goal" => SessionEventCategory.GoalCompleted, "released all remaining items from their world" => SessionEventCategory.ItemsReleased, "collected their items from other worlds" => SessionEventCategory.ItemsCollected, _ => SessionEventCategory.TagsChanged };
        return true;
    }

    /// <summary>Converts the WebLog tag representation into plain tag names.</summary>
    private static string[] ParseTags(string value) => TagsRegex().Matches(value).Select(m => m.Groups["tag"].Value).ToArray();
    /// <summary>Detects messages that should generate a warning when parsing fails.</summary>
    private static bool LooksStructured(string message) =>
        (message.StartsWith("(Team #", StringComparison.Ordinal) && message.Contains(" sent ", StringComparison.Ordinal))
        || message.Contains("[Hint]:", StringComparison.Ordinal)
        || (message.StartsWith("Notice (all):", StringComparison.Ordinal)
            && (message.Contains(" has joined.", StringComparison.Ordinal) || message.Contains(" has left the game.", StringComparison.Ordinal)
                || message.Contains(" has completed their goal.", StringComparison.Ordinal)));
    /// <summary>Builds a trusted source-file path from an import primary key.</summary>
    private string GetImportPath(int id) => Path.Combine(paths.ImportsDirectory, $"{id}.txt");
    /// <summary>Removes an attempt-owned file during cleanup without masking the original failure.</summary>
    private static void TryDeleteFile(string path) { try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }

    /// <summary>Loads one published session summary.</summary>
    private async Task<SessionSummaryResponse?> GetSummaryAsync(int id, string userId, CancellationToken ct) =>
        (await SessionOverview.ListAsync(db, userId, id, ct)).SingleOrDefault();

    /// <summary>Streams UTF-8 lines through a synchronous visitor.</summary>
    private static async Task ReadLinesAsync(string path, Action<string, int> visit, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        using var reader = new StreamReader(stream, StrictUtf8, true);
        var number = 0; string? line;
        try { while ((line = await reader.ReadLineAsync(ct)) is not null) { number++; if (line.IndexOf('\0') >= 0) throw new SessionInputException("The uploaded file is not plain UTF-8 text."); visit(line, number); } }
        catch (DecoderFallbackException) { throw new SessionInputException("The uploaded file is not valid UTF-8 text."); }
    }

    /// <summary>Streams UTF-8 lines through an asynchronous visitor.</summary>
    private static async Task ReadLinesAsync(string path, Func<string, int, Task> visit, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        using var reader = new StreamReader(stream, StrictUtf8, true);
        var number = 0; string? line;
        try { while ((line = await reader.ReadLineAsync(ct)) is not null) { number++; if (line.IndexOf('\0') >= 0) throw new SessionInputException("The uploaded file is not plain UTF-8 text."); await visit(line, number); } }
        catch (DecoderFallbackException) { throw new SessionInputException("The uploaded file is not valid UTF-8 text."); }
    }

    private sealed class Discovery
    {
        public Dictionary<(int Team, string Name), FoundSlot> Slots { get; } = [];
        public WarningBag Warnings { get; } = new();
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public IReadOnlyCollection<(int Team, string Name)> Names => Slots.Keys;
        /// <summary>Adds a discovered slot or fills its previously unknown game.</summary>
        public void Add(int team, string name, string? game)
        {
            var key = (team, name);
            if (!Slots.TryGetValue(key, out var slot)) Slots[key] = new(team, name, game);
            else if (slot.Game is null && game is not null) Slots[key] = slot with { Game = game };
        }
    }
    private sealed class WarningBag { public int Total { get; private set; } public List<ImportWarningResponse> Items { get; } = []; public void Add(int line, string reason) { Total++; if (Items.Count < WarningLimit) Items.Add(new(line, reason)); } }
    private sealed record FoundSlot(int Team, string Name, string? Game);
    private sealed record ParsedCheck(int Team, string Sender, string Receiver, string Item, string Location);
    private sealed record ParsedHint(int Team, string Sender, string Receiver, string Item, string Location, string Priority);
    private sealed record ParsedConnection(SlotConnectionAction Action, SlotConnectionKind Kind, string Version, string[] Tags);
    private sealed record ParsedEvent(SessionEventCategory Category, int? Team, string? Player, object? Data, ParsedCheck? Check, ParsedHint? Hint, ParsedConnection? Connection);

    [GeneratedRegex(@"^\[(?<timestamp>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2},\d{3})\]: (?<message>.*)$", RegexOptions.CultureInvariant, 100)] private static partial Regex EnvelopeRegex();
    [GeneratedRegex(@"^Notice \(all\): (?<player>.+?) \(Team #(?<team>\d+)\) (?<verb>playing|tracking) (?<game>.+?) has joined\. Client\((?<version>[^)]*)\), \[(?<tags>.*)\]\.$", RegexOptions.CultureInvariant, 100)] private static partial Regex JoinRegex();
    [GeneratedRegex(@"^Notice \(all\): (?<player>.+?) \(Team #(?<team>\d+)\) has (?<action>left the game|stopped tracking the game)\. Client\((?<version>[^)]*)\), \[(?<tags>.*)\]\.$", RegexOptions.CultureInvariant, 100)] private static partial Regex DisconnectRegex();
    [GeneratedRegex(@"^\(Team #(?<team>\d+)\) (?<body>.+)$", RegexOptions.CultureInvariant, 100)] private static partial Regex CheckEnvelopeRegex();
    [GeneratedRegex(@"^Notice \(Team #(?<team>\d+)\): \[Hint\]: (?<body>.+)$", RegexOptions.CultureInvariant, 100)] private static partial Regex HintEnvelopeRegex();
    [GeneratedRegex(@"^Notice \(all\): (?<player>.+?) \(Team #(?<team>\d+)\) has (?<action>completed their goal|released all remaining items from their world|collected their items from other worlds|changed tags .+)\.$", RegexOptions.CultureInvariant, 100)] private static partial Regex PlayerEventRegex();
    [GeneratedRegex("'(?<tag>[^']*)'", RegexOptions.CultureInvariant, 100)] private static partial Regex TagsRegex();
    [GeneratedRegex(@"^Notice \(Player .+? in team \d+\):", RegexOptions.CultureInvariant, 100)] private static partial Regex PlayerNoticeRegex();
    [GeneratedRegex(@"^Notice \(all\): [^()]+:", RegexOptions.CultureInvariant, 100)] private static partial Regex ChatRegex();
}

public sealed class SessionInputException(string message) : Exception(message);
public sealed class SessionConflictException(string message) : Exception(message);
