// Models/Quote.cs
namespace Quotely.Api.Models;
public class Quote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public string Text { get; set; } = "";
    public string? SourceTitle { get; set; }
    public string? SourceAuthor { get; set; }
    public string? SourceUrl { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<QuoteTag> QuoteTags { get; set; } = new();
}
