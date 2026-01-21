using Npgsql;
using PaymentOps.Backend.Application.Interfaces;
using PaymentOps.Backend.Domain;

namespace PaymentOps.Backend.Infrastructure;

/// <summary>
/// PostgreSQL with pgvector extension implementation of <see cref="IVectorStore"/>.
/// </summary>
public class PostgresPgVectorStore : IVectorStore
{
    private readonly string _connectionString;
    private readonly string _tableName;
    private readonly string _schemaName;
    private readonly int _vectorSize;
    private readonly ILogger<PostgresPgVectorStore> _logger;

    public PostgresPgVectorStore(
        string connectionString,
        string tableName,
        string schemaName,
        int vectorSize,
        ILogger<PostgresPgVectorStore> logger)
    {
        _connectionString = connectionString;
        _tableName = tableName;
        _schemaName = schemaName;
        _vectorSize = vectorSize;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Enable pgvector extension
        await using (var cmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector", connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Create schema if it doesn't exist
        await using (var cmd = new NpgsqlCommand($"CREATE SCHEMA IF NOT EXISTS {_schemaName}", connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Check if table exists
        var tableExists = false;
        await using (var cmd = new NpgsqlCommand(
            $"SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_schema = '{_schemaName}' AND table_name = '{_tableName}')",
            connection))
        {
            tableExists = (bool)(await cmd.ExecuteScalarAsync(cancellationToken) ?? false);
        }

        if (tableExists)
        {
            _logger.LogInformation("Table {SchemaName}.{TableName} already exists", _schemaName, _tableName);
            return;
        }

        // Create table with vector column
        var createTableSql = $@"
            CREATE TABLE {_schemaName}.{_tableName} (
                id UUID PRIMARY KEY,
                doc_id UUID NOT NULL,
                doc_name TEXT NOT NULL,
                source_path TEXT,
                hash TEXT NOT NULL,
                chunk_index INTEGER NOT NULL,
                text TEXT NOT NULL,
                snippet TEXT NOT NULL,
                embedding vector({_vectorSize}) NOT NULL,
                created_utc TIMESTAMP NOT NULL
            )";

        await using (var cmd = new NpgsqlCommand(createTableSql, connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Create indexes for better performance
        await using (var cmd = new NpgsqlCommand(
            $"CREATE INDEX ON {_schemaName}.{_tableName} USING ivfflat (embedding vector_cosine_ops) WITH (lists = 100)",
            connection))
        {
            try
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (PostgresException ex) when (ex.SqlState == "42P17")
            {
                // Index already exists, ignore
                _logger.LogDebug("Index already exists or could not be created: {Message}", ex.Message);
            }
        }

        await using (var cmd = new NpgsqlCommand(
            $"CREATE INDEX ON {_schemaName}.{_tableName} (doc_id)",
            connection))
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        _logger.LogInformation("Created table {SchemaName}.{TableName}", _schemaName, _tableName);
    }

    public async Task UpsertChunksAsync(IEnumerable<Chunk> chunks, CancellationToken cancellationToken = default)
    {
        var chunksList = chunks.ToList();
        if (chunksList.Count == 0) return;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        _logger.LogDebug("Upserting {Count} chunks to PostgreSQL", chunksList.Count);

        foreach (var chunk in chunksList)
        {
            if (chunk.Embedding == null)
            {
                throw new InvalidOperationException("Chunk must have embedding");
            }

            var sql = $@"
                INSERT INTO {_schemaName}.{_tableName} 
                    (id, doc_id, doc_name, source_path, hash, chunk_index, text, snippet, embedding, created_utc)
                VALUES 
                    (@id, @docId, @docName, @sourcePath, @hash, @chunkIndex, @text, @snippet, @embedding, @createdUtc)
                ON CONFLICT (id) DO UPDATE SET
                    doc_id = EXCLUDED.doc_id,
                    doc_name = EXCLUDED.doc_name,
                    source_path = EXCLUDED.source_path,
                    hash = EXCLUDED.hash,
                    chunk_index = EXCLUDED.chunk_index,
                    text = EXCLUDED.text,
                    snippet = EXCLUDED.snippet,
                    embedding = EXCLUDED.embedding,
                    created_utc = EXCLUDED.created_utc";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("id", chunk.Id);
            cmd.Parameters.AddWithValue("docId", chunk.DocumentId);
            cmd.Parameters.AddWithValue("docName", chunk.DocumentName);
            cmd.Parameters.AddWithValue("sourcePath", DBNull.Value);
            cmd.Parameters.AddWithValue("hash", chunk.Hash);
            cmd.Parameters.AddWithValue("chunkIndex", chunk.Index);
            cmd.Parameters.AddWithValue("text", chunk.Text);
            cmd.Parameters.AddWithValue("snippet", chunk.Snippet);
            cmd.Parameters.AddWithValue("embedding", chunk.Embedding);
            cmd.Parameters.AddWithValue("createdUtc", chunk.CreatedUtc);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        _logger.LogInformation("Upserted {Count} chunks", chunksList.Count);
    }

    public async Task<List<Chunk>> SearchAsync(float[] queryVector, int topK, float? minScore = null, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // pgvector cosine distance: 1 - cosine_similarity
        // We want higher scores for more similar vectors, so we use 1 - distance
        var sql = $@"
            SELECT 
                id, doc_id, doc_name, source_path, hash, chunk_index, text, snippet, created_utc,
                1 - (embedding <=> @queryVector::vector) as similarity
            FROM {_schemaName}.{_tableName}
            ORDER BY embedding <=> @queryVector::vector
            LIMIT @topK";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("queryVector", queryVector);
        cmd.Parameters.AddWithValue("topK", topK);

        var chunks = new List<Chunk>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var similarity = reader.GetDouble(9); // similarity score

            if (minScore.HasValue && similarity < minScore.Value)
            {
                continue;
            }

            chunks.Add(new Chunk
            {
                Id = reader.GetGuid(0),
                DocumentId = reader.GetGuid(1),
                DocumentName = reader.GetString(2),
                Hash = reader.GetString(4),
                Index = reader.GetInt32(5),
                Text = reader.GetString(6),
                Snippet = reader.GetString(7),
                CreatedUtc = reader.GetDateTime(8)
            });
        }

        return chunks;
    }

    public async Task<List<Document>> GetDocumentsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = $@"
            SELECT 
                doc_id,
                MAX(doc_name) as doc_name,
                MAX(source_path) as source_path,
                MIN(created_utc) as created_utc,
                COUNT(*) as chunk_count,
                SUM(LENGTH(text)) as total_size_bytes
            FROM {_schemaName}.{_tableName}
            GROUP BY doc_id";

        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var documents = new List<Document>();
        while (await reader.ReadAsync(cancellationToken))
        {
            documents.Add(new Document
            {
                Id = reader.GetGuid(0),
                Name = reader.GetString(1),
                SourcePath = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                CreatedUtc = reader.GetDateTime(3),
                ChunkCount = reader.GetInt32(4),
                TotalSizeBytes = reader.GetInt64(5)
            });
        }

        return documents;
    }

    public async Task<Document?> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var documents = await GetDocumentsAsync(cancellationToken);
        return documents.FirstOrDefault(d => d.Id == documentId);
    }

    public async Task<List<Chunk>> GetDocumentChunksAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = $@"
            SELECT id, doc_id, doc_name, source_path, hash, chunk_index, text, snippet, created_utc
            FROM {_schemaName}.{_tableName}
            WHERE doc_id = @docId
            ORDER BY chunk_index";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("docId", documentId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var chunks = new List<Chunk>();
        while (await reader.ReadAsync(cancellationToken))
        {
            chunks.Add(new Chunk
            {
                Id = reader.GetGuid(0),
                DocumentId = reader.GetGuid(1),
                DocumentName = reader.GetString(2),
                Hash = reader.GetString(4),
                Index = reader.GetInt32(5),
                Text = reader.GetString(6),
                Snippet = reader.GetString(7),
                CreatedUtc = reader.GetDateTime(8)
            });
        }

        return chunks;
    }
}
