using Microsoft.Data.Sqlite;

namespace PlcMonitor.Data;

public sealed class StoppageRepository
{
    private readonly string _connectionString;

    public StoppageRepository(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
        Initialize();
    }

    private void Initialize()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS Stoppages (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SectionKey TEXT NOT NULL,
                    SectionName TEXT NOT NULL,
                    StartedAt TEXT NOT NULL,
                    EndedAt TEXT,
                    DurationSeconds REAL NOT NULL DEFAULT 0,
                    Cause TEXT NOT NULL DEFAULT '',
                    Category TEXT NOT NULL DEFAULT 'Untagged',
                    OperatorReason TEXT,
                    OperatorNote TEXT,
                    OperatorSetAt TEXT,
                    IsActive INTEGER NOT NULL DEFAULT 0
                );
                CREATE INDEX IF NOT EXISTS IX_Stoppages_SectionKey ON Stoppages(SectionKey);
                CREATE INDEX IF NOT EXISTS IX_Stoppages_StartedAt  ON Stoppages(StartedAt);
                """;
            cmd.ExecuteNonQuery();
        }

        // ── Schema migration for existing databases ──────────────
        EnsureColumn(conn, "Stoppages", "Category", "TEXT NOT NULL DEFAULT 'Untagged'");
        EnsureColumn(conn, "Stoppages", "OperatorReason", "TEXT");
        EnsureColumn(conn, "Stoppages", "OperatorNote", "TEXT");
        EnsureColumn(conn, "Stoppages", "OperatorSetAt", "TEXT");
    }

    private static void EnsureColumn(SqliteConnection conn, string table, string column, string type)
    {
        using (var check = conn.CreateCommand())
        {
            check.CommandText = $"PRAGMA table_info({table});";
            using var reader = check.ExecuteReader();
            while (reader.Read())
                if (reader.GetString(1) == column) return;   // already exists
        }
        using var alter = conn.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {type};";
        alter.ExecuteNonQuery();
    }

    public long InsertActiveStoppage(StoppageEvent ev)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Stoppages
                (SectionKey, SectionName, StartedAt, Cause, Category, IsActive)
            VALUES ($key, $name, $started, $cause, 'Untagged', 1);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$key", ev.SectionKey);
        cmd.Parameters.AddWithValue("$name", ev.SectionName);
        cmd.Parameters.AddWithValue("$started", ev.StartedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$cause", ev.Cause);
        return (long)cmd.ExecuteScalar()!;
    }

    public void CloseStoppage(long id, DateTime endedAt, double durationSeconds, string cause)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Stoppages
            SET EndedAt = $ended, DurationSeconds = $dur, Cause = $cause, IsActive = 0
            WHERE Id = $id;
            """;
        cmd.Parameters.AddWithValue("$ended", endedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$dur", durationSeconds);
        cmd.Parameters.AddWithValue("$cause", cause);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void UpdateActiveCause(long id, string cause)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Stoppages SET Cause = $cause WHERE Id = $id;";
        cmd.Parameters.AddWithValue("$cause", cause);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Sets the operator-chosen reason on an event (active or already closed).</summary>
    public bool SetOperatorReason(long id, string reasonKey, string? note)
    {
        var opt = ReasonCatalog.Find(reasonKey);
        if (opt is null) return false;

        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Stoppages
            SET Category = $cat, OperatorReason = $reason, OperatorNote = $note,
                OperatorSetAt = $setAt
            WHERE Id = $id;
            """;
        cmd.Parameters.AddWithValue("$cat", opt.Category);
        cmd.Parameters.AddWithValue("$reason", opt.Key);
        cmd.Parameters.AddWithValue("$note", (object?)note ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$setAt", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteNonQuery() > 0;
    }

    public IReadOnlyList<StoppageEvent> GetHistory(string? sectionKey = null, int limit = 500)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sectionKey is null
            ? "SELECT Id, SectionKey, SectionName, StartedAt, EndedAt, DurationSeconds, Cause, Category, OperatorReason, OperatorNote, OperatorSetAt, IsActive FROM Stoppages ORDER BY StartedAt DESC LIMIT $limit;"
            : "SELECT Id, SectionKey, SectionName, StartedAt, EndedAt, DurationSeconds, Cause, Category, OperatorReason, OperatorNote, OperatorSetAt, IsActive FROM Stoppages WHERE SectionKey = $key ORDER BY StartedAt DESC LIMIT $limit;";
        if (sectionKey is not null) cmd.Parameters.AddWithValue("$key", sectionKey);
        cmd.Parameters.AddWithValue("$limit", limit);

        var list = new List<StoppageEvent>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new StoppageEvent
            {
                Id = r.GetInt64(0),
                SectionKey = r.GetString(1),
                SectionName = r.GetString(2),
                StartedAt = DateTime.Parse(r.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind),
                EndedAt = r.IsDBNull(4) ? null : DateTime.Parse(r.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind),
                DurationSeconds = r.GetDouble(5),
                Cause = r.GetString(6),
                Category = r.IsDBNull(7) ? "Untagged" : r.GetString(7),
                OperatorReason = r.IsDBNull(8) ? null : r.GetString(8),
                OperatorNote = r.IsDBNull(9) ? null : r.GetString(9),
                OperatorSetAt = r.IsDBNull(10) ? null : DateTime.Parse(r.GetString(10), null, System.Globalization.DateTimeStyles.RoundtripKind),
                IsActive = r.GetInt32(11) != 0
            });
        }
        return list;
    }

    public double GetTotalDowntimeSeconds(string? sectionKey = null) =>
        SumByCategory(sectionKey, null);

    public double SumByCategory(string? sectionKey, string? category)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        var where = new List<string> { "IsActive = 0" };
        if (sectionKey is not null) where.Add("SectionKey = $key");
        if (category is not null) where.Add("Category = $cat");
        cmd.CommandText = $"SELECT COALESCE(SUM(DurationSeconds),0) FROM Stoppages WHERE {string.Join(" AND ", where)};";
        if (sectionKey is not null) cmd.Parameters.AddWithValue("$key", sectionKey);
        if (category is not null) cmd.Parameters.AddWithValue("$cat", category);
        return Convert.ToDouble(cmd.ExecuteScalar()!);
    }

    public double GetTotalDowntimeSecondsForSections(IEnumerable<string> sectionKeys)
    {
        var keys = sectionKeys.ToList();
        if (keys.Count == 0) return 0;
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        var ph = string.Join(",", keys.Select((_, i) => $"$k{i}"));
        cmd.CommandText = $"SELECT COALESCE(SUM(DurationSeconds),0) FROM Stoppages WHERE IsActive = 0 AND SectionKey IN ({ph});";
        for (int i = 0; i < keys.Count; i++)
            cmd.Parameters.AddWithValue($"$k{i}", keys[i]);
        return Convert.ToDouble(cmd.ExecuteScalar()!);
    }

    public double SumByCategoryForSections(IEnumerable<string> sectionKeys, string category)
    {
        var keys = sectionKeys.ToList();
        if (keys.Count == 0) return 0;
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        var ph = string.Join(",", keys.Select((_, i) => $"$k{i}"));
        cmd.CommandText = $"SELECT COALESCE(SUM(DurationSeconds),0) FROM Stoppages WHERE IsActive = 0 AND Category = $cat AND SectionKey IN ({ph});";
        cmd.Parameters.AddWithValue("$cat", category);
        for (int i = 0; i < keys.Count; i++)
            cmd.Parameters.AddWithValue($"$k{i}", keys[i]);
        return Convert.ToDouble(cmd.ExecuteScalar()!);
    }
}