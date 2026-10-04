using SharedKernel.Concrete;

namespace Domain.Entities.PublicAnalytics;

public static class PublicAnalyticsErrors
{
    public static readonly Error ClickMissingFields = Error.Failure("PageClick.MissingFields", "A click needs a path and element label.");
    public static readonly Error ViewEmptyPath = Error.Failure("PageView.EmptyPath", "A page view needs a path.");
}
