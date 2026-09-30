using MovieWatch.Domain.Common;

namespace MovieWatch.Domain.Dto;

public record AccountDto(
    string AccountId,
    IReadOnlyList<AccountRole> Roles
    );
