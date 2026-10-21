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
}
