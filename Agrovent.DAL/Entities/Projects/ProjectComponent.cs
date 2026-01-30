// File: DAL/Entities/Projects/ProjectComponent.cs
using Agrovent.DAL.Entities.Components;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Agrovent.DAL.Entities.Projects
{
    [Table("ProjectComponents")]
    public class ProjectComponent
    {
        [Key]
        public int Id { get; set; }

        // Внешний ключ на проект
        [Required]
        public int ProjectId { get; set; }
        [ForeignKey(nameof(ProjectId))]
        public virtual Project Project { get; set; } = null!;

        // Внешний ключ на версию компонента
        [Required]
        public int ComponentVersionId { get; set; }
        [ForeignKey(nameof(ComponentVersionId))]
        public virtual ComponentVersion ComponentVersion { get; set; } = null!;
    }
}