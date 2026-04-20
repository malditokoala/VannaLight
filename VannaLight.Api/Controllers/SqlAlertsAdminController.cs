using Microsoft.AspNetCore.Mvc;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;
using VannaLight.Core.UseCases;

namespace VannaLight.Api.Controllers;

public sealed record SqlAlertRuleUpsertRequest(
    long Id,
    string? RuleKey,
    string TenantKey,
    string Domain,
    string ConnectionName,
    string DisplayName,
    string MetricKey,
    string? DimensionKey,
    string? DimensionValue,
    SqlAlertComparisonOperator ComparisonOperator,
    decimal Threshold,
    SqlAlertTimeScope TimeScope,
    int EvaluationFrequencyMinutes,
    int CooldownMinutes,
    bool IsActive,
    string? Notes);

public sealed record SqlAlertActivationRequest(bool IsActive);
public sealed record SqlAlertPreviewRequest(
    string TenantKey,
    string Domain,
    string ConnectionName,
    string DisplayName,
    string MetricKey,
    string? DimensionKey,
    string? DimensionValue,
    SqlAlertComparisonOperator ComparisonOperator,
    decimal Threshold,
    SqlAlertTimeScope TimeScope,
    int EvaluationFrequencyMinutes,
    int CooldownMinutes,
    bool IsActive,
    string? Notes);

