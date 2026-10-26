namespace Agrovent.DAL.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using AgroventInfrastructure.Interfaces.Components;
    using AgroventInfrastructure.Interfaces.Components.Base;

    public enum AGR_VersionAction_e
    {
        /// <summary>Новая версия max+1 (мой замок, или файла без маркера = новый компонент).</summary>
        NewVersion,
        /// <summary>Ссылка на существующую версию (неотмеченный компонент с маркером, покупной).</summary>
        UseExisting
    }

    /// <summary>Что делать с одним компонентом при сохранении. Решение принимается до транзакции.</summary>
    public class AGR_VersionPlanItem
    {
        public AGR_VersionAction_e Action { get; init; }
        /// <summary>Для NewVersion: BaseVersion из замка (null у нового компонента).</summary>
        public int? BasedOnVersion { get; init; }
        /// <summary>Для UseExisting.</summary>
        public int ComponentId { get; init; }
        /// <summary>Для UseExisting.</summary>
        public int Version { get; init; }
        /// <summary>Для NewVersion по замку: какой замок снять в той же транзакции.</summary>
        public int? LockedComponentId { get; init; }
        /// <summary>VM, по которой строился план (её документ получает маркер).</summary>
        public IAGR_BaseComponent Component { get; init; } = null!;
        /// <summary>Итог в БД: созданная или найденная версия (заполняет репозиторий).</summary>
        public AgroventInfrastructure.Entities.Components.ComponentVersion? Saved { get; set; }

        /// <summary>Отпечатки файла на момент построения плана (NewVersion и PropertiesChanged):
        /// одни и те же значения идут в БД и в маркер.</summary>
        public string? GeometryFingerprint { get; init; }
        public string? PropertiesFingerprint { get; init; }

        /// <summary>UseExisting: геометрия совпала с маркером, а отпечаток свойств нет. Свойства версии обновляются в БД,
        /// файл версии в хранилище перезаписывается (с новым маркером), новая версия не создаётся.</summary>
        public bool PropertiesChanged { get; init; }

        /// <summary>UseExisting + PropertiesChanged: свойства файла (конфигурация, иначе Custom файла) для записи в БД.</summary>
        public IReadOnlyDictionary<string, string>? FileProperties { get; init; }

        /// <summary>UseExisting покупного без маркера: записать маркер (если файл доступен на запись), ничего не копировать.</summary>
        public bool MarkerOnly { get; init; }
    }

    /// <summary>План сохранения: ключ = путь файла + конфигурация (как группирует спецификация).</summary>
    public class AGR_SavePlan
    {
        private readonly Dictionary<string, AGR_VersionPlanItem> _items = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, AGR_VersionPlanItem> Items => _items;

        public static string KeyOf(IAGR_BaseComponent component)
        {
            var path = (component as IAGR_HasFile)?.CurrentModelFilePath;
            if (string.IsNullOrEmpty(path)) path = component.SwDocument?.Path ?? "";
            if (!string.IsNullOrEmpty(path)) path = Path.GetFullPath(path);
            return path + "|" + component.ConfigName;
        }

        public bool Contains(IAGR_BaseComponent component) => _items.ContainsKey(KeyOf(component));

        public void Set(IAGR_BaseComponent component, AGR_VersionPlanItem item) => _items[KeyOf(component)] = item;

        public AGR_VersionPlanItem Get(IAGR_BaseComponent component)
            => _items.TryGetValue(KeyOf(component), out var item)
                ? item
                : throw new InvalidOperationException($"Компонент {component.Name} ({component.PartNumber}) отсутствует в плане сохранения.");
    }

    /// <summary>Существующий покупной компонент, подходящий под файл без маркера (6.5).</summary>
    public class AGR_PurchasedCandidate
    {
        public int ComponentId { get; init; }
        public string PartNumber { get; init; } = "";
        public int Version { get; init; }
        public string Name { get; init; } = "";
        public int? Article { get; init; }
        public string? ArticleName { get; init; }

        public override string ToString()
            => $"{PartNumber} v{Version}  {Name}" + (Article.HasValue ? $"  арт. {Article} {ArticleName}" : "");
    }
}
