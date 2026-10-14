using BatchReleaseIQ.Api.Dtos;
using BatchReleaseIQ.Api.Security;
using BatchReleaseIQ.Domain;
using BatchReleaseIQ.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace BatchReleaseIQ.Api.Controllers;

[ApiController, Route("api")]
public class InsightController : ControllerBase
{
    readonly InsightService _insight;
    readonly BatchStateService _state;
    readonly CurrentUserState _me;
    readonly IClock _clock;

    public InsightController(InsightService insight, BatchStateService state, CurrentUserState me, IClock clock)
    {
        _insight = insight; _state = state; _me = me; _clock = clock;
    }

    /// <summary>The prioritised "next to release" queue.</summary>
    [HttpGet("queue"), Requires(Permissions.QueueView)]
    public async Task<object> Queue([FromQuery] BatchFilter f)
    {
        var ranked = await _insight.QueueAsync(f);
        var s = await _state.SettingsAsync();
        return new
        {
            today = _clock.Today,
            settings = new { s.ReadinessBandSize, s.StuckDays, s.ExpiryWarningDays },
            items = ranked.Select((a, i) => BatchRowDto.From(a, i + 1))
        };
    }

    [HttpGet("workbench"), Requires(Permissions.QueueView)]
    public async Task<object> Workbench()
    {
        var s = await _state.SettingsAsync();
        var stuck = (await _insight.ScopeAsync(new BatchFilter { Metric = "stuck" }))
            .OrderByDescending(a => Domain.Engine.Metrics.DaysSinceProgress(a.Batch, _clock.Today));
        return new
        {
            stuckDays = s.StuckDays,
            department = _me.DepartmentId,
            mine = await _insight.WorkItemsAsync(_me.DepartmentId, mine: true),
            waitingOnOthers = await _insight.WorkItemsAsync(_me.DepartmentId, mine: false),
            stuck = stuck.Select(a => new
            {
                row = BatchRowDto.From(a),
                daysSinceProgress = Domain.Engine.Metrics.DaysSinceProgress(a.Batch, _clock.Today),
                waitingFor = a.PendingItems.Where(p => !p.IsFinalRelease).Take(3).Select(p => $"{p.Name} ({p.Department})")
            })
        };
    }

    [HttpGet("dashboard"), Requires(Permissions.DashboardView)]
    public Task<DashboardData> Dashboard([FromQuery] BatchFilter f) => _insight.DashboardAsync(f);
}
