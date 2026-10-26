using System;
using System.Collections.Generic;
using System.Windows.Documents;
using System.Windows.Forms.Design;
using AgroventInfrastructure.Enums;
using AgroventInfrastructure.Interfaces.Properties;
using AgroventInfrastructure.Entities.Components;
using AgroventInfrastructure.Interfaces.Entities.Components;
using Xarial.XCad.SolidWorks.Documents;

namespace AgroventInfrastructure.Interfaces.Components.Base
{
    public interface IAGR_BaseComponent : IAGR_BaseObject, IAGR_PageView
    {
        abstract ISwDocument3D SwDocument { get; }
        abstract string Name { get; }
        abstract string ConfigName { get; }
        abstract string Extension { get; }
        abstract string PartNumber { get; set; }
        abstract string Article { get; set; }
        abstract int Version { get; set; }
        /// <summary>Папка компонента в хранилище (Components.StorageGuid). null - компонента ещё нет в БД.</summary>
        abstract Guid? StorageGuid { get; set; }
        abstract byte[] Preview { get; }
        abstract IAGR_AvaArticleModel? AvaArticle { get; set; }
        abstract AGR_ComponentType_e ComponentType { get; set; }
        abstract AGR_AvaType_e AvaType { get; set; }
        abstract IAGR_PropertiesCollection PropertiesCollection { get; set; }
        abstract AGR_ComponentDatabaseState_e IsInDatabase { get; set; }
        /// <summary>Состояние редактирования в БД: свободен / у меня / у другого пользователя.</summary>
        abstract AGR_EditState_e EditState { get; set; }

        /// <summary>Кто держит замок (инициалы), если EditState == EditingByOther.</summary>
        abstract string? EditLockedBy { get; set; }

        /// <summary>Есть ли у текущего пользователя замок на этот компонент
        /// или (для сборки) на любой компонент внутри неё. Кэшированный флаг: читается
        /// в CommandStateResolve, поэтому обращаться к БД при чтении нельзя.</summary>
        abstract bool HasMyLocks { get; set; }
        abstract ComponentVersion? ComponentVersion { get; set; }

        abstract ICollection<IAGR_ComponentRegistryItemVM>? ParentAssemblies { get; set; }
        abstract int ParentAssembliesCount { get; }

        abstract bool CanUserEdit { get; }
        abstract void PrecomputePreview();

        abstract bool IsPurchased { get; }

        event EventHandler? PartnumberChanged;
    }
}
