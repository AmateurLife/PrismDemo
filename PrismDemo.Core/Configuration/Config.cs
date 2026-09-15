using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace PrismDemo.Core.Configuration
{
    public static class Config
    {
        private static IConfigurationRoot _config;

        public static IConfigurationRoot Configuration => _config;

        private static string ValueOrNull(string value) =>
            string.IsNullOrEmpty(value) ? null : value;

        public static string DatabaseConnectionString =>
            ValueOrNull(_config?["Database:ConnectionString"])
            ?? "Server=localhost;Database=PrismDemoDB;User Id=sa;Password=Demo@1234;TrustServerCertificate=True";

        public static string DatabaseBackupPath =>
            ValueOrNull(_config?["Database:BackupPath"])
            ?? "%Desktop%";

        public static string OpcEndpoint =>
            ValueOrNull(_config?["Opc:Endpoint"])
            ?? "opc.tcp://127.0.0.1:4840";

        public static string LogPath =>
            ValueOrNull(_config?["Logging:LogPath"]);

        public static string SettingPassword =>
            ValueOrNull(_config?["SettingPassword:Password"])
            ?? "Demo@1234";

        public static void Initialize()
        {
            (_config as IDisposable)?.Dispose();

            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("core.config.json", optional: true, reloadOnChange: true);

            _config = builder.Build();
        }

        public static void Initialize(string basePath)
        {
            (_config as IDisposable)?.Dispose();

            var builder = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("core.config.json", optional: true, reloadOnChange: true);

            _config = builder.Build();
        }

        public static IConfigurationRoot LoadModuleConfig(string moduleDirectory, string configFileName)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(moduleDirectory)
                .AddJsonFile(configFileName, optional: true, reloadOnChange: true);

            return builder.Build();
        }
    }
}
