using Microsoft.EntityFrameworkCore;
using Agrovent.DAL.Entities;
using Agrovent.DAL.Entities.Components;
using Microsoft.Extensions.Configuration;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.DAL.Entities.TechnologicalProcess;

namespace Agrovent.DAL
{
    public class DataContext : DbContext
    {
        private readonly IConfiguration _configuration;
        
        #region DataSet
        public DbSet<Component> Components { get; set; }
        public DbSet<ComponentVersion> ComponentVersions { get; set; }
        public DbSet<ComponentProperty> ComponentProperties { get; set; }
        public DbSet<ComponentMaterial> ComponentMaterials { get; set; }
        public DbSet<AssemblyStructure> AssemblyStructures { get; set; }
        public DbSet<ComponentFile> ComponentFiles { get; set; }
        public DbSet<AvaArticleModel> AvaArticles { get; set; } 
        #endregion

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var connectionString = _configuration?.GetConnectionString("DefaultConnection") ?? "Host=192.168.15.200;Port=5432;Database=PRPMDB2;Username=user;Password=sauser#1;";
                optionsBuilder.UseNpgsql(connectionString);
            }
            //optionsBuilder.UseNpgsql($"Host=localhost;Database=PRPMDB;Username=user;Password=sauser#1;");
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
            modelBuilder.Entity<AssemblyStructure>(entity =>
            {
                // Связь с версией сборки
                entity.HasOne(a => a.AssemblyVersion)
                    .WithMany()
                    .HasForeignKey(a => a.AssemblyVersionId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Связь с версией компонента
                entity.HasOne(a => a.ComponentVersion)
                    .WithMany()
                    .HasForeignKey(a => a.ComponentVersionId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Связь с родительской структурой
                entity.HasOne(a => a.ParentStructure)
                    .WithMany(a => a.ChildStructures)
                    .HasForeignKey(a => a.ParentStructureId)
                    .OnDelete(DeleteBehavior.Restrict); // Изменяем на Restrict или SetNull

                // Индексы
                entity.HasIndex(a => a.AssemblyVersionId);
                entity.HasIndex(a => a.ComponentVersionId);
                entity.HasIndex(a => a.ParentStructureId);
            });

            // Индекс для быстрого поиска структуры по сборке
            modelBuilder.Entity<AssemblyStructure>()
                .HasIndex(a => a.AssemblyVersionId);

            // Индекс для быстрого поиска где используется компонент
            modelBuilder.Entity<AssemblyStructure>()
                .HasIndex(a => a.ComponentVersionId);

            modelBuilder.Entity<AvaArticleModel>()
                .HasKey(a => a.Article);

            // Связь ComponentVersion с AvaArticle
            modelBuilder.Entity<ComponentVersion>()
                .HasOne(cv => cv.AvaArticle)
                .WithMany()
                .HasForeignKey(cv => cv.AvaArticleArticle)
                .OnDelete(DeleteBehavior.Restrict);

            // TechnologicalProcess конфигурация
            modelBuilder.Entity<TechnologicalProcess>(entity =>
            {
                entity.HasIndex(tp => tp.PartNumber)
                    .IsUnique();

                entity.HasOne(tp => tp.Component)
                    .WithMany()
                    .HasForeignKey(tp => tp.PartNumber)
                    .HasPrincipalKey(c => c.PartNumber)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(tp => tp.Operations)
                    .WithOne(o => o.TechnologicalProcess)
                    .HasForeignKey(o => o.TechnologicalProcessId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Operation>(entity =>
            {
                entity.HasIndex(o => o.TechnologicalProcessId);
                entity.HasIndex(o => new { o.TechnologicalProcessId, o.SequenceNumber })
                    .IsUnique();
            });

        }


        public DataContext()
        {
                
        }

        public DataContext(IConfiguration configuration)
        {
            _configuration = configuration;
        }
    }
}
