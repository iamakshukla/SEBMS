using System.ComponentModel.DataAnnotations;

namespace SEBMS.Application.Auth;

public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    string? DisplayName);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record AuthResponse(string Token, DateTime ExpiresAt, string Email, string? DisplayName);
