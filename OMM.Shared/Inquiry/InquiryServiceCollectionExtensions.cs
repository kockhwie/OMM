using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;

namespace OMM.Shared.Inquiry;

public static class InquiryServiceCollectionExtensions
{
    public static IServiceCollection AddSharedInquiryServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var keyRingPath = configuration["Inquiry:DataProtection:KeyRingPath"];
        var certificatePath = configuration["Inquiry:DataProtection:KeyEncryptionCertificatePath"];
        var certificatePassword = configuration["Inquiry:DataProtection:KeyEncryptionCertificatePassword"];
        var previousCertificatePaths = configuration.GetSection("Inquiry:DataProtection:KeyEncryptionPreviousCertificatePaths")
            .GetChildren()
            .Select(section => section.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();
        var previousCertificatePasswords = configuration.GetSection("Inquiry:DataProtection:KeyEncryptionPreviousCertificatePasswords")
            .GetChildren()
            .Select(section => section.Value)
            .ToArray();

        var requiresSharedProductionKeys = environment.IsProduction()
            || (!environment.IsDevelopment() && !string.Equals(environment.EnvironmentName, "Integration", StringComparison.OrdinalIgnoreCase));
        if (requiresSharedProductionKeys && (string.IsNullOrWhiteSpace(keyRingPath) || string.IsNullOrWhiteSpace(certificatePath)))
        {
            throw new InvalidOperationException(
                "Production inquiry encryption requires a shared durable Data Protection key-ring path and a key-encryption certificate. Configure Inquiry:DataProtection settings for both applications.");
        }

        var dataProtection = services.AddDataProtection()
            .SetApplicationName($"OMMv2.InquiryMessages.{environment.EnvironmentName}");

        if (!string.IsNullOrWhiteSpace(keyRingPath))
        {
            Directory.CreateDirectory(keyRingPath);
            dataProtection = dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));
        }

        if (!string.IsNullOrWhiteSpace(certificatePath))
        {
            var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                certificatePath,
                certificatePassword,
                X509KeyStorageFlags.EphemeralKeySet);
            if (!certificate.HasPrivateKey)
            {
                throw new InvalidOperationException("The inquiry Data Protection certificate must include its private key in both applications.");
            }
            dataProtection.ProtectKeysWithCertificate(certificate);

            if (previousCertificatePaths.Length != previousCertificatePasswords.Length)
            {
                throw new InvalidOperationException("Each previous inquiry key-encryption certificate path requires a matching password entry.");
            }
            if (previousCertificatePaths.Length > 0)
            {
                var previousCertificates = previousCertificatePaths
                    .Select((path, index) => X509CertificateLoader.LoadPkcs12FromFile(
                        path,
                        previousCertificatePasswords[index],
                        X509KeyStorageFlags.EphemeralKeySet))
                    .ToArray();
                if (previousCertificates.Any(previous => !previous.HasPrivateKey))
                {
                    throw new InvalidOperationException("Previous inquiry Data Protection certificates must include their private keys.");
                }
                dataProtection.UnprotectKeysWithAnyCertificate(previousCertificates);
            }
        }

        services.TryAddSingleton<IInquiryMessageProtector, DataProtectionInquiryMessageProtector>();
        return services;
    }
}

public sealed class DataProtectionInquiryMessageProtector(IDataProtectionProvider provider) : IInquiryMessageProtector
{
    private const string Purpose = "OMMv2.InquiryRawMessage.v1";
    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    public bool IsConfigured => true;

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string protectedMessage) => _protector.Unprotect(protectedMessage);
}
