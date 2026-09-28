using System.ComponentModel.DataAnnotations;

namespace MovieWatch.Web.Request;

public sealed record UpdateViewerRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MaxLength(100)] string DisplayName);
