using atn062024.Models;
using atn062024.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace atn062024.Controllers;

[ApiController]
[Route("players")]
public class PlayersController : ControllerBase
{
    private readonly IDbService _dbService_;

    public PlayersController(IDbService dbService)
    {
        _dbService_ = dbService;
    }

    //[Authorize(Roles = Role.CREATE_PLAYER)]
    [HttpPost]
    [Produces("application/json")]
    public async Task<ActionResult<Player>> InsertPlayerAsync([FromBody] InsertPlayerRequestBody request, CancellationToken ct)
    {
        // Generate a new ID
        // This could also be specified in the insert request as a unique user name for better personalization, or db auto increment ID.

        Guid newId = Guid.NewGuid();

        DbResult<Player> dbResult = await _dbService_.InsertPlayerAsync(newId, request.Name, ct);

        if(!dbResult.Success)
        {
            // react to expected issues
            return dbResult.FailureType switch
            {
                FailureType.Conflict => Conflict(),
                FailureType.BadRequest => BadRequest(),
                // notfound not expected here...
                _ => StatusCode((int)HttpStatusCode.InternalServerError)
            };
        }

        // TODO change to Created(path,val) ?
        return Ok(dbResult.Value);
    }

    [HttpPost]
    [Route("{playerId}/scores")]
    public async Task<ActionResult> InsertScoreAsync(Guid playerId, [FromBody] InsertScoreRequestBody request, CancellationToken ct)
    {
        DbResult dbResult = await _dbService_.InsertScoreAsync(
            playerId, request.PlayStart, TimeSpan.FromSeconds(request.TimeSpentSeconds), request.Score, request.PercentCorrectAnswers, ct);

        if(!dbResult.Success)
        {
            return dbResult.FailureType switch
            {
                FailureType.Conflict => Conflict(),
                FailureType.BadRequest => BadRequest(),
                FailureType.NotFound => NotFound(),
                _ => StatusCode((int)HttpStatusCode.InternalServerError)
            };
        }

        return Ok();
    }
}