[ApiController]
[Route("api/admin/sql-alerts")]
public sealed class SqlAlertsAdminController(
    ISqlAlertRuleStore ruleStore,
    ISqlAlertStateStore stateStore,
    ISqlAlertEventStore eventStore,
    ISqlAlertMetricCatalog metricCatalog,
    ISqlAlertQueryBuilder queryBuilder,
    UpsertSqlAlertRuleUseCase upsertRuleUseCase,
    AcknowledgeSqlAlertUseCase acknowledgeUseCase,
    ClearSqlAlertUseCase clearUseCase) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAllAsync(CancellationToken ct)
    {
        var rules = await ruleStore.GetAllAsync(ct);
        var items = new List<object>(rules.Count);

        foreach (var rule in rules)
        {
            var state = await stateStore.GetByRuleIdAsync(rule.Id, ct);
            items.Add(MapRule(rule, state));
        }

        return Ok(items);
    }

    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalogAsync([FromQuery] string domain, [FromQuery] string connectionName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return BadRequest(new { Error = "Domain es obligatorio." });
        if (string.IsNullOrWhiteSpace(connectionName))
            return BadRequest(new { Error = "ConnectionName es obligatorio." });

        var catalog = await metricCatalog.GetCatalogAsync(domain, connectionName, ct);
        return Ok(catalog);
    }

    [HttpGet("events")]
    public async Task<IActionResult> GetEventsAsync([FromQuery] string? domain, [FromQuery] long? ruleId, [FromQuery] int limit = 100, CancellationToken ct = default)
    {
        var events = await eventStore.GetRecentAsync(domain, ruleId, limit, ct);
        return Ok(events);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] SqlAlertRuleUpsertRequest request, CancellationToken ct)
    {
        return await UpsertInternalAsync(request with { Id = 0 }, ct);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateAsync(long id, [FromBody] SqlAlertRuleUpsertRequest request, CancellationToken ct)
    {
        return await UpsertInternalAsync(request with { Id = id }, ct);
    }

    [HttpPost("preview")]
    public async Task<IActionResult> PreviewAsync([FromBody] SqlAlertPreviewRequest request, CancellationToken ct)
    {
        try
        {
            var rule = ToRule(0, null, request.TenantKey, request.Domain, request.ConnectionName, request.DisplayName, request.MetricKey, request.DimensionKey, request.DimensionValue, request.ComparisonOperator, request.Threshold, request.TimeScope, request.EvaluationFrequencyMinutes, request.CooldownMinutes, request.IsActive, request.Notes);
            var plan = await queryBuilder.BuildAsync(rule, ct);
            return Ok(plan);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    [HttpPost("{id:long}/activate")]
    public async Task<IActionResult> SetActiveAsync(long id, [FromBody] SqlAlertActivationRequest request, CancellationToken ct)
    {
        var ok = await ruleStore.SetIsActiveAsync(id, request.IsActive, ct);
        if (!ok)
            return NotFound(new { Error = "Alerta no encontrada." });

        return Ok(new { Id = id, request.IsActive });
    }

    [HttpPost("{id:long}/ack")]
    public async Task<IActionResult> AcknowledgeAsync(long id, CancellationToken ct)
    {
        var ok = await acknowledgeUseCase.ExecuteAsync(id, ct);
        if (!ok)
            return NotFound(new { Error = "Alerta no encontrada." });
        return Ok(new { Id = id, Status = "Acknowledged" });
    }

    [HttpPost("{id:long}/clear")]
    public async Task<IActionResult> ClearAsync(long id, CancellationToken ct)
    {
        var ok = await clearUseCase.ExecuteAsync(id, ct);
        if (!ok)
            return NotFound(new { Error = "Alerta no encontrada." });
        return Ok(new { Id = id, Status = "Cleared" });
    }

    private async Task<IActionResult> UpsertInternalAsync(SqlAlertRuleUpsertRequest request, CancellationToken ct)
    {
        try
        {
            var rule = ToRule(request.Id, request.RuleKey, request.TenantKey, request.Domain, request.ConnectionName, request.DisplayName, request.MetricKey, request.DimensionKey, request.DimensionValue, request.ComparisonOperator, request.Threshold, request.TimeScope, request.EvaluationFrequencyMinutes, request.CooldownMinutes, request.IsActive, request.Notes);
            var id = await upsertRuleUseCase.ExecuteAsync(rule, ct);
            var saved = await ruleStore.GetByIdAsync(id, ct);
            var state = saved is null ? null : await stateStore.GetByRuleIdAsync(saved.Id, ct);
            return Ok(MapRule(saved!, state));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    private static SqlAlertRule ToRule(long id, string? ruleKey, string tenantKey, string domain, string connectionName, string displayName, string metricKey, string? dimensionKey, string? dimensionValue, SqlAlertComparisonOperator comparisonOperator, decimal threshold, SqlAlertTimeScope timeScope, int evaluationFrequencyMinutes, int cooldownMinutes, bool isActive, string? notes)
    {
        return new SqlAlertRule
        {
            Id = id,
            RuleKey = string.IsNullOrWhiteSpace(ruleKey) ? string.Empty : ruleKey.Trim(),
            TenantKey = tenantKey?.Trim() ?? string.Empty,
            Domain = domain?.Trim() ?? string.Empty,
            ConnectionName = connectionName?.Trim() ?? string.Empty,
            DisplayName = displayName?.Trim() ?? string.Empty,
            MetricKey = metricKey?.Trim() ?? string.Empty,
            DimensionKey = string.IsNullOrWhiteSpace(dimensionKey) ? null : dimensionKey.Trim(),
            DimensionValue = string.IsNullOrWhiteSpace(dimensionValue) ? null : dimensionValue.Trim(),
            ComparisonOperator = comparisonOperator,
            Threshold = threshold,
            TimeScope = timeScope,
            EvaluationFrequencyMinutes = evaluationFrequencyMinutes,
            CooldownMinutes = cooldownMinutes,
            IsActive = isActive,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };
    }

    private static object MapRule(SqlAlertRule rule, SqlAlertState? state)
    {
        return new
        {
            rule.Id,
            rule.RuleKey,
            rule.TenantKey,
            rule.Domain,
            rule.ConnectionName,
            rule.DisplayName,
            rule.MetricKey,
            rule.DimensionKey,
            rule.DimensionValue,
            ComparisonOperator = rule.ComparisonOperator,
            ComparisonOperatorLabel = rule.ComparisonOperator switch
            {
                SqlAlertComparisonOperator.GreaterThan => ">",
                SqlAlertComparisonOperator.GreaterThanOrEqual => ">=",
                SqlAlertComparisonOperator.LessThan => "<",
                SqlAlertComparisonOperator.LessThanOrEqual => "<=",
                SqlAlertComparisonOperator.Equal => "=",
                SqlAlertComparisonOperator.NotEqual => "!=",
                _ => "?"
            },
            rule.Threshold,
            TimeScope = rule.TimeScope,
            TimeScopeLabel = rule.TimeScope.ToString(),
            rule.EvaluationFrequencyMinutes,
            rule.CooldownMinutes,
            rule.IsActive,
            rule.Notes,
            rule.CreatedUtc,
            rule.UpdatedUtc,
            RuntimeState = state?.LifecycleState ?? SqlAlertLifecycleState.Closed,
            LastObservedValue = state?.LastObservedValue,
            LastEvaluationUtc = state?.LastEvaluationUtc,
            LastTriggeredUtc = state?.LastTriggeredUtc,
            LastAcknowledgedUtc = state?.LastAcknowledgedUtc,
            LastResolvedUtc = state?.LastResolvedUtc,
            LastClearedUtc = state?.LastClearedUtc,
            LastErrorUtc = state?.LastErrorUtc,
            LastErrorMessage = state?.LastErrorMessage
        };
    }
}
