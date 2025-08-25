// Dtos/AuthDtos.cs
namespace Quotely.Api.Dtos;
public record RegisterDto(string Email, string Password);
public record LoginDto(string Email, string Password);
public record AuthResult(string Token);

