using Microsoft.AspNetCore.Http;
using TimeSheet.Domain.Services;

namespace TimeSheet.Api.Auth;

public class HttpContextCurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    public CurrentUserContext? Current =>
        httpContextAccessor.HttpContext?.Items.TryGetValue(CurrentUserMiddleware.HttpContextItemKey, out var value) == true
            ? value as CurrentUserContext
            : null;
}
