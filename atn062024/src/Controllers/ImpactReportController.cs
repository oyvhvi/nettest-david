using atn062024.Models;
using atn062024.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace atn062024.Controllers;

[ApiController]
[Route("impact-report")]
public class ImpactReportController : ControllerBase
{
    private readonly IDbService _dbService_;

    public ImpactReportController(IDbService dbService)
    {
        _dbService_ = dbService;
    }

    [HttpGet]
    [Produces("application/json")]
    public async Task<ActionResult<AllPlayersImpactReport>> GetImpactReportAsync(CancellationToken ct)
    {
        DbResult<List<PlayerImpactReport>> impactReportsResult = await _dbService_.GetImpactReportsAsync(ct);

        if(!impactReportsResult.Success)
        {
            return StatusCode((int)HttpStatusCode.InternalServerError, impactReportsResult.FailureType?.ToString());
        }

        return new AllPlayersImpactReport(impactReportsResult.Value);
    }
}
