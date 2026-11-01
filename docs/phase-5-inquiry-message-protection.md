# Phase 5 Inquiry Message Protection

Public and Admin use ASP.NET Core Data Protection with the same environment-qualified application name (`OMMv2.InquiryMessages.{EnvironmentName}`) and purpose (`OMMv2.InquiryRawMessage.v1`). The two applications must use the same environment name and key configuration. Use a different key ring for each environment. `InquiryRawMessage.MessageText` contains only the protected payload. Admin decrypts it after recording an audit event; Public has no raw-message read API.

## Production configuration

Configure both applications with the same values:

- `Inquiry:DataProtection:KeyRingPath`: a durable directory mounted read/write by both Public and Admin instances. It must be shared across replicas and survive restarts.
- `Inquiry:DataProtection:KeyEncryptionCertificatePath`: a PFX certificate file available to both applications.
- `Inquiry:DataProtection:KeyEncryptionCertificatePassword`: load this from the deployment secret store; never commit it.
- `Inquiry:DataProtection:KeyEncryptionPreviousCertificatePaths` and `Inquiry:DataProtection:KeyEncryptionPreviousCertificatePasswords`: during certificate rotation, provide the old PFX files and matching secret passwords until old protected keys are no longer needed.

The certificate must include the private key in both applications. Production startup fails when the shared key-ring path or certificate is missing. The key ring must be backed up and access-restricted to the application identities. Use the same settings in each replica so data written by Public can be read by Admin.

For Development, if no key-ring path is supplied, Data Protection uses the local user's default key store. Run both applications under the same user profile or configure a shared local key-ring directory. Production-like testing should use a shared directory and certificate.

Key rotation must keep the new certificate as the active protection certificate and also configure every old certificate for unprotecting existing key-ring entries. Retain those certificates and key-ring files until old entries and all data protected by them have expired or been re-protected.

Raw inquiry capture fails closed until the protection service is configured. Existing session and turn metadata remains accessible to authorized Admin users; raw message viewing and export require a valid shared key ring.
