using AgroventInfrastructure.Interfaces;

namespace AgroventInfrastructure.Entities.Components
{
    public class AvaArticleModel : IAGR_AvaArticleModel
    {
        public int Article { get; set; }
        public string? Name { get; set; }
        public string? PartNumber { get; set; }
        public decimal? Count { get; set; }
        public string? MainUOM { get; set; }
        public string? Type { get; set; }
        public string? Folder { get; set; }
        public string? Brand { get; set; }
        public string? Company { get; set; }
        public string? SecondaryUOM { get; set; }
        public string? ArchiveType { get; set; }
    }
}
