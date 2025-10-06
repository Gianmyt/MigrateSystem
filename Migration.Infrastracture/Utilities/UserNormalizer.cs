using Migration.Infrastructure.models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Migration.Infrastructure.Utilities
{
    public interface INormalizer<TIn, TOut>
    {
        TOut Normalize(TIn input);
    }
    public class UserNormalizer : INormalizer<OldUser, NewUser>
    {
        public NewUser Normalize(OldUser oldUser)
        {
            if (oldUser == null)
                throw new ArgumentNullException(nameof(oldUser));

            var (firstName, lastName) = NormalizeName(oldUser.FullName);

            return new NewUser
            {
                UserId = Guid.NewGuid(),
                FirstName = firstName,
                LastName = lastName,
                Email = NormalizeEmail(oldUser.Mail),
                PhoneNumber = NormalizePhone(oldUser.Phone),
                MigratedAt = DateTime.UtcNow
            };
        }

        private (string, string) NormalizeName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return ("N/A", "N/A");

            var parts = fullName
                .Trim()
                .Replace("  ", " ")
                .Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)
                .Select(Capitalize)
                .ToArray();

            return (parts.ElementAtOrDefault(0) ?? "N/A",
                    parts.ElementAtOrDefault(1) ?? "N/A");
        }

        private static string Capitalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "N/A";
            TextInfo ti = CultureInfo.CurrentCulture.TextInfo;
            return ti.ToTitleCase(value.ToLower());
        }

        private static string NormalizeEmail(string email)
        {
            email = email.Trim().ToLowerInvariant();
            if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                throw new ArgumentException($"Email non valida: {email}");
            return email;
        }

        private static string? NormalizePhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return null;

            var cleaned = Regex.Replace(phone, @"[^\d]", "");
            if (string.IsNullOrWhiteSpace(cleaned))
                return null;

            if (!cleaned.StartsWith("+"))
                cleaned = "+39" + cleaned;

            return cleaned;
        }
    }
}

