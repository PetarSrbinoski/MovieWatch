using System.ComponentModel.DataAnnotations;

namespace MovieWatch.Web.Request;

public sealed record LoginRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MaxLength(128)] string Password);
