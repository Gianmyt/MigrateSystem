using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Migration.API.Stats;
using Migration.Infrastructure.services;
using System.Data;

namespace Migration.API.Audit
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuditController : ControllerBase
    {

        private readonly IAuditService _auditService;
        public AuditController(IAuditService auditService)
        {

            _auditService = auditService;
        }

        [Authorize(Roles = "Administrator")]
        [HttpGet("audit/report")]
        public async Task<IActionResult> GetAuditReport([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            return Ok(await _auditService.GetReportAsync(
                    from ?? DateTime.UtcNow.AddDays(-7),
                    to ?? DateTime.UtcNow));
        }
    }
}
