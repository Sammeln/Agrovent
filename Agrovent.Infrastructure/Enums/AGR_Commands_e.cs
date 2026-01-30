using Xarial.XCad.Base.Attributes;
using Xarial.XCad.UI.Commands.Attributes;

namespace Agrovent.Infrastructure.Enums
{
    public enum AGR_Commands_e
    {
        [Title("Спецификация")]
        [CommandItemInfo(true, true, Xarial.XCad.UI.Commands.Enums.WorkspaceTypes_e.Assembly)]
        Command1,

        [Title("Экспорт в Iges")]
        ExportToIges,

        [Title("Сохранить")]
        SaveComponent,

        [Title("Обновить свойства")]
        UpdateProperties,

        [Title("Реестр КД")]
        ComponentRegistry,

        [Title("Проводник проектов")]
        ProjectsExplorer
    }
}