namespace MovieWatch.Domain.Dto;

public sealed record AccountDto(string AccountId, IReadOnlyList<string> Roles);
