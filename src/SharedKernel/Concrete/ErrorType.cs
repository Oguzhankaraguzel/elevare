namespace SharedKernel.Concrete;

/// <summary>
/// Represents the type of error.
/// </summary>
public enum ErrorType
{
    Failure,
    Validation,
    Problem ,
    NotFound,
    Conflict,
    Unexpected 
}
