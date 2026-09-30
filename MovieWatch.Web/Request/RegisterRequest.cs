using System.ComponentModel.DataAnnotations;

namespace MovieWatch.Web.Request;

public record RegisterRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, StringLength(128, MinimumLength = 8)] string Password,
    [Required, MaxLength(100)] string DisplayName
    );
