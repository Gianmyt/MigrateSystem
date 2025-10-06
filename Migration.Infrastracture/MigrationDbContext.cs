using Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Migration.Infrastructure.models;
using System.Collections.Generic;

namespace Infrastructure
{
    public class MigrationDbContext : DbContext
    {
        public MigrationDbContext(DbContextOptions<MigrationDbContext> options) : base(options) { }

        public DbSet<UserMigration> UserMigrations => Set<UserMigration>();
        public DbSet<MigrationSlot> MigrationSlots => Set<MigrationSlot>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    }


}