using System;
using System.Collections.Generic;
using System.Text;

namespace VannaLight.Core.Models
{
    public class CachedQuestionJobRow
    {
        public string JobId { get; set; } = string.Empty;
        public string? SqlText { get; set; }
        public string? ResultJson { get; set; }
    }
}
