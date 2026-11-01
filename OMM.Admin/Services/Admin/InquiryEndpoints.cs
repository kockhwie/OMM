using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace OMM.Admin.Services.Admin;

public static class InquiryEndpoints
{
    public static IEndpointRouteBuilder MapPhase5InquiryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/admin/inquiries/{sessionId:long}/export", async (
                long sessionId,
                ClaimsPrincipal user,
                IInquiryAdminService inquiryService,
                CancellationToken cancellationToken) =>
            {
                var actorId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Admin";
                var actorName = user.Identity?.Name ?? "Admin";
                var export = await inquiryService.ExportSessionAsync(sessionId, actorId, actorName, cancellationToken);
                return export is null
                    ? Results.NotFound()
                    : Results.File(export, "application/json; charset=utf-8", $"inquiry-session-{sessionId}.json");
            })
            .RequireAuthorization("RequireAdminRole");

        return endpoints;
    }
}
