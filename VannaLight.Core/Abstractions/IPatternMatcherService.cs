using System;
using System.Collections.Generic;
using System.Text;
using VannaLight.Core.Models;

namespace VannaLight.Core.Abstractions
{
    public interface IPatternMatcherService
    {
        Task<PatternMatchResult> MatchAsync(string question, string? domain, CancellationToken ct = default);
        Task<string?> InferIntentNameAsync(string question, string? domain, CancellationToken ct = default);
    }

    public interface ITemplateSqlBuilder
    {
        string BuildSql(PatternMatchResult match);
    }
}
