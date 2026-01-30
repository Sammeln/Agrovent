// File: DAL/Entities/Projects/Project.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Agrovent.DAL.Entities.Projects
{
    [Table("Projects")]
    public class Project
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        // Внешний ключ на родительский проект (для вложенности)
        public int? ParentId { get; set; }
        [ForeignKey(nameof(ParentId))]
        public virtual Project? Parent { get; set; }

        // Обратная навигация к дочерним проектам
        public virtual ICollection<Project> Children { get; set; } = new List<Project>();

        // Обратная навигация к компонентам в проекте
        public virtual ICollection<ProjectComponent> ProjectComponents { get; set; } = new List<ProjectComponent>();
    }
}