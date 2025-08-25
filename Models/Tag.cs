// Models/Tag.cs
namespace Quotely.Api.Models;
public class Tag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public string Name { get; set; } = "";
    public List<QuoteTag> QuoteTags { get; set; } = new();
}
