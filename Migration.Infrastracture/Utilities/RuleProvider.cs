using Migration.Infrastructure.models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Migration.Infrastructure.Utilities
{
    public interface IRuleProvider
    {
        Dictionary<string, Dictionary<string, List<NormalizationRule>>> LoadRules();
    }
    public class RuleProvider : IRuleProvider
    {
        private readonly string _path;
        public RuleProvider(string path)
        {
            _path = path;
        }

        public Dictionary<string, Dictionary<string, List<NormalizationRule>>> LoadRules()
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, List<NormalizationRule>>>>(json)
                   ?? new();
        }
    }


    public class RuleBasedUserNormalizer : INormalizer<OldUser, NewUser>
    {
        private readonly IRuleProvider _ruleProvider;

        public RuleBasedUserNormalizer(IRuleProvider ruleProvider)
        {
            _ruleProvider = ruleProvider;
        }

        public NewUser Normalize(OldUser oldUser)
        {
            var allRules = _ruleProvider.LoadRules();
            var rules = allRules["User"]; // seleziona il gruppo "User"

            string fullName = ApplyRules(oldUser.FullName ?? "", rules["FullName"]);
            string mail = ApplyRules(oldUser.Mail ?? "", rules["Mail"]);
            string phone = ApplyRules(oldUser.Phone ?? "", rules["Phone"]);

            var names = fullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            return new NewUser
            {
                UserId = Guid.NewGuid(),
                FirstName = names.ElementAtOrDefault(0) ?? "N/A",
                LastName = names.ElementAtOrDefault(1) ?? "N/A",
                Email = mail,
                PhoneNumber = phone,
                MigratedAt = DateTime.UtcNow
            };
        }

        private string ApplyRules(string input, List<NormalizationRule> rules)
        {
            string result = input;

            foreach (var rule in rules)
            {
                if (!string.IsNullOrEmpty(rule.Pattern))
                    result = Regex.Replace(result, rule.Pattern, rule.Replace ?? "");

                if (!string.IsNullOrEmpty(rule.Transform))
                {
                    result = rule.Transform switch
                    {
                        "Lower" => result.ToLowerInvariant(),
                        "Upper" => result.ToUpperInvariant(),
                        "Capitalize" => char.ToUpper(result[0]) + result.Substring(1).ToLower(),
                        _ => result
                    };
                }

                if (!string.IsNullOrEmpty(rule.Prefix) && !result.StartsWith(rule.Prefix))
                    result = rule.Prefix + result;
            }

            return result.Trim();
        }
    }
}
