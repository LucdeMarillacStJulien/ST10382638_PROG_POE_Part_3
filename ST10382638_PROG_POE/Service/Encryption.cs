// =====================================================================================
// Name: Luc de Marillac St Julien
// Student Number: ST10382638
// Group: 1
//
// References:
//   1) Project outline/instructions: https://chatgpt.com/c/68f2c6ad-b79c-832c-97a1-59b6f53334a9
//   2) C# Reference & Tutorials:   https://www.w3schools.com/cs/index.php
// =====================================================================================

using Microsoft.IdentityModel.Tokens;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace ST10382638_PROG_POE.Service
{
    /// <summary>
    /// Provides AES-based stream/file encryption and decryption helpers for
    /// claim supporting documents. Files are encrypted with a random IV per file,
    /// stored as a 16-byte prefix followed by ciphertext. Keys are provided
    /// via <c>IConfiguration</c> and must decode to 32 bytes (AES-256).
    /// </summary>
    public class Encryption
    {
        // Configuration path for the Base64-encoded AES key (32 bytes required).
        private const string KeyPath = "Security:ClaimDocsKey";

        // Retrieves and validates the AES key from configuration:
        // - Must exist at KeyPath
        // - Must be Base64-decodable
        // - Must decode to exactly 32 bytes (256-bit)
        private static byte[] GetKey(IConfiguration config)
        {
            var b64 = config[KeyPath];
            if (string.IsNullOrWhiteSpace(b64))
                throw new InvalidOperationException($"Missing config key at '{KeyPath}' in appsettings.json.");

            byte[] key;
            try { key = Convert.FromBase64String(b64); }
            catch (FormatException) { throw new InvalidOperationException($"'{KeyPath}' must be Base64 of 32 bytes."); }

            if (key.Length != 32)
                throw new InvalidOperationException($"'{KeyPath}' must decode to 32 bytes (got {key.Length}).");

            return key;
        }

        /// <summary>
        /// Encrypts bytes from <paramref name="input"/> directly to <paramref name="outputPath"/> (.enc).
        /// Output file layout: first 16 bytes are the random IV, followed by AES-CBC ciphertext.
        /// No plaintext is written to disk.
        /// </summary>
        /// <param name="input">Input stream to read plaintext from.</param>
        /// <param name="outputPath">Destination file path for the encrypted output (.enc).</param>
        /// <param name="config">Configuration that supplies the Base64 AES key at <c>Security:ClaimDocsKey</c>.</param>
        /// <param name="ct">Cancellation token for cooperative cancellation.</param>
        public static async Task EncryptStreamAsync(Stream input, string outputPath, IConfiguration config, CancellationToken ct = default)
        {
            // Ensure destination directory exists.
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            // Open destination file (exclusive) to write encrypted content.
            using var fsOut = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);

            // Configure AES-256/CBC/PKCS7 with a fresh random IV per file.
            using var aes = Aes.Create();
            aes.Key = GetKey(config);
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.GenerateIV();

            // Write the IV prefix so decryption can recover it later.
            await fsOut.WriteAsync(aes.IV.AsMemory(0, aes.IV.Length), ct);

            // Stream-encrypt directly from input -> CryptoStream -> file, avoiding temp plaintext files.
            using var enc = aes.CreateEncryptor(aes.Key, aes.IV);
            using var crypto = new CryptoStream(fsOut, enc, CryptoStreamMode.Write, leaveOpen: false);
            await input.CopyToAsync(crypto, ct);
            await crypto.FlushAsync(ct);
        }

        /// <summary>
        /// Decrypts an encrypted file produced by <see cref="EncryptStreamAsync"/> into <paramref name="output"/>.
        /// Reads the first 16 bytes as IV, then streams AES-CBC decrypted plaintext to the provided output stream.
        /// </summary>
        /// <param name="encPath">Path to the encrypted .enc file.</param>
        /// <param name="output">Destination stream to receive decrypted plaintext.</param>
        /// <param name="config">Configuration that supplies the Base64 AES key at <c>Security:ClaimDocsKey</c>.</param>
        /// <param name="ct">Cancellation token for cooperative cancellation.</param>
        public static async Task DecryptToStreamAsync(string encPath, Stream output, IConfiguration config, CancellationToken ct = default)
        {
            // Open encrypted file for reading and extract the IV prefix.
            using var fsIn = new FileStream(encPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var iv = new byte[16];
            var read = await fsIn.ReadAsync(iv.AsMemory(0, 16), ct);
            if (read != 16) throw new InvalidDataException("Encrypted file missing IV.");

            // Configure AES-256/CBC/PKCS7 using the recovered IV and configured key.
            using var aes = Aes.Create();
            aes.Key = GetKey(config);
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.IV = iv;

            // Stream-decrypt directly into the caller-provided output stream.
            using var dec = aes.CreateDecryptor(aes.Key, aes.IV);
            using var crypto = new CryptoStream(fsIn, dec, CryptoStreamMode.Read, leaveOpen: false);
            await crypto.CopyToAsync(output, ct);
        }
    }
}
