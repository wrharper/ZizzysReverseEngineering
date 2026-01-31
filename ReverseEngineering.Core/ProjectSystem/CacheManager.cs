using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

#nullable enable

namespace ReverseEngineering.Core.ProjectSystem
{
    /// <summary>
    /// Manages SQLite cache for reverse engineering project analysis
    /// One database per project, organized by project name
    /// Tables: Symbols, Strings, CrossReferences, Disassembly, ByteRanges, Patterns, CacheMetadata
    /// </summary>
    public class CacheManager : IDisposable
    {
        private readonly string _databasePath;
        private SQLiteConnection? _connection;
        private bool _disposed;

        // Cache statistics
        private long _cacheHits;
        private long _totalQueries;

        public CacheManager(string databasePath)
        {
            _databasePath = databasePath;
            _cacheHits = 0;
            _totalQueries = 0;

            // Create directory if needed
            string? dir = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Initialize database
            InitializeDatabase();
        }

        /// <summary>
        /// Initialize SQLite database with all required tables
        /// </summary>
        private void InitializeDatabase()
        {
            try
            {
                _connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
                _connection.Open();

                using (var cmd = _connection.CreateCommand())
                {
                    // Symbols table
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Symbols (
                            SymbolID INTEGER PRIMARY KEY AUTOINCREMENT,
                            BinaryHash TEXT NOT NULL,
                            Address INTEGER NOT NULL,
                            Name TEXT,
                            Type TEXT,
                            Size INTEGER,
                            Section TEXT,
                            CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                            UNIQUE(BinaryHash, Address)
                        );
                        CREATE INDEX IF NOT EXISTS idx_symbols_hash_addr ON Symbols(BinaryHash, Address);
                        CREATE INDEX IF NOT EXISTS idx_symbols_name ON Symbols(Name);
                    ";
                    cmd.ExecuteNonQuery();

                    // Strings table
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Strings (
                            StringID INTEGER PRIMARY KEY AUTOINCREMENT,
                            BinaryHash TEXT NOT NULL,
                            Address INTEGER NOT NULL,
                            StringValue TEXT,
                            Type TEXT,
                            References BLOB,
                            CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                            UNIQUE(BinaryHash, Address)
                        );
                        CREATE INDEX IF NOT EXISTS idx_strings_hash_addr ON Strings(BinaryHash, Address);
                        CREATE INDEX IF NOT EXISTS idx_strings_value ON Strings(StringValue);
                    ";
                    cmd.ExecuteNonQuery();

                    // CrossReferences table
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS CrossReferences (
                            XRefID INTEGER PRIMARY KEY AUTOINCREMENT,
                            BinaryHash TEXT NOT NULL,
                            SourceAddress INTEGER NOT NULL,
                            TargetAddress INTEGER NOT NULL,
                            RefType TEXT,
                            Context TEXT,
                            CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                            UNIQUE(BinaryHash, SourceAddress, TargetAddress, RefType)
                        );
                        CREATE INDEX IF NOT EXISTS idx_xref_source ON CrossReferences(BinaryHash, SourceAddress);
                        CREATE INDEX IF NOT EXISTS idx_xref_target ON CrossReferences(BinaryHash, TargetAddress);
                    ";
                    cmd.ExecuteNonQuery();

                    // Disassembly table
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Disassembly (
                            RangeID INTEGER PRIMARY KEY AUTOINCREMENT,
                            AddressRange TEXT NOT NULL,
                            BinaryHash TEXT NOT NULL,
                            Instructions BLOB,
                            InBasicBlock TEXT,
                            CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                            UNIQUE(BinaryHash, AddressRange)
                        );
                        CREATE INDEX IF NOT EXISTS idx_disasm_hash_range ON Disassembly(BinaryHash, AddressRange);
                    ";
                    cmd.ExecuteNonQuery();

                    // ByteRanges table
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS ByteRanges (
                            RangeID INTEGER PRIMARY KEY AUTOINCREMENT,
                            BinaryHash TEXT NOT NULL,
                            StartOffset INTEGER NOT NULL,
                            EndOffset INTEGER NOT NULL,
                            Data BLOB,
                            Compressed BLOB,
                            CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                            UNIQUE(BinaryHash, StartOffset, EndOffset)
                        );
                        CREATE INDEX IF NOT EXISTS idx_bytes_hash_range ON ByteRanges(BinaryHash, StartOffset, EndOffset);
                    ";
                    cmd.ExecuteNonQuery();

                    // Patterns table (trainer)
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Patterns (
                            PatternID INTEGER PRIMARY KEY AUTOINCREMENT,
                            BinaryHash TEXT NOT NULL,
                            Address INTEGER NOT NULL,
                            Signature TEXT,
                            InstructionBytes BLOB,
                            Embedding BLOB,
                            ControlFlowSummary TEXT,
                            References BLOB,
                            CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                            UNIQUE(BinaryHash, Address)
                        );
                        CREATE INDEX IF NOT EXISTS idx_patterns_hash_addr ON Patterns(BinaryHash, Address);
                        CREATE INDEX IF NOT EXISTS idx_patterns_sig ON Patterns(Signature);
                    ";
                    cmd.ExecuteNonQuery();

                    // CacheMetadata table
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS CacheMetadata (
                            Key TEXT PRIMARY KEY,
                            Value TEXT,
                            LastUpdated DATETIME DEFAULT CURRENT_TIMESTAMP
                        );
                    ";
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to initialize cache database: {ex.Message}");
            }
        }

        /// <summary>
        /// Compute SHA256 hash of binary file
        /// </summary>
        public static string ComputeFileHash(string filePath)
        {
            try
            {
                using (var sha256 = SHA256.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hash = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            catch
            {
                return "";
            }
        }

        // ========== SYMBOL OPERATIONS ==========

        /// <summary>
        /// Insert or update a symbol in cache
        /// </summary>
        public bool InsertSymbol(CachedSymbol symbol)
        {
            if (_connection?.State != System.Data.ConnectionState.Open)
                return false;

            try
            {
                using (var cmd = _connection.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT OR REPLACE INTO Symbols (BinaryHash, Address, Name, Type, Size, Section, CreatedAt)
                        VALUES (@hash, @addr, @name, @type, @size, @section, @time)
                    ";
                    cmd.Parameters.AddWithValue("@hash", symbol.BinaryHash);
                    cmd.Parameters.AddWithValue("@addr", (long)symbol.Address);
                    cmd.Parameters.AddWithValue("@name", symbol.Name ?? "");
                    cmd.Parameters.AddWithValue("@type", symbol.Type ?? "");
                    cmd.Parameters.AddWithValue("@size", symbol.Size);
                    cmd.Parameters.AddWithValue("@section", symbol.Section ?? "");
                    cmd.Parameters.AddWithValue("@time", symbol.CreatedAt);

                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] InsertSymbol failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Query symbols by binary hash and optional name filter
        /// </summary>
        public List<CachedSymbol> QuerySymbols(string binaryHash, string? nameFilter = null)
        {
            _totalQueries++;
            var result = new List<CachedSymbol>();

            if (_connection?.State != System.Data.ConnectionState.Open)
                return result;

            try
            {
                using (var cmd = _connection.CreateCommand())
                {
                    if (nameFilter == null)
                    {
                        cmd.CommandText = "SELECT * FROM Symbols WHERE BinaryHash = @hash";
                        cmd.Parameters.AddWithValue("@hash", binaryHash);
                    }
                    else
                    {
                        cmd.CommandText = "SELECT * FROM Symbols WHERE BinaryHash = @hash AND Name = @name";
                        cmd.Parameters.AddWithValue("@hash", binaryHash);
                        cmd.Parameters.AddWithValue("@name", nameFilter);
                    }

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(new CachedSymbol
                            {
                                SymbolID = (long)reader["SymbolID"],
                                BinaryHash = (string)reader["BinaryHash"],
                                Address = (ulong)(long)reader["Address"],
                                Name = (string)(reader["Name"] ?? ""),
                                Type = (string)(reader["Type"] ?? ""),
                                Size = (int)(long)reader["Size"],
                                Section = (string)(reader["Section"] ?? ""),
                                CreatedAt = DateTime.Parse((string)reader["CreatedAt"])
                            });
                        }
                    }

                    if (result.Count > 0)
                        _cacheHits++;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] QuerySymbols failed: {ex.Message}");
            }

            return result;
        }

        // ========== STRING OPERATIONS ==========

        /// <summary>
        /// Insert or update a string in cache
        /// </summary>
        public bool InsertString(CachedString str)
        {
            if (_connection?.State != System.Data.ConnectionState.Open)
                return false;

            try
            {
                using (var cmd = _connection.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT OR REPLACE INTO Strings (BinaryHash, Address, StringValue, Type, References)
                        VALUES (@hash, @addr, @value, @type, @refs)
                    ";
                    cmd.Parameters.AddWithValue("@hash", str.BinaryHash);
                    cmd.Parameters.AddWithValue("@addr", (long)str.Address);
                    cmd.Parameters.AddWithValue("@value", str.StringValue ?? "");
                    cmd.Parameters.AddWithValue("@type", str.Type ?? "");
                    cmd.Parameters.AddWithValue("@refs", str.References ?? new byte[0]);

                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] InsertString failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Query strings by binary hash
        /// </summary>
        public List<CachedString> QueryStrings(string binaryHash)
        {
            _totalQueries++;
            var result = new List<CachedString>();

            if (_connection?.State != System.Data.ConnectionState.Open)
                return result;

            try
            {
                using (var cmd = _connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM Strings WHERE BinaryHash = @hash";
                    cmd.Parameters.AddWithValue("@hash", binaryHash);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(new CachedString
                            {
                                StringID = (long)reader["StringID"],
                                BinaryHash = (string)reader["BinaryHash"],
                                Address = (ulong)(long)reader["Address"],
                                StringValue = (string)(reader["StringValue"] ?? ""),
                                Type = (string)(reader["Type"] ?? ""),
                                References = reader["References"] as byte[]
                            });
                        }
                    }

                    if (result.Count > 0)
                        _cacheHits++;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] QueryStrings failed: {ex.Message}");
            }

            return result;
        }

        // ========== CROSS-REFERENCE OPERATIONS ==========

        /// <summary>
        /// Insert or update a cross-reference
        /// </summary>
        public bool InsertCrossReference(CachedCrossReference xref)
        {
            if (_connection?.State != System.Data.ConnectionState.Open)
                return false;

            try
            {
                using (var cmd = _connection.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT OR REPLACE INTO CrossReferences (BinaryHash, SourceAddress, TargetAddress, RefType, Context)
                        VALUES (@hash, @src, @tgt, @type, @ctx)
                    ";
                    cmd.Parameters.AddWithValue("@hash", xref.BinaryHash);
                    cmd.Parameters.AddWithValue("@src", (long)xref.SourceAddress);
                    cmd.Parameters.AddWithValue("@tgt", (long)xref.TargetAddress);
                    cmd.Parameters.AddWithValue("@type", xref.RefType ?? "");
                    cmd.Parameters.AddWithValue("@ctx", xref.Context ?? "");

                    return cmd.ExecuteNonQuery() > 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] InsertCrossReference failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Query cross-references by source address
        /// </summary>
        public List<CachedCrossReference> QueryCrossReferences(string binaryHash, ulong? sourceAddress = null, ulong? targetAddress = null)
        {
            _totalQueries++;
            var result = new List<CachedCrossReference>();

            if (_connection?.State != System.Data.ConnectionState.Open)
                return result;

            try
            {
                using (var cmd = _connection.CreateCommand())
                {
                    if (sourceAddress.HasValue && !targetAddress.HasValue)
                    {
                        cmd.CommandText = "SELECT * FROM CrossReferences WHERE BinaryHash = @hash AND SourceAddress = @src";
                        cmd.Parameters.AddWithValue("@hash", binaryHash);
                        cmd.Parameters.AddWithValue("@src", (long)sourceAddress.Value);
                    }
                    else if (targetAddress.HasValue && !sourceAddress.HasValue)
                    {
                        cmd.CommandText = "SELECT * FROM CrossReferences WHERE BinaryHash = @hash AND TargetAddress = @tgt";
                        cmd.Parameters.AddWithValue("@hash", binaryHash);
                        cmd.Parameters.AddWithValue("@tgt", (long)targetAddress.Value);
                    }
                    else
                    {
                        cmd.CommandText = "SELECT * FROM CrossReferences WHERE BinaryHash = @hash";
                        cmd.Parameters.AddWithValue("@hash", binaryHash);
                    }

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(new CachedCrossReference
                            {
                                XRefID = (long)reader["XRefID"],
                                BinaryHash = (string)reader["BinaryHash"],
                                SourceAddress = (ulong)(long)reader["SourceAddress"],
                                TargetAddress = (ulong)(long)reader["TargetAddress"],
                                RefType = (string)(reader["RefType"] ?? ""),
                                Context = (string)(reader["Context"] ?? "")
                            });
                        }
                    }

                    if (result.Count > 0)
                        _cacheHits++;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] QueryCrossReferences failed: {ex.Message}");
            }

            return result;
        }

        // ========== CACHE STATISTICS ==========

        /// <summary>
        /// Get cache statistics
        /// </summary>
        public CacheStats GetCacheStats()
        {
            var stats = new CacheStats
            {
                CacheHits = _cacheHits,
                TotalQueries = _totalQueries
            };

            if (_connection?.State != System.Data.ConnectionState.Open)
                return stats;

            try
            {
                using (var cmd = _connection.CreateCommand())
                {
                    // Symbol count
                    cmd.CommandText = "SELECT COUNT(*) FROM Symbols";
                    stats.SymbolCount = (long)(cmd.ExecuteScalar() ?? 0L);

                    // String count
                    cmd.CommandText = "SELECT COUNT(*) FROM Strings";
                    stats.StringCount = (long)(cmd.ExecuteScalar() ?? 0L);

                    // CrossRef count
                    cmd.CommandText = "SELECT COUNT(*) FROM CrossReferences";
                    stats.CrossRefCount = (long)(cmd.ExecuteScalar() ?? 0L);

                    // Pattern count
                    cmd.CommandText = "SELECT COUNT(*) FROM Patterns";
                    stats.PatternCount = (long)(cmd.ExecuteScalar() ?? 0L);

                    // DB size in KB
                    if (File.Exists(_databasePath))
                    {
                        stats.DatabaseSizeKB = new FileInfo(_databasePath).Length / 1024;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] GetCacheStats failed: {ex.Message}");
            }

            return stats;
        }

        /// <summary>
        /// Clear all data for a specific binary
        /// </summary>
        public bool ClearBinaryCache(string binaryHash)
        {
            if (_connection?.State != System.Data.ConnectionState.Open)
                return false;

            try
            {
                using (var cmd = _connection.CreateCommand())
                {
                    cmd.CommandText = @"
                        DELETE FROM Symbols WHERE BinaryHash = @hash;
                        DELETE FROM Strings WHERE BinaryHash = @hash;
                        DELETE FROM CrossReferences WHERE BinaryHash = @hash;
                        DELETE FROM Disassembly WHERE BinaryHash = @hash;
                        DELETE FROM ByteRanges WHERE BinaryHash = @hash;
                        DELETE FROM Patterns WHERE BinaryHash = @hash;
                    ";
                    cmd.Parameters.AddWithValue("@hash", binaryHash);
                    cmd.ExecuteNonQuery();
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] ClearBinaryCache failed: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                _connection?.Close();
                _connection?.Dispose();
            }
            catch { }

            _disposed = true;
        }
    }
}
