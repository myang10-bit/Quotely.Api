// Dtos/QuoteDtos.cs
namespace Quotely.Api.Dtos;
public record CreateQuoteDto(
    string Text,
    string? SourceTitle,
    string? SourceAuthor,
    string? SourceUrl,
    string? Note,
    List<string>? Tags
);

public record QuoteDto(
    Guid Id,
    string Text,
    string? SourceTitle,
    string? SourceAuthor,
    string? SourceUrl,
    string? Note,
    List<string> Tags,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public class UpdateQuoteDto
{
    public string Text { get; set; } = default!;
    public string? SourceTitle { get; set; }
    public string? SourceAuthor { get; set; }
    public string? SourceUrl { get; set; }
    public string? Note { get; set; }
    public IEnumerable<string>? Tags { get; set; }
}