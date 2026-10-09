using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureOKCS.Data;
using SecureOKCS.ViewModels;

namespace SecureOKCS.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AuditLogsController : Controller
    {
        private readonly ApplicationDbContext
            _context;

        public AuditLogsController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(
            string? search,
            string? actionType)
        {
            var query =
                _context.AuditLogs
                    .Include(log =>
                        log.User)
                    .ThenInclude(user =>
                        user!.Role)
                    .AsNoTracking()
                    .AsQueryable();

            if (!string.IsNullOrWhiteSpace(
                    search))
            {
                string searchValue =
                    search.Trim();

                query = query.Where(log =>
                    log.ActionType.Contains(
                        searchValue) ||

                    log.TableName.Contains(
                        searchValue) ||

                    (log.RecordId != null &&
                     log.RecordId.Contains(
                         searchValue)) ||

                    (log.ActionDetails != null &&
                     log.ActionDetails.Contains(
                         searchValue)) ||

                    (log.IpAddress != null &&
                     log.IpAddress.Contains(
                         searchValue)) ||

                    (log.User != null &&
                     log.User.FullName.Contains(
                         searchValue)));
            }

            if (!string.IsNullOrWhiteSpace(
                    actionType))
            {
                query = query.Where(log =>
                    log.ActionType ==
                    actionType);
            }

            ViewBag.Search =
                search;

            ViewBag.ActionType =
                actionType;

            ViewBag.ActionTypes =
                await _context.AuditLogs
                    .Select(log =>
                        log.ActionType)
                    .Distinct()
                    .OrderBy(action =>
                        action)
                    .ToListAsync();

            var logs =
                await query
                    .OrderByDescending(log =>
                        log.CreatedAt)
                    .Take(500)
                    .ToListAsync();

            var result =
                logs.Select(log =>
                    new AuditLogDisplayViewModel
                    {
                        AuditId =
                            log.AuditId,

                        UserName =
                            log.User?.FullName
                            ?? "System",

                        RoleName =
                            log.User?.Role?.RoleName
                            ?? "System",

                        ActionType =
                            log.ActionType,

                        TableName =
                            log.TableName,

                        RecordId =
                            log.RecordId,

                        ActionDetails =
                            log.ActionDetails,

                        IpAddress =
                            log.IpAddress,

                        CreatedAt =
                            log.CreatedAt
                    })
                .ToList();

            return View(result);
        }

        // EXPORT ALL AUDIT LOGS TO EXCEL
        [HttpGet]
        public async Task<IActionResult> ExportExcel()
        {
            var logs =
                await _context.AuditLogs
                    .Include(log =>
                        log.User)
                    .ThenInclude(user =>
                        user!.Role)
                    .AsNoTracking()
                    .OrderByDescending(log =>
                        log.CreatedAt)
                    .ToListAsync();

            using var workbook =
                new XLWorkbook();

            var worksheet =
                workbook.Worksheets.Add(
                    "Audit Logs");

            // EXCEL HEADERS
            worksheet.Cell(1, 1).Value =
                "Audit ID";

            worksheet.Cell(1, 2).Value =
                "Date and Time";

            worksheet.Cell(1, 3).Value =
                "User";

            worksheet.Cell(1, 4).Value =
                "Role";

            worksheet.Cell(1, 5).Value =
                "Action";

            worksheet.Cell(1, 6).Value =
                "Resource";

            worksheet.Cell(1, 7).Value =
                "Record ID";

            worksheet.Cell(1, 8).Value =
                "IP Address";

            worksheet.Cell(1, 9).Value =
                "Event Details";

            var header =
                worksheet.Range(
                    1,
                    1,
                    1,
                    9);

            header.Style.Font.Bold =
                true;

            header.Style.Font.FontColor =
                XLColor.White;

            header.Style.Fill.BackgroundColor =
                XLColor.FromHtml(
                    "#0F172A");

            header.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            header.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            int row = 2;

            foreach (var log in logs)
            {
                worksheet.Cell(row, 1).Value =
                    log.AuditId;

                worksheet.Cell(row, 2).Value =
                    log.CreatedAt;

                worksheet.Cell(row, 3).Value =
                    SafeExcelText(
                        log.User?.FullName
                        ?? "System");

                worksheet.Cell(row, 4).Value =
                    SafeExcelText(
                        log.User?.Role?.RoleName
                        ?? "System");

                worksheet.Cell(row, 5).Value =
                    SafeExcelText(
                        log.ActionType);

                worksheet.Cell(row, 6).Value =
                    SafeExcelText(
                        log.TableName);

                worksheet.Cell(row, 7).Value =
                    SafeExcelText(
                        log.RecordId);

                worksheet.Cell(row, 8).Value =
                    SafeExcelText(
                        log.IpAddress);

                worksheet.Cell(row, 9).Value =
                    SafeExcelText(
                        log.ActionDetails);

                row++;
            }

            // DATE FORMAT
            worksheet.Column(2)
                .Style.NumberFormat.Format =
                "dd MMM yyyy hh:mm:ss AM/PM";

            // EXCEL FORMATTING
            worksheet.SheetView.FreezeRows(1);

            worksheet.Columns(1, 8)
                .AdjustToContents();

            worksheet.Column(9).Width =
                60;

            worksheet.Column(9)
                .Style.Alignment.WrapText =
                true;

            worksheet.Rows()
                .Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Top;

            worksheet.Row(1).Height =
                24;

            if (logs.Count > 0)
            {
                worksheet.Range(
                        1,
                        1,
                        row - 1,
                        9)
                    .SetAutoFilter();

                worksheet.Range(
                        1,
                        1,
                        row - 1,
                        9)
                    .Style.Border.OutsideBorder =
                    XLBorderStyleValues.Thin;

                worksheet.Range(
                        1,
                        1,
                        row - 1,
                        9)
                    .Style.Border.InsideBorder =
                    XLBorderStyleValues.Thin;
            }

            using var stream =
                new MemoryStream();

            workbook.SaveAs(stream);

            string fileName =
                $"SecureOKCS_AuditLogs_" +
                $"{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-" +
                "officedocument.spreadsheetml.sheet",
                fileName);
        }

        // PREVENT EXCEL FORMULA INJECTION
        private static string SafeExcelText(
            string? value)
        {
            if (string.IsNullOrWhiteSpace(
                    value))
            {
                return string.Empty;
            }

            string safeValue =
                value.Trim();

            if (safeValue[0] is '=' or '+' or '-' or '@')
            {
                return "'" + safeValue;
            }

            return safeValue;
        }
    }
}