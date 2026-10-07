using System.Security.Claims;
using SyntaxCircus.Common;

namespace SyntaxCircus.AspNetCore.Common;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public bool IsAuthenticated => Principal.Identity?.IsAuthenticated ?? false;

    public string? UserId => Principal.GetSubject();

    public string? Email => Principal.GetEmail();

    public string? DisplayName => Principal.GetDisplayName() ?? Email;

    public ClaimsPrincipal Principal => httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
}
