// Models/User.cs
using System.ComponentModel.DataAnnotations;

namespace Quotely.Api.Models;
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(255)] public string Email { get; set; } = "";
    [MaxLength(255)] public string PasswordHash { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Quote> Quotes { get; set; } = new();
}
