using Microsoft.Extensions.Configuration;
using System.IO;

namespace KuisBolaBot.WebJob
{
    public static class Configuration
    {
        private static IConfigurationRoot instance;

        public static IConfigurationRoot Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = BuildConfiguration();
                }
                return instance;
            }
        }

        private static IConfigurationRoot BuildConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json");

            return builder.Build();
        }
    }
}
