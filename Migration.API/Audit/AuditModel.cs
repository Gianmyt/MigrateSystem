using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Migration.API
{
    public class AuditReportDto
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int Total { get; set; }
        public int Success { get; set; }
        public int Failed { get; set; }
        public List<ActionSummaryDto> Actions { get; set; } = new();
        public List<DailySummaryDto> Daily { get; set; } = new();
    }

    public class ActionSummaryDto
    {
        public string Action { get; set; } = string.Empty;
        public int Count { get; set; }
        public int Success { get; set; }
        public int Failed { get; set; }
    }

    public class DailySummaryDto
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
        public int Success { get; set; }
        public int Failed { get; set; }
    }
}
