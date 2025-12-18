using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Npgsql;

namespace PromptBox.Services;

public class DatabaseContextService : IDatabaseContextService
{
    private const int MaxRowLimit = 500;
    private const int MaxColumnValueLength = 200;

    public List<string> GetSupportedDatabaseTypes()
    {
        return new List<string> { "SqlServer", "PostgreSQL", "MySQL" };
    }

    public async Task<bool> TestConnectionAsync(string connectionString, string dbType)
    {
        try
        {
            return await Task.Run(async () =>
            {
                switch (dbType.ToLower())
                {
                    case "sqlserver":
                        using (var connection = new SqlConnection(connectionString))
                        {
                            await connection.OpenAsync();
                            return true;
                        }
                    case "postgresql":
                        using (var connection = new NpgsqlConnection(connectionString))
                        {
                            await connection.OpenAsync();
                            return true;
                        }
                    case "mysql":
                        using (var connection = new MySqlConnection(connectionString))
                        {
                            await connection.OpenAsync();
                            return true;
                        }
                    default:
                        throw new NotSupportedException($"Database type {dbType} is not supported");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Database connection test failed: {ex.Message}");
            return false;
        }
    }


    /// <summary>
    /// Checks if the query is a SELECT statement (read-only)
    /// </summary>
    private bool IsSelectQuery(string query)
    {
        var trimmed = query.Trim().ToLowerInvariant();
        return trimmed.StartsWith("select") || 
               trimmed.StartsWith("with") ||  // CTEs
               trimmed.StartsWith("show") ||  // MySQL SHOW commands
               trimmed.StartsWith("describe") || // MySQL DESCRIBE
               trimmed.StartsWith("explain");  // Execution plans
    }

    /// <summary>
    /// Checks if the query is a data modification statement
    /// </summary>
    private bool IsModificationQuery(string query)
    {
        var trimmed = query.Trim().ToLowerInvariant();
        return trimmed.StartsWith("insert") ||
               trimmed.StartsWith("update") ||
               trimmed.StartsWith("delete") ||
               trimmed.StartsWith("truncate") ||
               trimmed.StartsWith("drop") ||
               trimmed.StartsWith("alter") ||
               trimmed.StartsWith("create");
    }

    public async Task<string> ExecuteQueryAsync(string connectionString, string query, string dbType)
    {
        return await Task.Run(async () =>
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Determine if this is a SELECT or modification query
                bool isSelect = IsSelectQuery(query);
                bool isModification = IsModificationQuery(query);

                switch (dbType.ToLower())
                {
                    case "sqlserver":
                        return isSelect 
                            ? await ExecuteSqlServerSelectAsync(connectionString, query, stopwatch)
                            : await ExecuteSqlServerNonQueryAsync(connectionString, query, stopwatch, isModification);
                    case "postgresql":
                        return isSelect 
                            ? await ExecutePostgreSqlSelectAsync(connectionString, query, stopwatch)
                            : await ExecutePostgreSqlNonQueryAsync(connectionString, query, stopwatch, isModification);
                    case "mysql":
                        return isSelect 
                            ? await ExecuteMySqlSelectAsync(connectionString, query, stopwatch)
                            : await ExecuteMySqlNonQueryAsync(connectionString, query, stopwatch, isModification);
                    default:
                        throw new NotSupportedException($"Database type {dbType} is not supported");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Database query execution failed: {ex.Message}");
                return $"Error executing query: {ex.Message}";
            }
        });
    }

    #region SQL Server

    private async Task<string> ExecuteSqlServerSelectAsync(string connectionString, string query, Stopwatch stopwatch)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();
            
            return FormatResultSet(reader, stopwatch);
        }
        catch (Exception ex)
        {
            return $"SQL Server Error: {ex.Message}";
        }
    }

    private async Task<string> ExecuteSqlServerNonQueryAsync(string connectionString, string query, Stopwatch stopwatch, bool isModification)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            using var command = new SqlCommand(query, connection);
            var rowsAffected = await command.ExecuteNonQueryAsync();
            
