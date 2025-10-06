using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Migration.Infrastructure.Utilities
{
    public class NormalizationRule
    {
        public string? Pattern { get; set; }
        public string? Replace { get; set; }
        public string? Transform { get; set; }
        public string? Prefix { get; set; }
    }
}
