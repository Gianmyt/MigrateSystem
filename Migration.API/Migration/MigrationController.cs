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
using static Migration.API.Migration.MigrationModel;

namespace Migration.API.Migration
{
    [ApiController]
    [Route("api/[controller]")]
    public class MigrationController : ControllerBase
    {
        private readonly RabbitMqService _rabbitMqService;
        private readonly IAuditLogService _auditLogService;
        private readonly IMigrationService _migrationService;
        public MigrationController(RabbitMqService rabbitMqService,IAuditLogService auditLogService , IMigrationService migrationService  )
        {
            _rabbitMqService = rabbitMqService;
            _auditLogService = auditLogService;
            _migrationService = migrationService;
        }

        [HttpPost("request")]
        [Authorize]
        public async Task<IActionResult> RequestMigration([FromBody] MigrationRequest dto )
        {

            await _auditLogService.LogAsync(
                userId:dto.OldUser.Id.ToString(),
                action: "MigrationRequested",
                details: $"Requested migration for {dto.OldUser.Id}",
                status: "Pending"
            );

            var user = _migrationService.GetUserMigrationAsync(dto.OldUser).GetAwaiter().GetResult();
            if (user != null) 
            {
                await _auditLogService.LogAsync(
                userId: dto.OldUser.Id.ToString(),
                action: "MigrationRequested",
                details: $"Requested migration for {dto.OldUser.Id}",
                status: "Reject - already migrated"
            );
                return BadRequest(new {message ="Utente già migrato"});
            }
            await _rabbitMqService.SendMessageAsync(new MqModel { OldUser = dto.OldUser, Forced = false, SlotId = dto.slotId });

            return Ok(new { message = "Richiesta accettata" });
        }

        [Authorize(Roles = "Administrator")]
        [HttpPost("force")]
        public async Task<IActionResult> ForceMigration([FromBody] AdministrativeMigrationRequest dto)
        {
            await _auditLogService.LogAsync(
                userId: dto.OldUser.Id.ToString(),
                action: "MigrationRequested - Admin",
                details: $"Requested migration for {dto.OldUser.Id} from administrator",
                status: "Pending"
            );
            var user = _migrationService.GetUserMigrationAsync(dto.OldUser);
            if (user != null)
            {
                await _auditLogService.LogAsync(
                userId: dto.OldUser.Id.ToString(),
                action: "MigrationRequested",
                details: $"User already migrated {dto.OldUser.Id}",
                status: "Reject",
                success : false
            );
                return BadRequest(new { message = "Utente già migrato" });
            }

           
            await _rabbitMqService.SendMessageAsync(new MqModel { OldUser = dto.OldUser, Forced = true , SlotId = null});

            return Ok(new { message = "Migrazione forzata avviata" });
        }


        [Authorize]
        [HttpPost("propose")]
        public async Task<IActionResult> ProposeMigration([FromBody] MigrationSlotReservation dto)
        {
            var user = await _migrationService.GetUserMigrationAsync(dto.OldUser);
            if (user == null)
            {
                var slot = await _migrationService.TryReserveSlotAsync(dto.OldUser);

                if (slot == null)
                {
                    return Ok(new
                    {
                        canMigrate = false,
                        message = "Nessuno slot disponibile"
                    });
                }



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
       
       
    }
}