            return FormatNonQueryResult(rowsAffected, stopwatch, isModification, query);
        }
        catch (Exception ex)
        {
            return $"SQL Server Error: {ex.Message}";
        }
    }

    #endregion

    #region PostgreSQL

    private async Task<string> ExecutePostgreSqlSelectAsync(string connectionString, string query, Stopwatch stopwatch)
    {
        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            
            using var command = new NpgsqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();
            
            return FormatResultSet(reader, stopwatch);
        }
        catch (Exception ex)
        {
            return $"PostgreSQL Error: {ex.Message}";
        }
    }

    private async Task<string> ExecutePostgreSqlNonQueryAsync(string connectionString, string query, Stopwatch stopwatch, bool isModification)
    {
        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            
            using var command = new NpgsqlCommand(query, connection);
            var rowsAffected = await command.ExecuteNonQueryAsync();
            
            return FormatNonQueryResult(rowsAffected, stopwatch, isModification, query);
        }
        catch (Exception ex)
        {
            return $"PostgreSQL Error: {ex.Message}";
        }
    }

    #endregion

    #region MySQL

    private async Task<string> ExecuteMySqlSelectAsync(string connectionString, string query, Stopwatch stopwatch)
    {
        try
        {
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            
            using var command = new MySqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();
            
            return FormatResultSet(reader, stopwatch);
        }
        catch (Exception ex)
        {
            return $"MySQL Error: {ex.Message}";
        }
    }

    private async Task<string> ExecuteMySqlNonQueryAsync(string connectionString, string query, Stopwatch stopwatch, bool isModification)
    {
        try
        {
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            
            using var command = new MySqlCommand(query, connection);
            var rowsAffected = await command.ExecuteNonQueryAsync();
            
            return FormatNonQueryResult(rowsAffected, stopwatch, isModification, query);
        }
        catch (Exception ex)
        {
            return $"MySQL Error: {ex.Message}";
        }
    }

    #endregion


    #region Result Formatting

    private string FormatNonQueryResult(int rowsAffected, Stopwatch stopwatch, bool isModification, string query)
    {
        stopwatch.Stop();
        var sb = new StringBuilder();
        
        sb.AppendLine($"**Execution Time:** {stopwatch.ElapsedMilliseconds} ms");
        sb.AppendLine();
        
        if (isModification)
        {
            sb.AppendLine("⚠️ **Warning:** This was a data modification query.");
            sb.AppendLine();
        }
        
        // Determine query type for display
        var queryType = query.Trim().Split(' ')[0].ToUpperInvariant();
        sb.AppendLine($"**Query Type:** {queryType}");
        sb.AppendLine($"**Rows Affected:** {rowsAffected}");
        sb.AppendLine();
        sb.AppendLine("Query executed successfully.");
        
        return sb.ToString();
    }

    private string FormatResultSet(IDataReader reader, Stopwatch stopwatch)
    {
        var sb = new StringBuilder();
        var rowCount = 0;
        var wasTruncated = false;
        
        // Add metadata
        stopwatch.Stop();
        sb.AppendLine($"**Execution Time:** {stopwatch.ElapsedMilliseconds} ms");
        sb.AppendLine();
        
        // Handle case where there are no columns
        if (reader.FieldCount == 0)
        {
            sb.AppendLine("No data returned.");
            sb.AppendLine();
            sb.AppendLine($"**Row Count:** {rowCount}");
            return sb.ToString();
        }
        
        // Get column names
        var columns = new List<string>();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            columns.Add(reader.GetName(i));
        }
        
        // Create markdown table header
        sb.Append("| ");
        foreach (var column in columns)
        {
            sb.Append($"{TruncateValue(column)} | ");
        }
        sb.AppendLine();
        
        // Create separator
        sb.Append("| ");
        foreach (var column in columns)
        {
            sb.Append("--- | ");
        }
        sb.AppendLine();
        
        // Add data rows with limit
        while (reader.Read())
        {
            rowCount++;
            
            // Check row limit
            if (rowCount > MaxRowLimit)
            {
                wasTruncated = true;
                // Continue counting but don't add more rows
                while (reader.Read())
                {
                    rowCount++;
                }
                break;
            }
            
            sb.Append("| ");
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var value = reader.GetValue(i);
                var stringValue = value?.ToString() ?? "NULL";
                sb.Append($"{TruncateValue(stringValue)} | ");
            }
            sb.AppendLine();
        }
        
        sb.AppendLine();
        
        if (wasTruncated)
        {
            sb.AppendLine($"⚠️ **Result truncated after {MaxRowLimit} rows.** Total rows: {rowCount}");
        }
        else
        {
            sb.AppendLine($"**Row Count:** {rowCount}");
        }
        
        return sb.ToString();
    }

    private string TruncateValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;
            
        // Escape pipe characters for markdown tables
        value = value.Replace("|", "\\|");
        
        // Remove newlines for table formatting
        value = value.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
        
        if (value.Length > MaxColumnValueLength)
        {
            return value.Substring(0, MaxColumnValueLength - 3) + "...";
        }
        
        return value;
    }

    #endregion
}
