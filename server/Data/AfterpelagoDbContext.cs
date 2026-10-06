using Afterpelago.Domain;
using Microsoft.EntityFrameworkCore;

namespace Afterpelago.Data;

public sealed class AfterpelagoDbContext(DbContextOptions<AfterpelagoDbContext> options) : DbContext(options)
{
    public const int DemoCounterId = 1;

    public DbSet<DemoCounter> DemoCounters => Set<DemoCounter>();

    public DbSet<AccessRecord> AccessRecords => Set<AccessRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccessRecord>(entity =>
        {
            entity.HasKey(a => a.DiscordUserId);
            entity.Property(a => a.DiscordUserId).HasMaxLength(32);
            entity.Property(a => a.DisplayName).HasMaxLength(100);
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
