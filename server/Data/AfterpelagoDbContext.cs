using Afterpelago.Domain;
using Afterpelago.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Afterpelago.Data;

public sealed class AfterpelagoDbContext(DbContextOptions<AfterpelagoDbContext> options) : DbContext(options)
{
    public const int DemoCounterId = 1;

    public DbSet<DemoCounter> DemoCounters => Set<DemoCounter>();

    public DbSet<AccessRecord> AccessRecords => Set<AccessRecord>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<SessionImport> SessionImports => Set<SessionImport>();
    public DbSet<Slot> Slots => Set<Slot>();
    public DbSet<SessionLogEvent> SessionLogEvents => Set<SessionLogEvent>();
    public DbSet<SlotConnectionEvent> SlotConnectionEvents => Set<SlotConnectionEvent>();
    public DbSet<Check> Checks => Set<Check>();
    public DbSet<Hint> Hints => Set<Hint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccessRecord>(entity =>
        {
            entity.HasKey(a => a.DiscordUserId);
            entity.Property(a => a.DiscordUserId).HasMaxLength(32);
            entity.Property(a => a.DisplayName).HasMaxLength(100);
            entity.Property(a => a.AvatarHash).HasMaxLength(128);
            entity.Property(a => a.Role).HasMaxLength(32);
            // Stored as text ('Pending'/'Approved'/'Denied') so admins can edit it with plain SQL.
            entity.Property(a => a.Status).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(a => a.Status);
        });

        modelBuilder.Entity<DemoCounter>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).ValueGeneratedNever();
            entity.HasData(new DemoCounter
            {
                Id = DemoCounterId,
                Value = 0,
                UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            });
        });

        modelBuilder.Entity<Session>(entity =>
        {
            entity.Ignore(s => s.Start);
            entity.Ignore(s => s.ImportHistory);
            entity.Property(s => s.Name).HasMaxLength(200);
            entity.Property(s => s.State).HasConversion<string>().HasMaxLength(16);
            entity.HasMany(s => s.Imports).WithOne(i => i.Session).HasForeignKey(i => i.SessionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(s => s.Slots).WithOne(s => s.Session).HasForeignKey(s => s.SessionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(s => s.Events).WithOne(e => e.Session).HasForeignKey(e => e.SessionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(s => s.Connections).WithOne(c => c.Session).HasForeignKey(c => c.SessionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(s => s.Checks).WithOne(c => c.Session).HasForeignKey(c => c.SessionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(s => s.Hints).WithOne(h => h.Session).HasForeignKey(h => h.SessionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(s => new { s.State, s.StartUtc });
        });

        modelBuilder.Entity<SessionImport>(entity =>
        {
            entity.Property(i => i.Id).HasAnnotation("Sqlite:Autoincrement", true);
            entity.Property(i => i.Method).HasConversion<string>().HasMaxLength(16);
            entity.Property(i => i.OriginalFileName).HasMaxLength(260);
            entity.Property(i => i.Sha256).HasMaxLength(64);
            entity.HasIndex(i => i.RequestId).IsUnique();
        });

        modelBuilder.Entity<Slot>(entity =>
        {
            entity.Property(s => s.PlayerName).UseCollation("BINARY").HasMaxLength(200);
            entity.Property(s => s.GameName).HasMaxLength(200);
            entity.HasIndex(s => new { s.SessionId, s.TeamNumber, s.PlayerName }).IsUnique();
        });

        modelBuilder.Entity<SessionLogEvent>(entity =>
        {
            entity.Property(e => e.Category).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(e => new { e.ImportId, e.SourceLineNumber }).IsUnique();
            entity.HasIndex(e => new { e.SessionId, e.TimestampUtc, e.SourceLineNumber });
            entity.HasOne(e => e.Import).WithMany(i => i.Events).HasForeignKey(e => e.ImportId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Slot).WithMany().HasForeignKey(e => e.SlotId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<SlotConnectionEvent>(entity =>
        {
            entity.Property(c => c.Action).HasConversion<string>().HasMaxLength(16);
            entity.Property(c => c.Kind).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(c => c.LogEventId).IsUnique();
            entity.HasOne(c => c.Slot).WithMany(s => s.Connections).HasForeignKey(c => c.SlotId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(c => c.LogEvent).WithOne().HasForeignKey<SlotConnectionEvent>(c => c.LogEventId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Check>(entity =>
        {
            entity.HasIndex(c => c.LogEventId).IsUnique();
            entity.HasIndex(c => new { c.SessionId, c.SenderSlotId });
            entity.HasIndex(c => new { c.SessionId, c.ReceiverSlotId });
            entity.HasOne(c => c.LogEvent).WithOne().HasForeignKey<Check>(c => c.LogEventId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(c => c.SenderSlot).WithMany().HasForeignKey(c => c.SenderSlotId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(c => c.ReceiverSlot).WithMany().HasForeignKey(c => c.ReceiverSlotId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Hint>(entity =>
        {
            entity.HasIndex(h => h.LogEventId).IsUnique();
            entity.HasOne(h => h.LogEvent).WithOne().HasForeignKey<Hint>(h => h.LogEventId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(h => h.SenderSlot).WithMany().HasForeignKey(h => h.SenderSlotId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(h => h.ReceiverSlot).WithMany().HasForeignKey(h => h.ReceiverSlotId).OnDelete(DeleteBehavior.NoAction);
        });
    }

    // SQLite cannot compare/order DateTimeOffset natively; store as sortable ISO strings.
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToIsoConverter>();
    }
}

public sealed class DateTimeOffsetToIsoConverter()
    : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTimeOffset, string>(
        v => v.UtcDateTime.ToString("O"),
        v => DateTimeOffset.Parse(v, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind));
