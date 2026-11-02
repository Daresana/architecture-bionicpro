using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reports.Api.Models;
using Reports.Api.Services;

namespace Reports.Api.Controllers
{
    [ApiController]
    [Route("reports")]
    [Authorize(Roles = "prothetic_user")]
    public class ReportsController : ControllerBase
    {
        private readonly ReportService _reportService;
        private readonly ILogger<ReportsController> _logger;


        public ReportsController(ReportService reportService, ILogger<ReportsController> logger)
        {
            _reportService = reportService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetReport([FromQuery] DateOnly? date, [FromQuery] string? userId,
            CancellationToken cancellationToken)
        {
            var subjectId = User.FindFirst("sub")?.Value;

            if (string.IsNullOrWhiteSpace(subjectId))
            {
                return Unauthorized();
            }

            if (userId != null && userId != subjectId)
            {
                return Forbid();
            }

            var reportDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));


            try
            {
                // Читаем только опубликованный результат DAG.
                var batchId = await _reportService.GetBatchAsync(reportDate, cancellationToken);

                if (batchId == null)
                {
                    return Conflict(new { message = "Этот день ещё не обработан. Попробуйте позже." });
                }

                var rows = await _reportService.GetRowsAsync(subjectId, reportDate, batchId, cancellationToken);

                var report = new UsageReport
                {
                    ReportDate = reportDate,
                    UserId = subjectId,
                    Prostheses = rows
                };

                var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    WriteIndented = true
                });

                return File(Encoding.UTF8.GetBytes(json), "application/json", $"report-{reportDate:yyyy-MM-dd}.json");
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogError("Не удалось прочитать готовый отчёт из ClickHouse");

                return StatusCode(503, new { message = "Хранилище отчётов недоступно. Попробуйте позже." });
            }
        }
    }
}
