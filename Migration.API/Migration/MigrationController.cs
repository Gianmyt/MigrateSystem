using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Migration.API.Services;
using Migration.Infrastructure;
using Migration.Infrastructure.models;
using Migration.Infrastructure.services;

namespace Migration.API.Migration
{
    [ApiController]
    [Route("api/[controller]")]
    public class MigrationController : ControllerBase
    {
        private readonly MigrationDbContext _db;
        private readonly RabbitMqService _rabbitMqService;
        private readonly IAuditLogService _auditLogService;

        public MigrationController(MigrationDbContext db, RabbitMqService rabbitMqService,IAuditLogService auditLogService)
        {
            _db = db;
            _rabbitMqService = rabbitMqService;
            _auditLogService = auditLogService;
        }

        [HttpPost("request")]
        //[Authorize]
        public async Task<IActionResult> RequestMigration([FromBody] OldUser oldUser)
        {
            var userId = User.Identity?.Name ?? "Unknown";

            await _auditLogService.LogAsync(
                userId:oldUser.Id.ToString(),
                action: "MigrationRequested",
                details: $"Requested migration for {oldUser.Id}",
                status: "Pending"
            );

            await _rabbitMqService.SendMessageAsync(oldUser.Id);

            return Ok(new { message = "Richiesta accettata" });
        }

        // --------------------------
        // 2. Migrazione forzata (admin)
        // --------------------------
        [Authorize(Roles = "Administrator")]
        [HttpPost("force/{userId}")]
        public async Task<IActionResult> ForceMigration(string userId)
        {
            var user = await _db.UserMigrations.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return NotFound("Utente non trovato");

            if (user.IsMigrated)
                return BadRequest("Utente già migrato");

            // Aggiorna lo stato a "Queued" se necessario
            user.Status = "Queued";
            await _db.SaveChangesAsync();

            // Invia il messaggio al Worker
            await _rabbitMqService.SendMessageAsync(new { UserId = userId });

            return Ok(new { message = "Migrazione forzata avviata" });
        }

        // --------------------------
        // 3. Statistiche globali (admin)
        // --------------------------
        [Authorize(Roles = "Administrator")]
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var total = await _db.UserMigrations.CountAsync();
            var migrated = await _db.UserMigrations.CountAsync(u => u.IsMigrated);
            var failed = await _db.UserMigrations.CountAsync(u => u.Status != null && u.Status.StartsWith("Failed"));
            var queued = await _db.UserMigrations.CountAsync(u => u.Status == "Queued");

            return Ok(new
            {
                totalUsers = total,
                migratedUsers = migrated,
                failedUsers = failed,
                queuedUsers = queued,
                percentMigrated = total > 0 ? Math.Round((double)migrated / total * 100, 2) : 0
            });
        }

        // --------------------------
        // 4. Dettaglio utente (opzionale)
        // --------------------------
        [Authorize(Roles = "Administrator")]
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetUserStatus(string userId)
        {
            var user = await _db.UserMigrations.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return NotFound("Utente non trovato");

            return Ok(new
            {
                user.UserId,
                user.IsMigrated,
                user.Status,
                user.MigrationDate
            });
        }
    }
}
