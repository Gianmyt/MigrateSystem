using Infrastructure;
using Microsoft.Extensions.Logging;
using Migration.Infrastructure.models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Migration.Infrastructure.services
{
    public interface IAuditLogService
    {
        Task LogAsync(string userId, string action,string status ,string? details = null, string? ipAddress = null, bool success = true);
    }
    public class AuditLogService : IAuditLogService
    {
        private readonly MigrationDbContext _db;
        private readonly ILogger<AuditLogService> _logger;

        public AuditLogService(MigrationDbContext db, ILogger<AuditLogService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task LogAsync(string userId, string action,string status, string? details = null, string? ipAddress = null, bool success = true)
        {
            var log = new AuditLog
            {
                UserId = userId,
                Action = action,
                Details = details,
                Status = status,
                IpAddress = ipAddress,
                Success = success
            };

            _db.AuditLogs.Add(log);
            await _db.SaveChangesAsync();


            if (success)
                _logger.LogInformation("AUDIT | User: {UserId} | Action: {Action} | Details: {Details}", userId, action, details);
            else
                _logger.LogWarning("AUDIT FAIL | User: {UserId} | Action: {Action} | Details: {Details}", userId, action, details);
        }
    }
}
