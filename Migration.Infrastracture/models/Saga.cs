using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Migration.Infrastructure.models
{
    public class SagaState
    {
        [Key]
        public Guid SagaId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string CurrentState { get; set; } = "Started";
        public string? ErrorMessage { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public record StartMigrationSaga(Guid SagaId, OldUser OldUser);
    public record UserValidated(Guid SagaId, OldUser OldUser);
    public record UserTransformed(Guid SagaId, NewUser NewUser);
    public record MigrationCompleted(Guid SagaId, string UserId);
    public record MigrationFailed(Guid SagaId, string UserId, string Reason);
}
