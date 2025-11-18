using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ST10382638_PROG_POE.Data;
using ST10382638_PROG_POE.Models;
using System;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;
using ApplicationUser = ST10382638_PROG_POE.Models.ApplicationUser;

namespace Test.ST10382638_PROG_POE
{
    public sealed class TestHelpers
    {
        public static DbContextOptions<AppDbContext> InMemoryOptions(string? dbName = null)
        {
            return new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName ?? $"TestDb_{Guid.NewGuid()}")
                .Options;
        }

        public static IConfiguration MakeConfigWithKey()
        {
            var dict = new Dictionary<string, string?>
            {
                ["Security:ClaimDocsKey"] = "tQTyjao7dmWjzyCiWwH4Ddy5pL7QXKhxEfoqDWjP9uI="
            };
            return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        }

        


        public static string MakeTempWorkFolder()
        {
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "ClaimDocs");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
