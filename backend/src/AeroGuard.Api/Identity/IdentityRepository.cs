using System.Globalization;
using AeroGuard.Api.Data;
using Microsoft.Data.Sqlite;

namespace AeroGuard.Api.Identity;

public sealed class IdentityRepository(
    ISqliteConnectionFactory connectionFactory)
    : IIdentityRepository
{
    public async Task<UserAccount?> FindUserByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = UserSelect + " WHERE username = $username COLLATE NOCASE LIMIT 1;";
        command.Parameters.AddWithValue("$username", username);
        return await ReadOneUserAsync(connection, command, cancellationToken);
    }

    public async Task<UserAccount?> GetUserAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = UserSelect + " WHERE id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        return await ReadOneUserAsync(connection, command, cancellationToken);
    }

    public async Task<IReadOnlyList<UserAccount>> GetUsersAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = UserSelect + " ORDER BY username COLLATE NOCASE, id;";

        var users = new List<UserAccount>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                users.Add(ReadUser(reader, []));
            }
        }

        var result = new List<UserAccount>(users.Count);
        foreach (var user in users)
        {
            var roles = await ReadRoleNamesAsync(connection, user.Id, null, cancellationToken);
            result.Add(user with { Roles = roles });
        }

        return result;
    }

    public async Task<UserAccount> CreateUserAsync(
        string username,
        string displayName,
        string passwordHash,
        IReadOnlyCollection<string> roles,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        long userId;

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO users (
                    username, display_name, password_hash, is_active,
                    created_at_utc, updated_at_utc
                )
                VALUES ($username, $display_name, $password_hash, 1, $now, $now);
                SELECT last_insert_rowid();
                """;
            command.Parameters.AddWithValue("$username", username);
            command.Parameters.AddWithValue("$display_name", displayName);
            command.Parameters.AddWithValue("$password_hash", passwordHash);
            command.Parameters.AddWithValue("$now", Format(now));
            userId = Convert.ToInt64(
                await command.ExecuteScalarAsync(cancellationToken),
                CultureInfo.InvariantCulture);
        }

        await ReplaceRolesCoreAsync(
            connection,
            transaction,
            userId,
            roles,
            now,
            deleteExisting: false,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new UserAccount(
            userId,
            username,
            displayName,
            passwordHash,
            true,
            now,
            now,
            roles.Order(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public async Task<UserAccount?> ReplaceRolesAsync(
        long userId,
        IReadOnlyCollection<string> roles,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        if (!await UserExistsAsync(connection, transaction, userId, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await ReplaceRolesCoreAsync(
            connection,
            transaction,
            userId,
            roles,
            now,
            deleteExisting: true,
            cancellationToken);
        await TouchUserAsync(connection, transaction, userId, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetUserAsync(userId, cancellationToken);
    }

    public async Task<UserAccount?> SetUserStatusAsync(
        long userId,
        bool isActive,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE users
            SET is_active = $is_active, updated_at_utc = $updated_at_utc
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$is_active", isActive ? 1 : 0);
        command.Parameters.AddWithValue("$updated_at_utc", Format(now));
        command.Parameters.AddWithValue("$id", userId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            return null;
        }

        if (!isActive)
        {
            await using var revoke = connection.CreateCommand();
            revoke.CommandText =
                """
                UPDATE sessions
                SET revoked_at_utc = $revoked_at_utc
                WHERE user_id = $user_id AND revoked_at_utc IS NULL;
                """;
            revoke.Parameters.AddWithValue("$revoked_at_utc", Format(now));
            revoke.Parameters.AddWithValue("$user_id", userId);
            await revoke.ExecuteNonQueryAsync(cancellationToken);
        }

        return await GetUserAsync(userId, cancellationToken);
    }

    public async Task<bool> SetPasswordHashAsync(
        long userId,
        string passwordHash,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText =
            """
            UPDATE users
            SET password_hash = $password_hash, updated_at_utc = $updated_at_utc
            WHERE id = $id;
            """;
        update.Parameters.AddWithValue("$password_hash", passwordHash);
        update.Parameters.AddWithValue("$updated_at_utc", Format(now));
        update.Parameters.AddWithValue("$id", userId);
        if (await update.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await using var revoke = connection.CreateCommand();
        revoke.Transaction = transaction;
        revoke.CommandText =
            """
            UPDATE sessions
            SET revoked_at_utc = $revoked_at_utc
            WHERE user_id = $user_id AND revoked_at_utc IS NULL;
            """;
        revoke.Parameters.AddWithValue("$revoked_at_utc", Format(now));
        revoke.Parameters.AddWithValue("$user_id", userId);
        await revoke.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<RoleRecord>> GetRolesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT roles.id, roles.name, roles.description, roles.is_system,
                   COUNT(user_roles.user_id), roles.created_at_utc
            FROM roles
            LEFT JOIN user_roles ON user_roles.role_id = roles.id
            GROUP BY roles.id
            ORDER BY roles.is_system DESC, roles.name COLLATE NOCASE;
            """;

        var roles = new List<RoleRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            roles.Add(new RoleRecord(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetBoolean(3),
                reader.GetInt64(4),
                Parse(reader.GetString(5))));
        }

        return roles;
    }

    public async Task<RoleRecord> CreateRoleAsync(
        string name,
        string description,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO roles (name, description, is_system, created_at_utc)
            VALUES ($name, $description, 0, $created_at_utc);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$description", description);
        command.Parameters.AddWithValue("$created_at_utc", Format(now));
        var id = Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);
        return new RoleRecord(id, name, description, false, 0, now);
    }

    public async Task<bool> DeleteRoleAsync(
        long roleId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "DELETE FROM roles WHERE id = $id AND is_system = 0;";
        command.Parameters.AddWithValue("$id", roleId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<long> CountActiveAdminsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(DISTINCT users.id)
            FROM users
            JOIN user_roles ON user_roles.user_id = users.id
            JOIN roles ON roles.id = user_roles.role_id
            WHERE users.is_active = 1 AND roles.name = 'Admin' COLLATE NOCASE;
            """;
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);
    }

    public async Task<long> CreateSessionAsync(
        string tokenHash,
        long userId,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO sessions (token_hash, user_id, created_at_utc, expires_at_utc)
            VALUES ($token_hash, $user_id, $created_at_utc, $expires_at_utc);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$token_hash", tokenHash);
        command.Parameters.AddWithValue("$user_id", userId);
        command.Parameters.AddWithValue("$created_at_utc", Format(createdAt));
        command.Parameters.AddWithValue("$expires_at_utc", Format(expiresAt));
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);
    }

    public async Task<SessionRecord?> FindSessionAsync(
        string tokenHash,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT sessions.id, sessions.expires_at_utc,
                   users.id, users.username, users.display_name, users.password_hash,
                   users.is_active, users.created_at_utc, users.updated_at_utc
            FROM sessions
            JOIN users ON users.id = sessions.user_id
            WHERE sessions.token_hash = $token_hash
              AND sessions.revoked_at_utc IS NULL
              AND sessions.expires_at_utc > $now
              AND users.is_active = 1
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$token_hash", tokenHash);
        command.Parameters.AddWithValue("$now", Format(now));

        long sessionId;
        DateTimeOffset expiresAt;
        UserAccount user;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            sessionId = reader.GetInt64(0);
            expiresAt = Parse(reader.GetString(1));
            user = new UserAccount(
                reader.GetInt64(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetBoolean(6),
                Parse(reader.GetString(7)),
                Parse(reader.GetString(8)),
                []);
        }

        var roles = await ReadRoleNamesAsync(connection, user.Id, null, cancellationToken);
        return new SessionRecord(sessionId, user with { Roles = roles }, expiresAt);
    }

    public async Task<bool> RevokeSessionAsync(
        long sessionId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE sessions
            SET revoked_at_utc = $revoked_at_utc
            WHERE id = $id AND revoked_at_utc IS NULL;
            """;
        command.Parameters.AddWithValue("$revoked_at_utc", Format(revokedAt));
        command.Parameters.AddWithValue("$id", sessionId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private static async Task<UserAccount?> ReadOneUserAsync(
        SqliteConnection connection,
        SqliteCommand command,
        CancellationToken cancellationToken)
    {
        UserAccount user;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            user = ReadUser(reader, []);
        }

        var roles = await ReadRoleNamesAsync(connection, user.Id, null, cancellationToken);
        return user with { Roles = roles };
    }

    private static UserAccount ReadUser(SqliteDataReader reader, IReadOnlyList<string> roles) =>
        new(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetBoolean(4),
            Parse(reader.GetString(5)),
            Parse(reader.GetString(6)),
            roles);

    private static async Task<IReadOnlyList<string>> ReadRoleNamesAsync(
        SqliteConnection connection,
        long userId,
        SqliteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT roles.name
            FROM roles
            JOIN user_roles ON user_roles.role_id = roles.id
            WHERE user_roles.user_id = $user_id
            ORDER BY roles.name COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$user_id", userId);

        var roles = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            roles.Add(reader.GetString(0));
        }

        return roles;
    }

    private static async Task ReplaceRolesCoreAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long userId,
        IReadOnlyCollection<string> roles,
        DateTimeOffset now,
        bool deleteExisting,
        CancellationToken cancellationToken)
    {
        foreach (var role in roles)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT OR IGNORE INTO user_roles (user_id, role_id, assigned_at_utc)
                SELECT $user_id, id, $assigned_at_utc
                FROM roles
                WHERE name = $role_name COLLATE NOCASE;
                """;
            insert.Parameters.AddWithValue("$user_id", userId);
            insert.Parameters.AddWithValue("$assigned_at_utc", Format(now));
            insert.Parameters.AddWithValue("$role_name", role);
            await insert.ExecuteNonQueryAsync(cancellationToken);
            await using var verify = connection.CreateCommand();
            verify.Transaction = transaction;
            verify.CommandText =
                """
                SELECT 1
                FROM user_roles
                JOIN roles ON roles.id = user_roles.role_id
                WHERE user_roles.user_id = $user_id
                  AND roles.name = $role_name COLLATE NOCASE
                LIMIT 1;
                """;
            verify.Parameters.AddWithValue("$user_id", userId);
            verify.Parameters.AddWithValue("$role_name", role);
            if (await verify.ExecuteScalarAsync(cancellationToken) is null)
            {
                throw new InvalidOperationException($"Role '{role}' no longer exists.");
            }
        }

        if (deleteExisting)
        {
            await using var delete = connection.CreateCommand();
            delete.Transaction = transaction;
            var parameters = roles
                .Select((_, index) => $"$role_{index}")
                .ToArray();
            delete.CommandText =
                $"""
                DELETE FROM user_roles
                WHERE user_id = $user_id
                  AND role_id NOT IN (
                      SELECT id
                      FROM roles
                      WHERE name IN ({string.Join(", ", parameters)})
                  );
                """;
            delete.Parameters.AddWithValue("$user_id", userId);
            foreach (var (role, index) in roles.Select((value, index) => (value, index)))
            {
                delete.Parameters.AddWithValue($"$role_{index}", role);
            }

            await delete.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<bool> UserExistsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT 1 FROM users WHERE id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", userId);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task TouchUserAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "UPDATE users SET updated_at_utc = $updated_at_utc WHERE id = $id;";
        command.Parameters.AddWithValue("$updated_at_utc", Format(now));
        command.Parameters.AddWithValue("$id", userId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string Format(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset Parse(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private const string UserSelect =
        """
        SELECT id, username, display_name, password_hash, is_active,
               created_at_utc, updated_at_utc
        FROM users
        """;
}
