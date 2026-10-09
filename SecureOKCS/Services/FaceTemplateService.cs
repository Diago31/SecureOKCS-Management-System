using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace SecureOKCS.Services
{
    public class FaceTemplateService
    {
        private const int DescriptorLength =
            1024;

        private readonly IDataProtector
            _protector;

        private readonly string
            _templateDirectory;

        public FaceTemplateService(
            IDataProtectionProvider
                dataProtectionProvider,
            IWebHostEnvironment environment)
        {
            _protector =
                dataProtectionProvider
                    .CreateProtector(
                        "SecureOKCS.FaceTemplates.v1");

            _templateDirectory =
                Path.Combine(
                    environment.ContentRootPath,
                    "App_Data",
                    "FaceTemplates");

            Directory.CreateDirectory(
                _templateDirectory);
        }

        // SIMPAN DESCRIPTOR WAJAH
        public async Task<(
            string Reference,
            string Hash)> SaveTemplateAsync(
                string descriptorJson)
        {
            float[] descriptor =
                ParseAndValidateDescriptor(
                    descriptorJson);

            string canonicalJson =
                JsonSerializer.Serialize(
                    descriptor);

            string encryptedPayload =
                _protector.Protect(
                    canonicalJson);

            string fileName =
                $"face_{Guid.NewGuid():N}.dat";

            string filePath =
                ResolveSafePath(
                    fileName);

            await File.WriteAllTextAsync(
                filePath,
                encryptedPayload,
                Encoding.UTF8);

            string hash =
                CalculateHash(
                    encryptedPayload);

            return (
                Reference: fileName,
                Hash: hash);
        }

        // BACA DAN DECRYPT DESCRIPTOR WAJAH
        public async Task<float[]>
            LoadTemplateAsync(
                string templateReference,
                string? expectedHash)
        {
            if (string.IsNullOrWhiteSpace(
                    expectedHash))
            {
                throw new InvalidOperationException(
                    "Face template hash " +
                    "tidak dijumpai.");
            }

            string filePath =
                ResolveSafePath(
                    templateReference);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    "Face template tidak dijumpai.");
            }

            string encryptedPayload =
                await File.ReadAllTextAsync(
                    filePath,
                    Encoding.UTF8);

            string actualHash =
                CalculateHash(
                    encryptedPayload);

            if (!HashesMatch(
                    expectedHash,
                    actualHash))
            {
                throw new CryptographicException(
                    "Face template telah berubah " +
                    "atau rosak.");
            }

            string descriptorJson =
                _protector.Unprotect(
                    encryptedPayload);

            return ParseAndValidateDescriptor(
                descriptorJson);
        }

        // PADAM TEMPLATE LAMA
        // SEMASA RE-ENROLL
        public void DeleteTemplate(
            string? templateReference)
        {
            if (string.IsNullOrWhiteSpace(
                    templateReference))
            {
                return;
            }

            string filePath =
                ResolveSafePath(
                    templateReference);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        // PARSE DESCRIPTOR DARIPADA JSON
        // DAN KIRA SIMILARITY
        public double CalculateSimilarity(
            IReadOnlyList<float>
                storedDescriptor,
            string capturedDescriptorJson)
        {
            float[] capturedDescriptor =
                ParseAndValidateDescriptor(
                    capturedDescriptorJson);

            return CalculateSimilarity(
                storedDescriptor,
                capturedDescriptor);
        }

        // KIRA SIMILARITY MENGIKUT
        // FORMULA HUMAN FACERES
        public double CalculateSimilarity(
            IReadOnlyList<float>
                storedDescriptor,
            IReadOnlyList<float>
                capturedDescriptor)
        {
            const double multiplier =
                25;

            const double minimumRange =
                0.2;

            const double maximumRange =
                0.8;

            if (storedDescriptor.Count !=
                    DescriptorLength ||
                capturedDescriptor.Count !=
                    DescriptorLength)
            {
                throw new InvalidOperationException(
                    "Face descriptor mesti " +
                    "mempunyai " +
                    $"{DescriptorLength} nilai.");
            }

            double sum = 0;

            for (int index = 0;
                 index < DescriptorLength;
                 index++)
            {
                float storedValue =
                    storedDescriptor[index];

                float capturedValue =
                    capturedDescriptor[index];

                if (!float.IsFinite(
                        storedValue) ||
                    !float.IsFinite(
                        capturedValue))
                {
                    throw new
                        InvalidOperationException(
                            "Face descriptor " +
                            "mengandungi nilai " +
                            "tidak sah.");
                }

                double difference =
                    storedValue -
                    capturedValue;

                sum +=
                    difference *
                    difference;
            }

            double distance =
                Math.Round(
                    100 *
                    multiplier *
                    sum,
                    MidpointRounding
                        .AwayFromZero)
                / 100;

            if (distance == 0)
            {
                return 1;
            }

            double rootDistance =
                Math.Sqrt(
                    distance);

            double similarity =
                (
                    1 -
                    (rootDistance / 100) -
                    minimumRange
                )
                /
                (
                    maximumRange -
                    minimumRange
                );

            similarity =
                Math.Clamp(
                    similarity,
                    0,
                    1);

            return Math.Round(
                similarity,
                2,
                MidpointRounding.AwayFromZero);
        }

        // VALIDASI DESCRIPTOR
        private static float[]
            ParseAndValidateDescriptor(
                string descriptorJson)
        {
            if (string.IsNullOrWhiteSpace(
                    descriptorJson))
            {
                throw new InvalidOperationException(
                    "Face descriptor kosong.");
            }

            float[]? descriptor;

            try
            {
                descriptor =
                    JsonSerializer
                        .Deserialize<float[]>(
                            descriptorJson);
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException(
                    "Format face descriptor " +
                    "tidak sah.",
                    exception);
            }

            if (descriptor == null ||
                descriptor.Length !=
                    DescriptorLength)
            {
                throw new InvalidOperationException(
                    "Face descriptor mesti " +
                    "mempunyai " +
                    $"{DescriptorLength} nilai.");
            }

            if (descriptor.Any(value =>
                    !float.IsFinite(value)))
            {
                throw new InvalidOperationException(
                    "Face descriptor mengandungi " +
                    "nilai tidak sah.");
            }

            return descriptor;
        }

        // HASILKAN SHA-256 HASH
        private static string CalculateHash(
            string encryptedPayload)
        {
            byte[] contentBytes =
                Encoding.UTF8.GetBytes(
                    encryptedPayload);

            byte[] hashBytes =
                SHA256.HashData(
                    contentBytes);

            return Convert.ToHexString(
                hashBytes);
        }

        // PERBANDINGAN HASH SECARA SELAMAT
        private static bool HashesMatch(
            string expectedHash,
            string actualHash)
        {
            try
            {
                byte[] expectedBytes =
                    Convert.FromHexString(
                        expectedHash);

                byte[] actualBytes =
                    Convert.FromHexString(
                        actualHash);

                return CryptographicOperations
                    .FixedTimeEquals(
                        expectedBytes,
                        actualBytes);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        // HALANG PATH TRAVERSAL
        private string ResolveSafePath(
            string templateReference)
        {
            if (string.IsNullOrWhiteSpace(
                    templateReference) ||
                Path.GetFileName(
                    templateReference) !=
                templateReference)
            {
                throw new InvalidOperationException(
                    "Face template reference " +
                    "tidak sah.");
            }

            return Path.Combine(
                _templateDirectory,
                templateReference);
        }
    }
}