using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ST10382638_PROG_POE.Data;
using ST10382638_PROG_POE.Models;
using System;
using System.Collections.Generic;
using System.IO;
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

        /// <summary>
        /// Creates an in-memory AppDbContext seeded with a single lecturer user + profile,
        /// ready for controller/service tests.
        /// </summary>
        public static AppDbContext NewSeededContext(out ApplicationUser lecturerUser, out LecturerProfile lecturer)
        {
            // Build in-memory options so EF Core uses a temp database in RAM
            var options = InMemoryOptions();

            // Create a fresh AppDbContext instance
            var ctx = new AppDbContext(options);

            // Fake lecturer identity user (ApplicationUser)
            lecturerUser = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                FirstName = "Luc",
                Surname = "De Marillac",
                Email = "lecturer@example.com",
                UserName = "lecturer@example.com"
            };

            // LecturerProfile linked to that user
            lecturer = new LecturerProfile
            {
                LecturerProfileId = 1,
                UserId = lecturerUser.Id,
                HourlyRate = 500,
                IsAvailable = true,
                User = lecturerUser
            };

            // Seed both into the EF context
            ctx.Users.Add(lecturerUser);
            ctx.LecturerProfile.Add(lecturer);
            ctx.SaveChanges();

            return ctx;
        }

        public static string MakeTempWorkFolder()
        {
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "ClaimDocs");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
