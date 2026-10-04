using SharedKernel.Concrete;

namespace Domain.Entities.SiteSettings;

public static class SiteSettingErrors
{
    public static readonly Error NotFound = Error.NotFound("SiteSetting.NotFound", "The site setting was not found.");
    public static readonly Error KeyAlreadyExists = Error.Conflict("SiteSetting.KeyAlreadyExists", "A setting with this key already exists in the group.");
    public static readonly Error CustomCodeNotAllowed = Error.Failure("SiteSetting.CustomCodeNotAllowed", "Only Developer, Admin or SuperAdmin can save raw script/custom-code content.");
}
