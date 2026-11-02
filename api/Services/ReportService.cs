using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Reports.Api.Models;

namespace Reports.Api.Services
{
    public class ReportService
    {
        private readonly HttpClient _httpClient;
        private readonly string _url;
        private readonly JsonSerializerOptions _jsonOptions;


        public ReportService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
            _url = configuration["ClickHouse:Url"] ?? "http://clickhouse:8123";

            var user = configuration["ClickHouse:User"] ?? "reports_api";
            var password = configuration["ClickHouse:Password"]
                ?? throw new InvalidOperationException("Не задан пароль ClickHouse");

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };
        }

        public async Task<string?> GetBatchAsync(DateOnly date, CancellationToken cancellationToken)
        {
            const string query = """
                SELECT batch_id
                FROM reports.report_periods FINAL
                WHERE report_date = {report_date:Date}
                FORMAT JSON
                """;

            var parameters = new Dictionary<string, string>
            {
                ["report_date"] = date.ToString("yyyy-MM-dd")
            };

            var periods = await ReadAsync<ReportPeriod>(query, parameters, cancellationToken);

            return periods.FirstOrDefault()?.BatchId;
        }

        public async Task<List<ReportRow>> GetRowsAsync(string userId, DateOnly date, string batchId,
            CancellationToken cancellationToken)
        {
            // Пользователь берётся из проверенного токена.
            const string query = """
                SELECT client_name, prosthesis_id, movement_count, average_signal, average_battery
                FROM reports.usage_reports FINAL
                WHERE user_id = {user_id:String}
                    AND report_date = {report_date:Date}
                    AND batch_id = {batch_id:String}
                ORDER BY prosthesis_id
                FORMAT JSON
                """;

            var parameters = new Dictionary<string, string>
            {
                ["user_id"] = userId,
                ["report_date"] = date.ToString("yyyy-MM-dd"),
                ["batch_id"] = batchId
            };

            return await ReadAsync<ReportRow>(query, parameters, cancellationToken);
        }

        private async Task<List<T>> ReadAsync<T>(string query, Dictionary<string, string> parameters,
            CancellationToken cancellationToken)
        {
            var queryParameters = new List<string>
            {
                "output_format_json_quote_64bit_integers=0"
            };

            foreach (var parameter in parameters)
            {
                queryParameters.Add($"param_{parameter.Key}={Uri.EscapeDataString(parameter.Value)}");
            }

            var url = $"{_url}/?{string.Join("&", queryParameters)}";

            using var content = new StringContent(query, Encoding.UTF8, "text/plain");
            using var response = await _httpClient.PostAsync(url, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ClickHouseResult<T>>(_jsonOptions, cancellationToken);

            return result?.Data ?? new List<T>();
        }
    }
}
