using System.Text.Json;
using Core.Entities;
using Core.Interfaces;

namespace Infrastructure.Services;

public class AlertEvaluator
{
    private readonly IRepository<AlertRule> _rules;
    private readonly NotificationDispatcher _dispatcher;

    public AlertEvaluator(IRepository<AlertRule> rules, NotificationDispatcher dispatcher)
    {
        _rules = rules;
        _dispatcher = dispatcher;
    }

    public async Task EvaluateAsync(TagValue reading, CancellationToken ct = default)
    {
        if (!double.TryParse(reading.Value.ToString(), out var numericValue)) return;

        var matchingRules = await _rules.FindAsync(r =>
            r.AdapterId == reading.AdapterId &&
            r.TagAddress == reading.TagAddress &&
            r.IsEnabled, ct);

        foreach (var rule in matchingRules)
        {
            if (!IsTriggered(rule.Condition, numericValue, rule.Threshold)) continue;
            if (rule.LastTriggeredAt.HasValue &&
                DateTime.UtcNow - rule.LastTriggeredAt.Value < rule.Cooldown) continue;

            var alert = new AlertEvent
            {
                RuleId = rule.Id,
                AdapterId = reading.AdapterId,
                TagAddress = reading.TagAddress,
                TriggeredValue = reading.Value,
                Condition = rule.Condition,
                Threshold = rule.Threshold
            };

            await _dispatcher.DispatchAsync(alert,
                JsonSerializer.Deserialize<List<string>>(rule.NotificationChannels) ?? [],
                ct);

            rule.LastTriggeredAt = DateTime.UtcNow;
            await _rules.UpdateAsync(rule, ct);
        }
    }

    private const double EqualityTolerance = 0.0001;

    private static bool IsTriggered(AlertCondition condition, double value, double threshold) =>
        condition switch
        {
            AlertCondition.GreaterThan => value > threshold,
            AlertCondition.LessThan   => value < threshold,
            AlertCondition.Equal      => Math.Abs(value - threshold) < EqualityTolerance,
            AlertCondition.NotEqual   => Math.Abs(value - threshold) >= EqualityTolerance,
            _ => false
        };
}
