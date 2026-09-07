using Npgsql;

namespace Hospitality.IntegrationTests;

/// <summary>
/// Base PostgreSQL dedicada a pruebas. Se recrea desde cero al arrancar la factory
/// para que la migración y el seed demo sean deterministas en cada ejecución.
/// </summary>
public static class TestDatabase
{
    private const string MasterConnectionString =
        "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";

    public const string ConnectionString =
        "Host=localhost;Port=5432;Database=hospitality_db_test;Username=postgres;Password=postgres";

    private static bool _initialized;

    private static readonly object SyncRoot = new();

    public static void EnsureFreshDatabase()
    {
        if (_initialized)
        {
            return;
        }

        lock (SyncRoot)
        {
            if (_initialized)
            {
                return;
            }

            using var connection = new NpgsqlConnection(MasterConnectionString);
            connection.Open();

            using (var drop = connection.CreateCommand())
            {
                drop.CommandText = "DROP DATABASE IF EXISTS \"hospitality_db_test\" WITH (FORCE)";
                drop.ExecuteNonQuery();
            }

            using (var create = connection.CreateCommand())
            {
                create.CommandText = "CREATE DATABASE \"hospitality_db_test\"";
                create.ExecuteNonQuery();
            }

            _initialized = true;
        }
    }
}