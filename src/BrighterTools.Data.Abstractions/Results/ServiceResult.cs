namespace BrighterTools.Data.Abstractions.Results;

public class ServiceResult<T>
{
    public bool Succeeded { get; private set; }

    public bool Failed => !Succeeded;

    public T? Content { get; private set; } = default!;

    public List<string> Messages { get; set; }

    public string Message => Messages.Any() ? string.Join("\n", Messages) : string.Empty;

    private ServiceResult(IEnumerable<string> errors)
    {
        Succeeded = false;
        Messages = errors.ToList();
    }

    private ServiceResult(T entity, params string[] messages)
    {
        Content = entity;
        Succeeded = true;
        Messages = messages?.ToList() ?? [];
    }

    private ServiceResult()
    {
        Succeeded = true;
        Messages = [];
    }

    public static ServiceResult<T> Success(T entity) => new(entity);

    public static ServiceResult<T> Success(T entity, params string[] messages) => new(entity, messages);

    public static ServiceResult<T> Success() => new();

    public static ServiceResult<T> Fail(params string[] errors) => new(errors);

    public static ServiceResult<T> Fail(IEnumerable<string> errors) => new(errors);

    public static ServiceResult<T> NotFound() => new([ $"{typeof(T).Name} not found." ]);
}
