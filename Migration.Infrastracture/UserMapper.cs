using Migration.Infrastructure.models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Migration.Infrastructure
{
    public static class UserMapper
    {
        public static NewUser Map(OldUser oldUser)
        {
            if (string.IsNullOrWhiteSpace(oldUser.FullName))
                throw new ArgumentException("FullName is required");

            if (string.IsNullOrWhiteSpace(oldUser.Mail))
                throw new ArgumentException("Email is required");

            var names = oldUser.FullName.Trim()
                                        .Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

            return new NewUser
            {
                UserId = Guid.NewGuid(),
                FirstName = names.Length > 0 ? Capitalize(names[0]) : "N/A",
                LastName = names.Length > 1 ? Capitalize(names[1]) : "N/A",
                Email = NormalizeEmail(oldUser.Mail),
                PhoneNumber = NormalizePhone(oldUser.Phone),
                MigratedAt = DateTime.UtcNow
            };
        }

        private static string Capitalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return char.ToUpper(value[0]) + value.Substring(1).ToLower();
        }

        private static string NormalizeEmail(string email)
        {
            email = email.Trim().ToLowerInvariant();
            if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                throw new ArgumentException("Email non valida");
            return email;
        }

        private static string? NormalizePhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return null;

            // rimuove spazi, + e trattini
            var cleaned = Regex.Replace(phone, @"[\s\-\+]", "");
            if (!Regex.IsMatch(cleaned, @"^\d+$"))
                throw new ArgumentException("Numero di telefono non valido");
            return cleaned;
        }
    }
}
