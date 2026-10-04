namespace Domain.Entities.IntegrationSecrets;

/// <summary>Which outbound integration a secret configures — drives the tab's sub-sections.</summary>
public enum IntegrationSecretCategory
{
    Email = 1,
    Captcha,
    ObjectStorage,
}
