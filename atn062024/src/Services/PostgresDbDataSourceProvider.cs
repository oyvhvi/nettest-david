using atn062024.Models;
using Npgsql;
using Polly;
using Polly.Retry;
using System.Data.Common;

namespace atn062024.Services;

public sealed class PostgresDbDataSourceProvider : IDataSourceProvider
{
    private readonly NpgsqlDataSourceBuilder _configuredBuilder_;
    private readonly ResiliencySettings _resiliencySettings_;

    public PostgresDbDataSourceProvider(ISecretProvider secretProvider, ResiliencySettings resiliencySettings)
    {
        String connstring = secretProvider.GetSecret(Models.SecretKey.pgsql_connstring);
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connstring)
        {
#if DEBUG
            IncludeErrorDetail = true
#else
            IncludeErrorDetail = false
#endif
        };

        // require pooling
        if (!connectionStringBuilder.Pooling)
            throw new ArgumentException($"{nameof(PostgresDbDataSourceProvider)} requires pooling of connections");

        NpgsqlDataSourceBuilder dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionStringBuilder.ConnectionString);

        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

        _configuredBuilder_ = dataSourceBuilder;
        _resiliencySettings_ = resiliencySettings;
    }

    public DbDataSource BuildDataSource() =>
        _configuredBuilder_.Build();

    public AsyncRetryPolicy GetRetryPolicy() =>
        Policy
        .Handle<NpgsqlException>(ex => ex.IsTransient)
        .WaitAndRetryAsync(
            _resiliencySettings_.MaxRetries,
            retryAttempt => TimeSpan.FromMilliseconds(_resiliencySettings_.BackoffMs));

    /// <summary>
    /// Pooling is enabled, which doesn't actually close connections.
    /// Make sure that connections to the server are actually closed when we're done,
    /// instead of relying on timeouts.
    /// </summary>
    void IDisposable.Dispose() =>
        NpgsqlConnection.ClearAllPools();
}