using System;
using System.Collections.Generic;
using System.Text;
using VannaLight.Core.Models;

namespace VannaLight.Core.Abstractions
{
    public interface IPatternMatcherService
    {
        PatternMatchResult Match(string question);
    }

    public interface ITemplateSqlBuilder
    {
        string BuildSql(PatternMatchResult match);
    }
}
