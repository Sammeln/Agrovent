using Microsoft.EntityFrameworkCore;
using Agrovent.DAL.Entities;
using Agrovent.DAL.Entities.Components;

namespace Agrovent.DAL
{
    public class DataContext : DbContext
    {
        public DbSet<Component> Components { get; set; }
        public DbSet<ComponentVersion> ComponentVersions { get; set; }
        public DbSet<ComponentProperty> ComponentProperties { get; set; }
        public DbSet<ComponentMaterial> ComponentMaterials { get; set; }
        public DbSet<AssemblyStructure> AssemblyStructures { get; set; }
        public DbSet<ComponentFile> ComponentFiles { get; set; }
        public DbSet<AvaArticleModel> AvaArticles { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql($"Host=localhost;Database=PRPMDB;Username=user;Password=sauser#1;");
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Компоненты - уникальный PartNumber
            modelBuilder.Entity<Component>()
                .HasIndex(c => c.PartNumber)
                .IsUnique();

            // Версии компонентов - уникальная комбинация ComponentId + Version
            modelBuilder.Entity<ComponentVersion>()
                .HasIndex(cv => new { cv.ComponentId, cv.Version })
                .IsUnique();

            // Хеш-сумма должна быть уникальной для версии
            modelBuilder.Entity<ComponentVersion>()
                .HasIndex(cv => cv.HashSum)
                .IsUnique();

            // Связи
            modelBuilder.Entity<Component>()
                .HasMany(c => c.Versions)
                .WithOne(cv => cv.Component)
                .HasForeignKey(cv => cv.ComponentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ComponentVersion>()
                .HasMany(cv => cv.Properties)
                .WithOne(p => p.ComponentVersion)
                .HasForeignKey(p => p.ComponentVersionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ComponentVersion>()
                .HasOne(cv => cv.Material)
                .WithOne(m => m.ComponentVersion)
                .HasForeignKey<ComponentMaterial>(m => m.ComponentVersionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ComponentVersion>()
                .HasMany(cv => cv.Files)
                .WithOne(f => f.ComponentVersion)
                .HasForeignKey(f => f.ComponentVersionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Структура сборки - самореференциальная связь
            modelBuilder.Entity<AssemblyStructure>()
                .HasOne(a => a.AssemblyVersion)
                .WithMany()
                .HasForeignKey(a => a.AssemblyVersionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AssemblyStructure>()
                .HasOne(a => a.ComponentVersion)
                .WithMany()
                .HasForeignKey(a => a.ComponentVersionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AssemblyStructure>()
                .HasOne(a => a.ParentStructure)
                .WithMany(a => a.ChildStructures)
                .HasForeignKey(a => a.ParentStructureId)
                .OnDelete(DeleteBehavior.Cascade);

            // Индекс для быстрого поиска структуры по сборке
            modelBuilder.Entity<AssemblyStructure>()
                .HasIndex(a => a.AssemblyVersionId);

            // Индекс для быстрого поиска где используется компонент
            modelBuilder.Entity<AssemblyStructure>()
                .HasIndex(a => a.ComponentVersionId);

            modelBuilder.Entity<AvaArticleModel>()
                .HasKey(a => a.Article);
        }

    }
}
