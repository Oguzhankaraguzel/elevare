using System.Text;
using SharedKernel.Concrete;

namespace SharedKernel.Extensions.Exceptions;

public static class ExceptionExtensions
{
    public static Error ToError(this Exception ex, string code = "Unexpected.Error", string description = "An unexpected error occurred")
    {
        StringBuilder sb = new(description);   
        do
        {
            if (ex is not null)
            {
                sb.AppendLine();
                sb.AppendLine(ex.Message);
                ex = ex.InnerException;
            }
        } while (ex is not null);
        return Error.Unexpected(code, sb.ToString());
    }
}
