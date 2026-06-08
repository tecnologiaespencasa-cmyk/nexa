namespace IntranetPrueba.Services.Models;

public class ServiceResult
{
    protected ServiceResult(bool succeeded, string? errorMessage)
    {
        Succeeded = succeeded;
        ErrorMessage = errorMessage;
    }

    public bool Succeeded { get; }

    public string? ErrorMessage { get; }

    public static ServiceResult Success() => new(true, null);

    public static ServiceResult Failure(string errorMessage) => new(false, errorMessage);
}

public class ServiceResult<T> : ServiceResult
{
    private ServiceResult(bool succeeded, T? value, string? errorMessage)
        : base(succeeded, errorMessage)
    {
        Value = value;
    }

    public T? Value { get; }

    public static ServiceResult<T> Success(T value) => new(true, value, null);

    public static new ServiceResult<T> Failure(string errorMessage) => new(false, default, errorMessage);
}
