namespace atn062024.Util;

public sealed class AsyncLazy<T> : IDisposable
{
    private readonly Func<CancellationToken, Task<T>> _factoryAsync_;
    private readonly SemaphoreSlim _factorySync_ = new(1, 1);
    private Boolean _hasValue = false;
    private T? _value;

    public AsyncLazy(Func<CancellationToken, Task<T>> factoryAsync) =>
        _factoryAsync_ = factoryAsync;

    public void Dispose() =>
        _factorySync_.Dispose();

    public async ValueTask<T> GetValueAsync(CancellationToken cancel)
    {
        if (!_hasValue)
        {
            await _factorySync_.WaitAsync(cancel);
            try
            {
                if (!_hasValue)
                {
                    _value = await _factoryAsync_(cancel) ?? throw new AsyncLazyException("Factory returned null.");
                    _hasValue = true;
                }
            }
            finally
            {
                _factorySync_.Release();
            }
        }
        return _value!;
    }
}

public class AsyncLazyException : Exception
{
    public AsyncLazyException() : base()
    {
    }

    public AsyncLazyException(string? message) : base(message)
    {
    }

    public AsyncLazyException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}