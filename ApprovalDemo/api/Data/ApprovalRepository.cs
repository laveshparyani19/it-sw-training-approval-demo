using Microsoft.Data.SqlClient;
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
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("SP_ApprovalRequest_Create", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@Title", dto.Title);
            command.Parameters.AddWithValue("@RequestedBy", dto.RequestedBy);

            await connection.OpenAsync();
            var id = (int)await command.ExecuteScalarAsync();
            return id;
        }

        public async Task<List<ApprovalRequest>> GetPendingRequestsAsync()
        {
            var requests = new List<ApprovalRequest>();
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("SP_ApprovalRequest_GetPending", connection);
            command.CommandType = CommandType.StoredProcedure;

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
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("SP_ApprovalRequest_Approve", connection);
            command.CommandType = CommandType.StoredProcedure;
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
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("SP_ApprovalRequest_Reject", connection);
            command.CommandType = CommandType.StoredProcedure;
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
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("SP_ApprovalRequest_GetById", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@Id", id);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapToApprovalRequest(reader);
            }
            return null;
        }

        private static ApprovalRequest MapToApprovalRequest(SqlDataReader reader)
        {
            return new ApprovalRequest
            {
                Id = reader.GetInt32("Id"),
                Title = reader.GetString("Title"),
                RequestedBy = reader.GetString("RequestedBy"),
                Status = reader.GetByte("Status"),
                CreatedAt = reader.GetDateTime("CreatedAt"),
                DecisionBy = reader.IsDBNull("DecisionBy") ? null : reader.GetString("DecisionBy"),
                DecisionAt = reader.IsDBNull("DecisionAt") ? null : reader.GetDateTime("DecisionAt"),
                RejectReason = reader.IsDBNull("RejectReason") ? null : reader.GetString("RejectReason")
            };
        }
    }
}
