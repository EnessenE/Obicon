using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Obicon.Server.Data;

/// <summary>
/// Reconciles an existing SQLite schema with the current model at startup:
/// creates missing tables and adds missing columns, so upgrading the server
/// does not require deleting the database. EnsureCreated only builds a
/// completely empty database, so this fills the gap for partially old ones.
/// </summary>
public static class SchemaMigrator
{
    /// <summary>
    /// Column defaults for non-nullable columns that were added in later versions;
    /// rows already in the database get these values when the column is added.
    /// </summary>
    private static readonly Dictionary<string, string> ColumnDefaults = new()
    {
        ["TimeoutSeconds"] = "60",
        ["ExpectedStatusCodes"] = "'200-399'"
    };

    public static void Migrate(ObiconDbContext db)
    {
        var tables = GetExistingTables(db);
        var script = db.Database.GenerateCreateScript();

        CreateMissingTables(db, script, tables);

        foreach (var entityType in db.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName() ?? string.Empty;
            if (!tables.Contains(tableName))
            {
                continue;
            }

            var existingColumns = GetExistingColumns(db, tableName);
            foreach (var property in entityType.GetProperties())
            {
                var columnName = property.GetColumnName(StoreObjectIdentifier.Table(tableName, null));
                if (columnName == null || existingColumns.Contains(columnName))
                {
                    continue;
                }

                var columnType = property.GetColumnType() ?? "TEXT";
                var defaultClause = BuildDefaultClause(property, columnName);
                db.Database.ExecuteSqlRaw($"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {columnType} {defaultClause}");
            }
        }

        ApplyDataMigrations(db);
    }

    /// <summary>
    /// One-time data conversions for values whose meaning changed between versions.
    /// Applied fixups are recorded in the SchemaMigrations table so they run exactly once.
    /// </summary>
    private static void ApplyDataMigrations(ObiconDbContext db)
    {
        db.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS \"SchemaMigrations\" (\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK_SchemaMigrations\" PRIMARY KEY)");

        // Test.Frequency changed from the TestFrequency enum (0-6) to plain seconds
        if (!IsMigrationApplied(db, "frequency-enum-to-seconds"))
        {
            db.Database.ExecuteSqlRaw("""
                UPDATE "Tests" SET "Frequency" = CASE "Frequency"
                    WHEN 0 THEN 10
                    WHEN 1 THEN 30
                    WHEN 2 THEN 60
                    WHEN 3 THEN 120
                    WHEN 4 THEN 300
                    WHEN 5 THEN 600
                    WHEN 6 THEN 3600
                    ELSE "Frequency" END
                WHERE "Frequency" BETWEEN 0 AND 6
                """);
            db.Database.ExecuteSqlRaw("INSERT INTO \"SchemaMigrations\" (\"MigrationId\") VALUES ('frequency-enum-to-seconds')");
        }
    }

    private static bool IsMigrationApplied(ObiconDbContext db, string migrationId)
    {
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM \"SchemaMigrations\" WHERE \"MigrationId\" = @id";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@id";
        parameter.Value = migrationId;
        command.Parameters.Add(parameter);
        db.Database.OpenConnection();
        try
        {
            return Convert.ToInt64(command.ExecuteScalar()) > 0;
        }
        finally
        {
            db.Database.CloseConnection();
        }
    }

    private static void CreateMissingTables(ObiconDbContext db, string createScript, HashSet<string> existingTables)
    {
        // The generated script is plain DDL with statements separated by semicolons;
        // execute only the CREATE TABLE / CREATE INDEX statements for tables that do not exist yet
        foreach (var rawStatement in createScript.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var statement = rawStatement.Trim(' ', '\r', '\n');
            if (statement.Length == 0)
            {
                continue;
            }

            var tableName = ExtractTableName(statement);
            if (tableName == null || existingTables.Contains(tableName))
            {
                continue;
            }

            db.Database.ExecuteSqlRaw(statement);
        }
    }

    /// <summary>
    /// Extracts the table a CREATE TABLE / CREATE INDEX statement belongs to.
    /// </summary>
    private static string? ExtractTableName(string statement)
    {
        if (statement.StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase))
        {
            var start = statement.IndexOf('"');
            var end = start >= 0 ? statement.IndexOf('"', start + 1) : -1;
            return start >= 0 && end > start ? statement[(start + 1)..end] : null;
        }

        if (statement.StartsWith("CREATE INDEX", StringComparison.OrdinalIgnoreCase) ||
            statement.StartsWith("CREATE UNIQUE INDEX", StringComparison.OrdinalIgnoreCase))
        {
            var onIndex = statement.IndexOf(" ON \"", StringComparison.OrdinalIgnoreCase);
            if (onIndex < 0)
            {
                return null;
            }
            var start = onIndex + 5;
            var end = statement.IndexOf('"', start);
            return end > start ? statement[start..end] : null;
        }

        return null;
    }

    private static string BuildDefaultClause(IProperty property, string columnName)
    {
        if (property.IsNullable)
        {
            return "DEFAULT NULL";
        }

        if (ColumnDefaults.TryGetValue(columnName, out var @default))
        {
            return $"DEFAULT {@default}";
        }

        if (property.ClrType == typeof(string) || property.ClrType.IsGenericType)
        {
            // Strings and JSON-converted collections: an empty value round-trips to a sane default
            return property.ClrType.IsGenericType ? "DEFAULT '[]'" : "DEFAULT ''";
        }

        return "DEFAULT 0";
    }

    private static HashSet<string> GetExistingTables(ObiconDbContext db)
    {
        var tables = new HashSet<string>();
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
        db.Database.OpenConnection();
        try
        {
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                tables.Add(reader.GetString(0));
            }
        }
        finally
        {
            db.Database.CloseConnection();
        }
        return tables;
    }

    private static HashSet<string> GetExistingColumns(ObiconDbContext db, string tableName)
    {
        var columns = new HashSet<string>();
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\")";
        db.Database.OpenConnection();
        try
        {
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                columns.Add(reader.GetString(1));
            }
        }
        finally
        {
            db.Database.CloseConnection();
        }
        return columns;
    }
}
