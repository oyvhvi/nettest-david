using Polly.Retry;
using System.Data.Common;

namespace atn062024.Services;

public interface IDataSourceProvider : IDisposable
{
    /// <summary>
    /// Gets a data source from which a db connection can be created.
    /// </summary>
    DbDataSource BuildDataSource();

    /// <summary>
    /// Gets a retry policy compatible with the data source.
    /// </summary>
    AsyncRetryPolicy GetRetryPolicy();
}
