// File: DAL/Entities/TechProcess/Workstation.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Agrovent.DAL.Entities.TechnologicalProcess;

namespace Agrovent.DAL.Entities.TechProcess
{
    [Table("Workstations")]
    public class Workstation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        // Обратная навигация к операциям
        public virtual ICollection<Operation> Operations { get; set; } = new List<Operation>();
    }
}