using System.Diagnostics;
using Booster.Auth.Idp.Controllers.Base;
using Booster.Auth.Idp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Booster.Auth.Idp.Controllers;

[AllowAnonymous]
public class ErrorController : IdpBaseController
{
    [HttpGet("/error")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Index(
        [FromQuery(Name = "error")] string? error,
        [FromQuery(Name = "error_description")] string? errorDescription,
        [FromQuery(Name = "interaction_key")] string? interactionKey)
    {
        var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

        return View(ErrorViewModel.Create(error, errorDescription, interactionKey, requestId));
    }
}
