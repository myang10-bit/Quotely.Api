using Microsoft.EntityFrameworkCore;
using Quotely.Api.Models;

namespace Quotely.Api.Data;
public class AppDb : DbContext
{
    public AppDb(DbContextOptions<AppDb> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<QuoteTag> QuoteTags => Set<QuoteTag>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(u => u.Email).IsUnique();

        b.Entity<Tag>()
            .HasIndex(t => new { t.UserId, t.Name })
            .IsUnique();

        b.Entity<QuoteTag>()
            .HasKey(qt => new { qt.QuoteId, qt.TagId });

        b.Entity<QuoteTag>()
            .HasOne(qt => qt.Quote)
            .WithMany(q => q.QuoteTags)
            .HasForeignKey(qt => qt.QuoteId);

        b.Entity<QuoteTag>()
            .HasOne(qt => qt.Tag)
            .WithMany(t => t.QuoteTags)
            .HasForeignKey(qt => qt.TagId);
    }
}
