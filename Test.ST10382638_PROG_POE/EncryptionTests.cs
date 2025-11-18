using Microsoft.Extensions.Configuration;
using ST10382638_PROG_POE.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test.ST10382638_PROG_POE
{
    [TestClass]
    public class EncryptionTests
    {
        [TestMethod]
        public async Task EncryptDecrypt_RoundTrip_Works()
        {
            var config = TestHelpers.MakeConfigWithKey();
            var plain = "Hello, CMCS!";
            var tmpEnc = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.enc");

            using (var src = new MemoryStream(Encoding.UTF8.GetBytes(plain)))
            {
                await Encryption.EncryptStreamAsync(src, tmpEnc, config);
            }

            Assert.IsTrue(File.Exists(tmpEnc), "Encrypted file not written.");

            using var outMs = new MemoryStream();
            await Encryption.DecryptToStreamAsync(tmpEnc, outMs, config);
            var round = Encoding.UTF8.GetString(outMs.ToArray());

            Assert.AreEqual(plain, round);
            File.Delete(tmpEnc);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public async Task Encryption_Throws_WhenKeyMissing()
        {
            var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes("hi"));
            var tmp = Path.GetTempFileName();
            try { await Encryption.EncryptStreamAsync(ms, tmp, cfg); }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }
        }
    }
}
