using System.Collections.Generic;
using AgroventInfrastructure.Enums;
using AgroventInfrastructure.Interfaces.Entities.Projects;

namespace AgroventInfrastructure.Interfaces.Entities.Components
{
    public interface IAGR_ComponentVersionEntity : IAGR_DateStampEntity
    {
        IAGR_AvaArticleModel? AvaArticle { get; set; }
        int? AvaArticleArticle { get; set; }
        AGR_AvaType_e AvaType { get; set; }
        IAGR_ComponentEntity Component { get; set; }
        int ComponentId { get; set; }
        AGR_ComponentType_e ComponentType { get; set; }
        string ConfigName { get; set; }
        ICollection<IAGR_ComponentFileEntity> Files { get; set; }
        int HashSum { get; set; }
        IAGR_ComponentMaterialEntity? Material { get; set; }
        string Name { get; set; }
        byte[] PreviewImage { get; set; }
        ICollection<IAGR_ProjectComponentEntity> ProjectComponents { get; set; }
        ICollection<IAGR_ComponentPropertyEntity> Properties { get; set; }
        IAGR_UserEntity SavedByUser { get; set; }
        int SavedByUserId { get; set; }
        int Version { get; set; }

        bool IsLatestVersion();
    }
}