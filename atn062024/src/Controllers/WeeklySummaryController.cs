using atn062024.Models;
using atn062024.Services;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Net;

namespace atn062024.Controllers;

[ApiController]
[Route("weekly-summary")]
public class WeeklySummaryController : ControllerBase
{
    private readonly IDbService _dbService_;

    public WeeklySummaryController(IDbService dbService)
    {
        _dbService_ = dbService;
    }

    [HttpGet]
    [Produces("application/json")]
    [Route("{year}/{week}")]
    public async Task<ActionResult<WeeklySummary>> GetWeeklySummaryAsync(
        Int32 year,
        Int32 week,
        // TODO continuationtoken?
        CancellationToken ct)
    {
        // TODO validate week 1-52
        // TODO configurable top score result count

        var weekStartInclusive = new DateTimeOffset(ISOWeek.ToDateTime(year, week, DayOfWeek.Monday), offset: TimeSpan.Zero);
        var weekEndExclusive = new DateTimeOffset(ISOWeek.ToDateTime(year, week + 1, DayOfWeek.Monday), offset: TimeSpan.Zero);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task<DbResult<List<PlayerScore>>> topScoringTask = _dbService_.GetTopScoringPlayersAsync(weekStartInclusive, weekEndExclusive, 10, cts.Token);
        Task<DbResult<List<PlayerActivity>>> mostActiveTask = _dbService_.GetMostActivePlayersAsync(weekStartInclusive, weekEndExclusive, 10, cts.Token);

        try
        {
            await Task.WhenAll(topScoringTask, mostActiveTask);
        }
        catch(Exception)
        {
            cts.Cancel();
            throw;
        }

        // Results are ready at this point, can be accessed without await
        DbResult<List<PlayerScore>> topScoringResult = topScoringTask.Result;
        DbResult<List<PlayerActivity>> mostActiveResult = mostActiveTask.Result;

        // check failures in arbitrary order
        if(!topScoringResult.Success)
        {
            return StatusCode((int)HttpStatusCode.InternalServerError, topScoringResult.FailureType?.ToString());
        }
        if(!mostActiveResult.Success)
        {
            return StatusCode((int)HttpStatusCode.InternalServerError, mostActiveResult.FailureType?.ToString());
        }

        var weeklySummary = new WeeklySummary(year, week, topScoringResult.Value, mostActiveResult.Value);

        return Ok(weeklySummary);
    }
}
