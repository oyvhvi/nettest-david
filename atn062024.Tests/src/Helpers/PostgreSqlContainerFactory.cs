using atn062024.Services;
using Testcontainers.PostgreSql;

namespace atn062024.Tests.Helpers;

public static class PostgreSqlContainerFactory
{
    public static PostgreSqlContainer GetNew()
    {
        // Requires docker service or docker installation!

        return new PostgreSqlBuilder()
            .WithImage("postgres:16.0-bullseye")
            .WithDatabase("test_db_name")
            .WithUsername("admin")
            .WithPassword("admin")
            // Will remove the container automatically after all tests have been run.
            .WithCleanUp(true)
            .Build();
    }
}
