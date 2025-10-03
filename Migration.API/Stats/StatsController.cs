using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Migration.API.Migration;
using Migration.API.MqServices;
using Migration.Infrastructure.services;

namespace Migration.API.Stats
{
    [Route("api/[controller]")]
    [ApiController]
    public class StatsController : ControllerBase
    {
        private readonly IAuditLogService _auditLogService;
        private readonly IStatsService _statsService;
        public StatsController( IStatsService statsService)
        {
            _statsService = statsService;
        }


        [Authorize(Roles = "Administrator")]
        [HttpGet("in-progress")]
        public async Task<IActionResult> GetInProgress()
        {
            return Ok(await _statsService.GetInProgressasync());
        }
    }
}
