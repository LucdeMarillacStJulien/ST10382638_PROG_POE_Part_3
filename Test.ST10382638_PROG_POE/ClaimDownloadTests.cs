using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ST10382638_PROG_POE.Controllers;
using ST10382638_PROG_POE.Models;
using ST10382638_PROG_POE.Service;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.ST10382638_PROG_POE
{
    [TestClass]
    public class ClaimDownloadTests
    {
        private static IFormFile MakeFormFile(string name, string contentType, byte[] data)
        {
            var stream = new MemoryStream(data);
            return new FormFile(stream, 0, data.Length, name, name)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
        }

        [TestMethod]
        public async Task BuildDecryptedZipAsync_ReturnsZipWithFilesAtRoot()
        {
            var ctx = TestHelpers.NewSeededContext(out var lecturerUser, out var lecturer);
            var config = TestHelpers.MakeConfigWithKey();
            TestHelpers.MakeTempWorkFolder();

            var sutDownload = new ClaimDownload(config, ctx);
            var sutController = new ClaimController(ctx, config, sutDownload);

            // Create a real claim via controller to produce encrypted docs
            var claim = new Claim
            {
                LecturerProfileId = lecturer.LecturerProfileId,
                HoursWorked = 2,
                RateAtSubmission = lecturer.HourlyRate
            };

            var files = new List<IFormFile>
            {
                MakeFormFile("proof.pdf", "application/pdf", Encoding.UTF8.GetBytes("PDFDATA")),
                MakeFormFile("report.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", Encoding.UTF8.GetBytes("XLSXDATA"))
            };

            var res = await sutController.Create(claim, files);
            Assert.IsInstanceOfType(res, typeof(Microsoft.AspNetCore.Mvc.RedirectToActionResult));

            var tuple = await sutDownload.BuildDecryptedZipAsync(claim.ClaimId);
            Assert.IsNotNull(tuple, "Expected a non-null zip result");

            var (zipStream, fileName) = tuple!.Value;
            Assert.IsTrue(fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

            // Inspect zip entries
            using var mem = new MemoryStream();
            await zipStream.CopyToAsync(mem);
            mem.Position = 0;

            using var zip = new ZipArchive(mem, ZipArchiveMode.Read, leaveOpen: false);
            Assert.IsTrue(zip.Entries.Count >= 2, "Expected at least two entries in zip");

            // Ensure files are at root (no folders)
            foreach (var e in zip.Entries)
            {
                Assert.IsFalse(e.FullName.Contains("/"), "Zip should not contain folder structure");
            }
        }
    }
}
