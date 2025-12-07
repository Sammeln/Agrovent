namespace Agrovent.Infrastructure.Interfaces
{
    public interface IAGR_AvaArticleModel
    {
        int Article { get; set; }
        string Brand { get; set; }
        string Company { get; set; }
        decimal? Count { get; set; }
        string Folder { get; set; }
        string Name { get; set; }
        string? PartNumber { get; set; }
        string Type { get; set; }
        string UOM { get; set; }
    }
}