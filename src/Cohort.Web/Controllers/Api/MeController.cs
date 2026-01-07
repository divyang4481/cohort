using System.Security.Claims;
using Cohort.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cohort.Web.Controllers.Api;

[ApiController]
[Route("api/me")]
public sealed class MeController : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Get()
    {
        if (User?.Identity?.IsAuthenticated != true)
        {
            return Ok(new MeDto(
                IsAuthenticated: false,
                Name: null,
                AuthSource: null,
                ParticipantMode: null,
                AppRole: null,
                Oid: null,
                Tid: null,
                Sub: null,
                NameIdentifier: null,
                Claims: Array.Empty<ClaimDto>()));
        }

        var claims = User.Claims
            .Select(c => new ClaimDto(c.Type, c.Value))
            .OrderBy(c => c.Type)
            .ToList();

        return Ok(new MeDto(
            IsAuthenticated: true,
            Name: User.Identity?.Name,
            AuthSource: User.FindFirst("auth_source")?.Value,
            ParticipantMode: User.FindFirst(AuthConstants.Claims.ParticipantMode)?.Value,
            AppRole: User.FindFirst(AuthConstants.Claims.AppRole)?.Value,
            Oid: User.FindFirst("oid")?.Value,
            Tid: User.FindFirst("tid")?.Value,
            Sub: User.FindFirst("sub")?.Value,
            NameIdentifier: User.FindFirstValue(ClaimTypes.NameIdentifier),
            Claims: claims));
    }

    public sealed record ClaimDto(string Type, string Value);

    public sealed record MeDto(
        bool IsAuthenticated,
        string? Name,
        string? AuthSource,
        string? ParticipantMode,
        string? AppRole,
        string? Oid,
        string? Tid,
        string? Sub,
        string? NameIdentifier,
        IReadOnlyList<ClaimDto> Claims);
}
