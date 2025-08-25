// Models/QuoteTag.cs
namespace Quotely.Api.Models;
public class QuoteTag
{
    public Guid QuoteId { get; set; }
    public Quote Quote { get; set; } = default!;
    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = default!;
}
