using Infrastructure;
using Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Migration.API.MqServices;
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
        private readonly IMigrationService _migrationService;
        public MigrationController(MigrationDbContext db, RabbitMqService rabbitMqService,IAuditLogService auditLogService , IMigrationService migrationService  )
        {
            _db = db;
            _rabbitMqService = rabbitMqService;
            _auditLogService = auditLogService;
            _migrationService = migrationService;
        }

        [HttpPost("request")]
        //[Authorize]
        public async Task<IActionResult> RequestMigration([FromBody] OldUser oldUser , int slotId)
        {
            var userId = User.Identity?.Name ?? "Unknown";

            await _auditLogService.LogAsync(
                userId:oldUser.Id.ToString(),
                action: "MigrationRequested",
                details: $"Requested migration for {oldUser.Id}",
                status: "Pending"
            );

            var user = _migrationService.GetUserMigrationAsync(oldUser).GetAwaiter().GetResult();
            if (user != null) 
            {
                await _auditLogService.LogAsync(
                userId: oldUser.Id.ToString(),
                action: "MigrationRequested",
                details: $"Requested migration for {oldUser.Id}",
                status: "Reject - already migrated"
            );
                return BadRequest(new {message ="Utente già migrato"});
            }
            await _rabbitMqService.SendMessageAsync(new MqModel { OldUser = oldUser, Forced = true, SlotId = slotId });

            return Ok(new { message = "Richiesta accettata" });
        }

        // --------------------------
        // 2. Migrazione forzata (admin)
        // --------------------------
        [Authorize(Roles = "Administrator")]
        [HttpPost("force/{userId}")]
        public async Task<IActionResult> ForceMigration([FromBody] OldUser oldUser)
        {
            await _auditLogService.LogAsync(
                userId: oldUser.Id.ToString(),
                action: "MigrationRequested - Admin",
                details: $"Requested migration for {oldUser.Id} from administrator",
                status: "Pending"
            );
            var user = _migrationService.GetUserMigrationAsync(oldUser);
            if (user != null)
            {
                await _auditLogService.LogAsync(
                userId: oldUser.Id.ToString(),
                action: "MigrationRequested",
                details: $"Requested migration for {oldUser.Id}",
                status: "Reject - already migrated"
            );
                return BadRequest(new { message = "Utente già migrato" });
            }

           

            // Invia il messaggio al Worker
            await _rabbitMqService.SendMessageAsync(new MqModel { OldUser = oldUser, Forced = true , SlotId = null});

            return Ok(new { message = "Migrazione forzata avviata" });
        }


       // [Authorize]
        [HttpPost("propose")]
        public async Task<IActionResult> ProposeMigration([FromBody] OldUser oldUser)
        {
            var user = await _migrationService.GetUserMigrationAsync(oldUser);
            if (user == null)
            {
                // Cerca uno slot libero o con prenotazione scaduta
                //var slot = await _migrationService.GetSlotAsync(oldUser);
                var slot = await _migrationService.TryReserveSlotAsync(oldUser);

                if (slot == null)
                {
                    return Ok(new
                    {
                        canMigrate = false,
                        message = "Nessuno slot disponibile"
                    });
                }
              //  await _migrationService.TryReserveSlotAsync(oldUser);

                // Riserva lo slot per 2 minuti


                return Ok(new
                {
                    canMigrate = true,
                    message = "Slot riservato per 2 minuti",
                    slotId = slot.Id
                });
            }
            else
            {
                return Ok(new
                {
                    canMigrate = false,
                    message = "Utente già migrato"
                });
            }
        }
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
