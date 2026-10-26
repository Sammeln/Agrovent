using System;
using System.Collections.Generic;
using System.Linq;
using AgroventInfrastructure.Entities.Base;
using AgroventInfrastructure.Entities.TechProcess;
using AgroventInfrastructure.Interfaces.Entities.Components;
using AgroventInfrastructure.Interfaces.Entities.TechProcess;

namespace AgroventInfrastructure.Entities.Components
{
    public class Component : DateStampEntity
    {
        // Основной идентификатор (PartNumber)
        public string PartNumber { get; set; }

        // Папка компонента в хранилище: StorageRoot\<StorageGuid>\<версия> (выдаётся при первом сохранении)
        public Guid StorageGuid { get; set; }

        // Навигационные свойства
        public ICollection<ComponentVersion> Versions { get; set; } = new List<ComponentVersion>();
        public TechnologicalProcess? TechnologicalProcess { get; set; }

        // Метод для получения последней версии
        public ComponentVersion? GetLatestVersion()
            => Versions.OrderByDescending(v => v.Version).FirstOrDefault();
    }
}