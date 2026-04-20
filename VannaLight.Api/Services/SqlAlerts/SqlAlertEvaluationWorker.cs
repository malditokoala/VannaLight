using Microsoft.AspNetCore.SignalR;
using VannaLight.Api.Hubs;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Api.Services.SqlAlerts;

public sealed class SqlAlertEvaluationWorker(
    IServiceScopeFactory scopeFactory,
    IHubContext<AssistantHub> hubContext,
    ILogger<SqlAlertEvaluationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SqlAlertEvaluationWorker iniciado.");
        using var timer = new PeriodicTimer(TickInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EvaluateBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fallo en el ciclo principal de SqlAlertEvaluationWorker.");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task EvaluateBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var ruleStore = scope.ServiceProvider.GetRequiredService<ISqlAlertRuleStore>();
        var stateStore = scope.ServiceProvider.GetRequiredService<ISqlAlertStateStore>();
        var eventStore = scope.ServiceProvider.GetRequiredService<ISqlAlertEventStore>();
        var evaluator = scope.ServiceProvider.GetRequiredService<ISqlAlertEvaluator>();

        var rules = await ruleStore.GetActiveAsync(ct);
        foreach (var rule in rules)
        {
            try
            {
                await EvaluateRuleAsync(rule, stateStore, eventStore, evaluator, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fallo evaluando la regla SQL {RuleKey}", rule.RuleKey);
            }
        }
    }

    private async Task EvaluateRuleAsync(
        SqlAlertRule rule,
        ISqlAlertStateStore stateStore,
        ISqlAlertEventStore eventStore,
        ISqlAlertEvaluator evaluator,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var nowText = now.ToString("O");
        var state = await stateStore.GetByRuleIdAsync(rule.Id, ct) ?? new SqlAlertState
        {
            RuleId = rule.Id,
            RuleKey = rule.RuleKey,
            LifecycleState = SqlAlertLifecycleState.Closed
        };

        if (TryParseUtc(state.LastEvaluationUtc, out var lastEvaluationUtc)
            && now - lastEvaluationUtc < TimeSpan.FromMinutes(rule.EvaluationFrequencyMinutes))
        {
            return;
        }

        var outcome = await evaluator.EvaluateAsync(rule, ct);
        state.LastEvaluationUtc = nowText;
        state.LastObservedValue = outcome.ObservedValue;
        state.UpdatedUtc = nowText;

        if (!outcome.Success)
        {
            var previousErrorUtc = state.LastErrorUtc;
            var previousErrorMessage = state.LastErrorMessage;
            var shouldLogFailure = !string.Equals(previousErrorMessage, outcome.ErrorMessage, StringComparison.OrdinalIgnoreCase)
                || !TryParseUtc(previousErrorUtc, out var lastErrorUtc)
                || now - lastErrorUtc >= TimeSpan.FromMinutes(Math.Max(15, rule.EvaluationFrequencyMinutes));

            state.LastErrorUtc = nowText;
            state.LastErrorMessage = outcome.ErrorMessage;
            await stateStore.UpsertAsync(state, ct);

            if (shouldLogFailure)
            {
                await eventStore.AddAsync(new SqlAlertEvent
                {
                    RuleId = rule.Id,
                    RuleKey = rule.RuleKey,
                    TenantKey = rule.TenantKey,
                    Domain = rule.Domain,
                    ConnectionName = rule.ConnectionName,
                    EventType = SqlAlertEventType.EvaluationFailed,
                    LifecycleState = state.LifecycleState,
                    ObservedValue = outcome.ObservedValue,
                    Threshold = rule.Threshold,
                    ComparisonOperator = rule.ComparisonOperator,
                    Message = outcome.Message,
                    ErrorText = outcome.ErrorMessage,
                    EventUtc = nowText
                }, ct);
            }

            return;
        }

        state.LastErrorUtc = null;
        state.LastErrorMessage = null;

        if (outcome.ConditionMet)
        {
            var shouldNotify = state.LifecycleState == SqlAlertLifecycleState.Closed;
            if (shouldNotify && TryParseUtc(state.LastTriggeredUtc, out var lastTriggeredUtc))
            {
                shouldNotify = now - lastTriggeredUtc >= TimeSpan.FromMinutes(rule.CooldownMinutes);
            }

            state.LifecycleState = state.LifecycleState == SqlAlertLifecycleState.Acknowledged
                ? SqlAlertLifecycleState.Acknowledged
                : SqlAlertLifecycleState.Open;

            if (shouldNotify)
            {
                state.LastTriggeredUtc = nowText;
                var eventId = await eventStore.AddAsync(new SqlAlertEvent
                {
                    RuleId = rule.Id,
                    RuleKey = rule.RuleKey,
                    TenantKey = rule.TenantKey,
                    Domain = rule.Domain,
                    ConnectionName = rule.ConnectionName,
                    EventType = SqlAlertEventType.Triggered,
                    LifecycleState = SqlAlertLifecycleState.Open,
                    ObservedValue = outcome.ObservedValue,
                    Threshold = rule.Threshold,
                    ComparisonOperator = rule.ComparisonOperator,
                    Message = outcome.Message,
                    QuerySummary = outcome.QueryPlan?.Summary,
                    SqlPreview = outcome.QueryPlan?.Sql,
                    EventUtc = nowText
                }, ct);

                state.OpenEventKey = $"evt:{eventId}";

                await hubContext.Clients.All.SendAsync("SqlAlertEventRaised", new
                {
                    EventId = eventId,
                    RuleId = rule.Id,
                    rule.RuleKey,
                    rule.DisplayName,
                    rule.Domain,
                    rule.ConnectionName,
                    MetricKey = rule.MetricKey,
                    ObservedValue = outcome.ObservedValue,
                    rule.Threshold,
                    Message = outcome.Message,
                    EventType = "Triggered",
                    EventUtc = nowText
                }, ct);
            }
        }
        else if (state.LifecycleState == SqlAlertLifecycleState.Open || state.LifecycleState == SqlAlertLifecycleState.Acknowledged)
        {
            state.LifecycleState = SqlAlertLifecycleState.Closed;
            state.LastResolvedUtc = nowText;
            state.OpenEventKey = null;

            var eventId = await eventStore.AddAsync(new SqlAlertEvent
            {
                RuleId = rule.Id,
                RuleKey = rule.RuleKey,
                TenantKey = rule.TenantKey,
                Domain = rule.Domain,
                ConnectionName = rule.ConnectionName,
                EventType = SqlAlertEventType.Resolved,
                LifecycleState = SqlAlertLifecycleState.Closed,
                ObservedValue = outcome.ObservedValue,
                Threshold = rule.Threshold,
                ComparisonOperator = rule.ComparisonOperator,
                Message = $"La condicion de alerta volvio al rango normal: {rule.DisplayName}.",
                QuerySummary = outcome.QueryPlan?.Summary,
                SqlPreview = outcome.QueryPlan?.Sql,
                EventUtc = nowText
            }, ct);

            await hubContext.Clients.All.SendAsync("SqlAlertEventRaised", new
            {
                EventId = eventId,
                RuleId = rule.Id,
                rule.RuleKey,
                rule.DisplayName,
                rule.Domain,
                rule.ConnectionName,
                MetricKey = rule.MetricKey,
                ObservedValue = outcome.ObservedValue,
                rule.Threshold,
                Message = $"La alerta '{rule.DisplayName}' se resolvio.",
                EventType = "Resolved",
                EventUtc = nowText
            }, ct);
        }

        await stateStore.UpsertAsync(state, ct);
    }

    private static bool TryParseUtc(string? value, out DateTime utc)
    {
        if (DateTime.TryParse(value, out var parsed))
        {
            utc = parsed;
            return true;
        }

        utc = DateTime.MinValue;
        return false;
    }
}
