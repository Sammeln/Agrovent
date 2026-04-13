using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Agrovent.DAL.Entities.Base;
using Agrovent.DAL.Entities.Components;
using Agrovent.Infrastructure.Enums;

namespace Agrovent.DAL.Entities.TechProcess
{
    [Table("TechnologicalProcesses")]
    public class TechnologicalProcess : DateStampEntity
    {

        // Внешний ключ на компонент по PartNumber
        public string PartNumber { get; set; } = string.Empty;

        // Навигационное свойство к компоненту
        [ForeignKey(nameof(PartNumber))]
        public virtual Component Component { get; set; } = null!;

        // Коллекция операций
        public virtual ICollection<Operation> Operations { get; set; } = new List<Operation>();
    }
}