using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WorkoutCompanion.Server.Pages.Progress;

[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
public sealed class ProgressDetailsModel : PageModel
{
    public Guid TrackSyncId { get; private set; }

    public void OnGet(Guid trackSyncId)
    {
        TrackSyncId = trackSyncId;
    }
}
