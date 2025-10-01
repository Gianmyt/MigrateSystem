using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Migration.Infrastructure.models;

namespace Migration.API.Audit
{
    public interface IAuditService
    {
        Task<AuditReportDto> GetReportAsync(DateTime from, DateTime to);
    }
    public class AuditService : IAuditService
    {
        private readonly MigrationDbContext _db;
        public AuditService(MigrationDbContext db)
        {
            _db = db;
        }


        public async Task<AuditReportDto> GetReportAsync(DateTime from, DateTime to)
        {
            var report = new AuditReportDto
            {
                From = from,
                To = to,
                Total = await _db.AuditLogs.CountAsync(a => a.Timestamp >= from && a.Timestamp <= to),
                Success = await _db.AuditLogs.CountAsync(a => a.Timestamp >= from && a.Timestamp <= to && a.Status == "Success"),
                Failed = await _db.AuditLogs.CountAsync(a => a.Timestamp >= from && a.Timestamp <= to && a.Status == "Failed"),
                Actions = await _db.AuditLogs
        .Where(a => a.Timestamp >= from && a.Timestamp <= to)
        .GroupBy(a => a.Action)
        .Select(g => new ActionSummaryDto
        {
            Action = g.Key,
            Count = g.Count(),
            Success = g.Count(a => a.Status == "Success"),
            Failed = g.Count(a => a.Status == "Failed")
        })
        .ToListAsync(),
                Daily = await _db.AuditLogs
        .Where(a => a.Timestamp >= from && a.Timestamp <= to)
        .GroupBy(a => a.Timestamp.Date)
        .Select(g => new DailySummaryDto
        {
            Date = g.Key,
            Count = g.Count(),
            Success = g.Count(a => a.Status == "Success"),
            Failed = g.Count(a => a.Status == "Failed")
        })
        .ToListAsync()
            };

            return report;
        }
    }
}
