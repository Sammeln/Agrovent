using Microsoft.Extensions.Configuration;

namespace Agrovent.Infrastructure
{
    public static class AGR_Options
    {
        public static string OldStorageRootFolderPath { get => @"\\192.168.10.1\kd\!StorageRootFolder"; }
        public static string OldProductionRootFolderPath { get => @"\\192.168.10.1\kd\Listogib\!TestRootFolder"; }
        public static string StorageRootFolderPath { get; set; }
        public static string ProductionRootFolderPath { get; set; }
        public static string LocalWorkFolder { get; set; }

        public static void Initialize(IConfiguration configuration)
        {
            var section = configuration.GetSection("Storage");

            StorageRootFolderPath = section["StorageRootFolderPath"];
            ProductionRootFolderPath = section["ProductionRootFolderPath"];
            LocalWorkFolder = section["LocalWorkFolder"];
        }
    }
}
