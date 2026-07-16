// File: ViewModels/Windows/AGR_ComponentPropertyEditVM.cs
using Agrovent.ViewModels.Base;
using AgroventInfrastructure.Entities.Components;

namespace Agrovent.ViewModels.Windows
{
    /// <summary>Редактируемая строка свойства компонента (аналог IXProperty, но из БД).</summary>
    public class AGR_ComponentPropertyEditVM : BaseViewModel
    {
        private readonly ComponentProperty _entity;
        public ComponentProperty Entity => _entity;

        public AGR_ComponentPropertyEditVM(ComponentProperty entity)
        {
            _entity = entity;
            _name = entity.Name;
            _value = entity.Value;
        }

        private string _name;
        public string Name
        {
            get => _name;
            set { Set(ref _name, value); _entity.Name = value; }
        }

        private string _value;
        public string Value
        {
            get => _value;
            set { Set(ref _value, value); _entity.Value = value; }
        }

        public string Unit => string.Empty; // в БД не хранится, колонка в XAML и так Collapsed
    }
}