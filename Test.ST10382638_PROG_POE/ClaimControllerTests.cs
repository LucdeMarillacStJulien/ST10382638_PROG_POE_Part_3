using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ST10382638_PROG_POE.Controllers;
using ST10382638_PROG_POE.Data;
using ST10382638_PROG_POE.Models;
using ST10382638_PROG_POE.Service;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.ST10382638_PROG_POE
{
    [TestClass]
    public class ClaimControllerTests
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
        public async Task Create_InvalidFileType_AddsModelErrorAndReturnsView()
        {
            var ctx = TestHelpers.NewSeededContext(out var lecturerUser, out var lecturer);
            var config = TestHelpers.MakeConfigWithKey();
            var svc = new ClaimDownload(config, ctx);
            var sut = new ClaimController(ctx, config, svc);

            var claim = new Claim
            {
                LecturerProfileId = lecturer.LecturerProfileId,
                HoursWorked = 4,
                RateAtSubmission = lecturer.HourlyRate
            };

            var files = new List<IFormFile>
            {
                MakeFormFile("bad.exe", "application/octet-stream", Encoding.UTF8.GetBytes("boom"))
            };

            var result = await sut.Create(claim, files);

            // Should return the same View with model error
            Assert.IsInstanceOfType(result, typeof(ViewResult));
            Assert.IsFalse(sut.ModelState.IsValid);

            var errors = sut.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            Assert.IsTrue(
                errors.Any(e => e.Contains("invalid file type", StringComparison.OrdinalIgnoreCase)),
                "Expected an invalid file type error message.");

            Assert.AreEqual(0, await ctx.Claim.CountAsync());
        }

        [TestMethod]
        public async Task Create_ValidSubmission_SavesClaimAndEncryptedDocs()
        {
            var ctx = TestHelpers.NewSeededContext(out var lecturerUser, out var lecturer);
            var config = TestHelpers.MakeConfigWithKey();
            TestHelpers.MakeTempWorkFolder();
            var svc = new ClaimDownload(config, ctx);
            var sut = new ClaimController(ctx, config, svc);

            var pdf = MakeFormFile("proof.pdf", "application/pdf", Encoding.UTF8.GetBytes("PDFDATA"));
            var docx = MakeFormFile(
                "form.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                Encoding.UTF8.GetBytes("DOCXDATA"));

            var files = new List<IFormFile> { pdf, docx };

            var claim = new Claim
            {
                LecturerProfileId = lecturer.LecturerProfileId,
                HoursWorked = 3.5,
                RateAtSubmission = lecturer.HourlyRate
            };

            var result = await sut.Create(claim, files);

            // Should redirect on success
            Assert.IsInstanceOfType(result, typeof(RedirectToActionResult));

            var saved = await ctx.Claim
                .Include(c => c.SupportingDocs)
                .FirstOrDefaultAsync();

            Assert.IsNotNull(saved);
            Assert.AreEqual("Pending", saved!.Status);
            Assert.AreEqual(saved.HoursWorked * saved.RateAtSubmission, saved.CalculatedAmount, 0.0001);
            Assert.IsTrue(saved.SupportingDocs.Count >= 2, "Expected at least two saved docs");

            // Ensure .enc files exist on disk
            foreach (var d in saved.SupportingDocs)
            {
                var fullPath = Path.Combine(Directory.GetCurrentDirectory(), d.FileUrl!);
                Assert.IsTrue(File.Exists(fullPath), $"Encrypted file missing: {d.FileUrl}");
                Assert.IsTrue(d.FileUrl!.EndsWith(".enc", StringComparison.OrdinalIgnoreCase));
            }
        }

        [TestMethod]
        public async Task Verify_SetsStatusVerified_WhenPending()
        {
            var ctx = TestHelpers.NewSeededContext(out var lecturerUser, out var lecturer);
            var config = TestHelpers.MakeConfigWithKey();
            var svc = new ClaimDownload(config, ctx);
            var sut = new ClaimController(ctx, config, svc);

            var claim = new Claim
            {
                LecturerProfileId = lecturer.LecturerProfileId,
                HoursWorked = 1,
                RateAtSubmission = 100,
                Status = "Pending",
                CalculatedAmount = 100
            };

            ctx.Claim.Add(claim);
            await ctx.SaveChangesAsync();

            var res = await sut.Verify(claim.ClaimId);
            Assert.IsInstanceOfType(res, typeof(RedirectToActionResult));

            var updated = await ctx.Claim.FindAsync(claim.ClaimId);
            Assert.AreEqual("Verified", updated!.Status);
        }

        [TestMethod]
        public async Task Reject_SetsStatusRejected_WhenPending()
        {
            var ctx = TestHelpers.NewSeededContext(out var lecturerUser, out var lecturer);
            var config = TestHelpers.MakeConfigWithKey();
            var svc = new ClaimDownload(config, ctx);
            var sut = new ClaimController(ctx, config, svc);

            var claim = new Claim
            {
                LecturerProfileId = lecturer.LecturerProfileId,
                HoursWorked = 1,
                RateAtSubmission = 100,
                Status = "Pending",
                CalculatedAmount = 100
            };

            ctx.Claim.Add(claim);
            await ctx.SaveChangesAsync();

            var res = await sut.Reject(claim.ClaimId);
            Assert.IsInstanceOfType(res, typeof(RedirectToActionResult));

            var updated = await ctx.Claim.FindAsync(claim.ClaimId);
            Assert.AreEqual("Rejected", updated!.Status);
        }

        [TestMethod]
        public async Task Approve_OnlyAllowsApprovedWhenVerified()
        {
            var ctx = TestHelpers.NewSeededContext(out var lecturerUser, out var lecturer);
            var config = TestHelpers.MakeConfigWithKey();
            var svc = new ClaimDownload(config, ctx);
            var sut = new ClaimController(ctx, config, svc);

            var claim = new Claim
            {
                LecturerProfileId = lecturer.LecturerProfileId,
                HoursWorked = 1,
                RateAtSubmission = 100,
                Status = "Pending",
                CalculatedAmount = 100
            };

            ctx.Claim.Add(claim);
            await ctx.SaveChangesAsync();

            // Not verified -> BadRequest
            var bad = await sut.Approve(claim.ClaimId);
            Assert.IsInstanceOfType(bad, typeof(BadRequestObjectResult));

            // Now verify then approve
            claim.Status = "Verified";
            await ctx.SaveChangesAsync();

            var ok = await sut.Approve(claim.ClaimId);
            Assert.IsInstanceOfType(ok, typeof(RedirectToActionResult));

            var updated = await ctx.Claim.FindAsync(claim.ClaimId);
            Assert.AreEqual("Approved", updated!.Status);
        }

        [TestMethod]
        public async Task Create_FileOver10MB_AddsModelErrorAndReturnsView()
        {
            var ctx = TestHelpers.NewSeededContext(out var lecturerUser, out var lecturer);
            var config = TestHelpers.MakeConfigWithKey();
            TestHelpers.MakeTempWorkFolder();
            var svc = new ClaimDownload(config, ctx);
            var sut = new ClaimController(ctx, config, svc);

            // 10MB + 1 byte
            var big = new byte[10 * 1024 * 1024 + 1];
            var file = new FormFile(new MemoryStream(big), 0, big.Length, "proof.pdf", "proof.pdf")
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/pdf"
            };

            var claim = new Claim
            {
                LecturerProfileId = lecturer.LecturerProfileId,
                HoursWorked = 1,
                RateAtSubmission = lecturer.HourlyRate
            };

            var result = await sut.Create(claim, new List<IFormFile> { file });

            Assert.IsInstanceOfType(result, typeof(ViewResult));
            Assert.IsFalse(sut.ModelState.IsValid);

            var errors = sut.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            Assert.IsTrue(
                errors.Any(e => e.Contains("bigger than the maximum allowed size", StringComparison.OrdinalIgnoreCase)),
                "Expected file size error message.");

            Assert.AreEqual(0, ctx.SupportingDoc.Count());
            Assert.AreEqual(0, ctx.Claim.Count());
        }
    }
}
