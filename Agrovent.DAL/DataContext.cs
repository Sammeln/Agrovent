using Microsoft.EntityFrameworkCore;
using Agrovent.DAL.Entities;
using Agrovent.DAL.Entities.Components;
using Microsoft.Extensions.Configuration;
using Agrovent.Infrastructure.Interfaces;
using Agrovent.DAL.Entities.TechProcess;
using Agrovent.DAL.Entities.Projects;

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
        public DbSet<Project> Projects { get; set; } 
        public DbSet<ProjectComponent> ProjectComponents { get; set; }

        public DbSet<Workstation> Workstations { get; set; }
        public DbSet<Operation> Operations { get; set; }
        public DbSet<TemplateOperation> TemplateOperations { get; set; }
        public DbSet<TechnologicalProcess> TechProcesses { get; set; }

        #endregion

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                //var connectionString = _configuration?.GetConnectionString("DefaultConnection") ?? "Host=192.168.15.200;Port=5432;Database=PRPMDB2;Username=user;Password=sauser#1;";
                var connectionString = _configuration?.GetConnectionString("DefaultConnection");
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

            modelBuilder.Entity<ComponentVersion>()
                .HasOne(cv => cv.Component)
                .WithMany(c => c.Versions)
                .HasForeignKey(cv => cv.ComponentId);

            // Структура сборки - самореференциальная связь
            modelBuilder.Entity<AssemblyStructure>(entity =>
            {
                // Связь с версией сборки
                entity.HasOne(a => a.ParentComponentVersion)
                    .WithMany()
                    .HasForeignKey(a => a.ParentComponentVersionId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Связь с версией компонента
                entity.HasOne(a => a.ChildComponentVersion)
                    .WithMany()
                    .HasForeignKey(a => a.ChildComponentVersionId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Индексы
                entity.HasIndex(a => a.ChildComponentVersionId);
                entity.HasIndex(a => a.ParentComponentVersionId);
            });

            // Индекс для быстрого поиска структуры по сборке
            modelBuilder.Entity<AssemblyStructure>()
                .HasIndex(a => a.ParentComponentVersionId);

            // Индекс для быстрого поиска где используется компонент
            modelBuilder.Entity<AssemblyStructure>()
                .HasIndex(a => a.ChildComponentVersionId);

            modelBuilder.Entity<AvaArticleModel>()
                .HasKey(a => a.Article);

            // Связь ComponentVersion с AvaArticle
            modelBuilder.Entity<ComponentVersion>()
                .HasOne(cv => cv.AvaArticle)
                .WithMany()
                .HasForeignKey(cv => cv.AvaArticleArticle)
                .OnDelete(DeleteBehavior.Restrict);

           modelBuilder.Entity<Operation>(entity =>
            {
                entity.HasIndex(o => o.TechnologicalProcessId);
                entity.HasIndex(o => new { o.TechnologicalProcessId, o.SequenceNumber })
                    .IsUnique();
            });

            // Конфигурация для Project и ProjectComponent
            modelBuilder.Entity<Project>()
                .HasMany(p => p.Children)
                .WithOne(p => p.Parent)
                .HasForeignKey(p => p.ParentId)
                .OnDelete(DeleteBehavior.Cascade); // Удалять дочерние при удалении родителя

            modelBuilder.Entity<ProjectComponent>()
                .HasOne(pc => pc.Project)
                .WithMany(p => p.ProjectComponents)
                .HasForeignKey(pc => pc.ProjectId)
                .OnDelete(DeleteBehavior.Cascade); // Удалять связь при удалении проекта

            modelBuilder.Entity<ProjectComponent>()
                .HasOne(pc => pc.ComponentVersion)
                .WithMany(cv => cv.ProjectComponents) // Предполагаем, что в ComponentVersion есть ProjectComponents
                .HasForeignKey(pc => pc.ComponentVersionId)
                .OnDelete(DeleteBehavior.Cascade); // Удалять связь при удалении компонента

            // Если в ComponentVersion нет ProjectComponents, добавьте его:
            modelBuilder.Entity<ComponentVersion>()
                .HasMany(cv => cv.ProjectComponents) // Добавляем навигационное свойство
                .WithOne(pc => pc.ComponentVersion)
                .HasForeignKey(pc => pc.ComponentVersionId);

            // --- Техпроцессы ---
            modelBuilder.Entity<Workstation>(entity =>
            {
                entity.HasKey(w => w.AvaId);
                entity.HasMany(d => d.TemplateOperations)
                    .WithOne(p => p.Workstation)
                    .HasForeignKey(d => d.WorkstationId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_TemplateOperation_Workstation");
            });

            modelBuilder.Entity<TechnologicalProcess>(entity =>
            {
                entity.HasOne(d => d.Component)
                    .WithOne(p => p.TechnologicalProcess)
                    .HasForeignKey<TechnologicalProcess>(d => d.PartNumber)
                    .HasPrincipalKey<Component>(p => p.PartNumber)
                    .OnDelete(DeleteBehavior.ClientSetNull) 
                    .HasConstraintName("FK_TechnologicalProcess_Component");

            });

            modelBuilder.Entity<Operation>(entity => 
            {
                entity.HasOne(d => d.TechnologicalProcess)
                    .WithMany(p => p.Operations) 
                    .HasForeignKey(d => d.TechnologicalProcessId)
                    .OnDelete(DeleteBehavior.ClientSetNull) 
                    .HasConstraintName("FK_Operation_TechnologicalProcess");
            });

            modelBuilder.Entity<TemplateOperation>(entity =>
            {
                entity.HasOne(d => d.Workstation)
                    .WithMany(p => p.TemplateOperations)
                    .HasForeignKey(d => d.WorkstationId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_TemplateOperation_Workstation");
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
