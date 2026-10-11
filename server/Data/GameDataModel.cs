using Afterpelago.Domain;
using Afterpelago.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Afterpelago.Data;

internal static class GameDataModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<Game>(e =>
        {
            e.Property(g => g.Name).UseCollation("BINARY").HasMaxLength(200);
            e.Property(g => g.CoverHash).HasMaxLength(64);
            e.HasIndex(g => g.Name).IsUnique();
            e.Property(g => g.Revision).IsConcurrencyToken();
        });
        model.Entity<Slot>().HasOne(s => s.Game).WithMany().HasForeignKey(s => s.GameId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<GameItem>(e =>
        {
            e.Property(i => i.Name).UseCollation("BINARY").HasMaxLength(500);
            e.HasIndex(i => new { i.GameId, i.Name }).IsUnique();
            e.HasOne<Game>().WithMany().HasForeignKey(i => i.GameId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<GameImageAsset>(e =>
        {
            e.HasIndex(a => new { a.GameId, a.Hash }).IsUnique();
            e.HasOne<Game>().WithMany().HasForeignKey(a => a.GameId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<GameImageCandidate>(e =>
        {
            e.HasIndex(c => new { c.GameId, c.SourceKey }).IsUnique();
            e.HasOne<Game>().WithMany().HasForeignKey(c => c.GameId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.Asset).WithMany().HasForeignKey(c => c.AssetId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<GameScrape>().WithMany().HasForeignKey(c => c.ImportId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<GameItemImage>(e =>
        {
            e.HasKey(m => m.GameItemId);
            e.HasOne(m => m.GameItem).WithOne(i => i.Image).HasForeignKey<GameItemImage>(m => m.GameItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.Candidate).WithMany().HasForeignKey(m => m.CandidateId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<GameScrape>(e =>
        {
            e.HasIndex(s => s.CreatedAt);
            e.HasOne<Game>().WithMany().HasForeignKey(s => s.GameId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
