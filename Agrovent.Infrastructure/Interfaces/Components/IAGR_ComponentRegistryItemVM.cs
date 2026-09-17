using System;
using System.Windows.Media.Imaging;

namespace AgroventInfrastructure.Interfaces.Components
{
    public interface IAGR_ComponentRegistryItemVM
    {
        string AvaTypeDisplay { get; }
        string ComponentTypeDisplay { get; }
        DateTime CreatedAt { get; }
        int Id { get; }
        string Name { get; }
        string PartNumber { get; }
        BitmapImage? Preview { get; }
        string SavedByUserInitials { get; }
        string StoragePath { get; }
        int Version { get; }
    }
}