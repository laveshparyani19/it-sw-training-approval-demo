using Npgsql;
using System.Data;
using Microsoft.Extensions.Configuration;
using ApprovalDemo.Api.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ApprovalDemo.Api.Data
{
    public class ApprovalRepository
    {
        private readonly string _connectionString;

        public ApprovalRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        public async Task<int> CreateRequestAsync(CreateRequestDto dto)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            const string sql = "INSERT INTO \"ApprovalRequest\" (\"Title\", \"RequestedBy\", \"Status\", \"CreatedAt\") " +
                               "VALUES(@Title, @RequestedBy, 0, NOW()) " +
                               "RETURNING \"Id\"";

            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Title", dto.Title);
            command.Parameters.AddWithValue("@RequestedBy", dto.RequestedBy);

            await connection.OpenAsync();
            var id = (int)await command.ExecuteScalarAsync();
            return id;
        }

        public async Task<List<ApprovalRequest>> GetPendingRequestsAsync()
        {
            var requests = new List<ApprovalRequest>();

            using var connection = new NpgsqlConnection(_connectionString);
            const string sql = "SELECT * FROM \"ApprovalRequest\" WHERE \"Status\" = 0 ORDER BY \"CreatedAt\" DESC";

            using var command = new NpgsqlCommand(sql, connection);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                requests.Add(MapToApprovalRequest(reader));
            }
            return requests;
        }

        public async Task<int> ApproveRequestAsync(int id, string decisionBy)
        {
            Console.WriteLine($"Repository: Approving request {id} by {decisionBy}");

            using var connection = new NpgsqlConnection(_connectionString);
            const string sql = "UPDATE \"ApprovalRequest\" " +
                               "SET \"Status\" = 1, " +
                               "\"DecisionBy\" = @DecisionBy, " +
                               "\"DecisionAt\" = NOW() " +
                               "WHERE \"Id\" = @Id";

            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Id", id);
            command.Parameters.AddWithValue("@DecisionBy", decisionBy);

            await connection.OpenAsync();
            int rows = await command.ExecuteNonQueryAsync();
            Console.WriteLine($"Repository: Approved {id}, rows affected: {rows}");
            return rows;
        }

        public async Task<int> RejectRequestAsync(int id, string decisionBy, string rejectReason)
        {
            Console.WriteLine($"Repository: Rejecting request {id} by {decisionBy}, reason: {rejectReason}");

            using var connection = new NpgsqlConnection(_connectionString);
            const string sql = "UPDATE \"ApprovalRequest\" " +
                               "SET \"Status\" = 2, " +
                               "\"DecisionBy\" = @DecisionBy, " +
                               "\"DecisionAt\" = NOW(), " +
                               "\"RejectReason\" = @RejectReason " +
                               "WHERE \"Id\" = @Id";

            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Id", id);
            command.Parameters.AddWithValue("@DecisionBy", decisionBy);
            command.Parameters.AddWithValue("@RejectReason", rejectReason);

            await connection.OpenAsync();
            int rows = await command.ExecuteNonQueryAsync();
            Console.WriteLine($"Repository: Rejected {id}, rows affected: {rows}");
            return rows;
        }

        public async Task<ApprovalRequest?> GetByIdAsync(int id)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            const string sql = "SELECT * FROM \"ApprovalRequest\" WHERE \"Id\" = @Id";

            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Id", id);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapToApprovalRequest(reader);
            }
            return null;
        }

        private static ApprovalRequest MapToApprovalRequest(NpgsqlDataReader reader)
        {
            return new ApprovalRequest
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                Title = reader.GetString(reader.GetOrdinal("Title")),
                RequestedBy = reader.GetString(reader.GetOrdinal("RequestedBy")),
                Status = (byte)reader.GetInt16(reader.GetOrdinal("Status")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                DecisionBy = reader.IsDBNull(reader.GetOrdinal("DecisionBy")) ? null : reader.GetString(reader.GetOrdinal("DecisionBy")),
                DecisionAt = reader.IsDBNull(reader.GetOrdinal("DecisionAt")) ? null : reader.GetDateTime(reader.GetOrdinal("DecisionAt")),
                RejectReason = reader.IsDBNull(reader.GetOrdinal("RejectReason")) ? null : reader.GetString(reader.GetOrdinal("RejectReason"))
            };
        }
    }
}
