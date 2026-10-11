namespace Afterpelago.Domain.Sessions
{
    public enum SessionState { Draft, Published, Deleting }
    public enum SessionStatus { Live, Abandoned, Finished }
    public enum SessionImportMethod { WebLog, SpoilerLog }

    /// <summary>
    /// Represents an import that was applied/processed for a given session
    /// </summary>
    public sealed class SessionImport
    {
        /// <summary>
        /// Primary key in the DB, unique identifier for this import session
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// ID of the Session we imported data into. Foreign key to <see cref="Session.Id"/>
        /// </summary>
        public int SessionId { get; set; }
        public Session Session { get; set; } = null!;
        public Guid RequestId { get; set; }
        public SessionImportMethod Method { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public long ByteLength { get; set; }
        public string Sha256 { get; set; } = string.Empty;
        public int WarningCount { get; set; }
        public string WarningDetailsJson { get; set; } = "[]";
        public ICollection<SessionLogEvent> Events { get; set; } = [];

        /// <summary>
        /// When we imported data into the session.
        /// This is the time that we processed the import, not the time of the data itself.
        /// </summary>
        public DateTimeOffset ImportedAt { get; set; }
    }

    /// <summary>
    /// The key data structure for Afterpelago, represents a Session that we are providing a post-game report for
    /// </summary>
    public sealed class Session
    {
        /// <summary>
        /// Primary Key
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Name of the session
        /// </summary>
        public string Name { get; set; } = string.Empty;
        public SessionState State { get; set; }
        public SessionStatus Status { get; set; } = SessionStatus.Finished;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTime OriginalStart { get; set; }
        public DateTime OriginalEnd { get; set; }
        public DateTimeOffset? StartUtc { get; set; }
        public DateTimeOffset? EndUtc { get; set; }
        public int? SourceUtcOffsetMinutes { get; set; }

        /// <summary>
        /// Start of the session
        /// </summary>
        public DateTime Start { get => OriginalStart; set => OriginalStart = value; }

        /// <summary>
        /// Every time that we've imported data into the Session.
        /// Tracks each time we've used a log file, etc.
        /// </summary>
        public ICollection<SessionImport> ImportHistory { get => Imports; set => Imports = value; }
        public ICollection<SessionImport> Imports { get; set; } = [];
        public ICollection<Slot> Slots { get; set; } = [];
        public ICollection<SessionLogEvent> Events { get; set; } = [];
        public ICollection<Check> Checks { get; set; } = [];
        public ICollection<Hint> Hints { get; set; } = [];
        public ICollection<SessionFavorite> Favorites { get; set; } = [];
        public ICollection<SlotConnectionEvent> Connections { get; set; } = [];
    }
}
