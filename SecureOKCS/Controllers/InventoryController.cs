using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureOKCS.Data;
using SecureOKCS.Models;
using SecureOKCS.Services;
using SecureOKCS.ViewModels;

namespace SecureOKCS.Controllers
{
    [Authorize(Roles = "Admin,Staff")]
    public class InventoryController : Controller
    {
        private readonly ApplicationDbContext
            _context;

        private readonly AuditLogService
            _auditLog;

        public InventoryController(
            ApplicationDbContext context,
            AuditLogService auditLog)
        {
            _context = context;
            _auditLog = auditLog;
        }

        // LIST INVENTORY
        public async Task<IActionResult> Index(
            string? search,
            bool lowStockOnly = false)
        {
            var activeItems =
                _context.InventoryItems
                    .AsNoTracking()
                    .Where(item =>
                        item.IsActive);

            // INVENTORY SUMMARY
            ViewBag.TotalItemCount =
                await activeItems.CountAsync();

            ViewBag.LowStockCount =
                await activeItems.CountAsync(item =>
                    item.Quantity <=
                    item.MinimumStock);

            ViewBag.OutOfStockCount =
                await activeItems.CountAsync(item =>
                    item.Quantity == 0);

            ViewBag.TotalStockUnits =
                await activeItems.SumAsync(item =>
                    (int?)item.Quantity) ?? 0;

            var query =
                activeItems.AsQueryable();

            // SEARCH INVENTORY
            if (!string.IsNullOrWhiteSpace(
                    search))
            {
                string searchValue =
                    search.Trim();

                query = query.Where(item =>
                    item.ItemCode.Contains(
                        searchValue) ||

                    item.ItemName.Contains(
                        searchValue) ||

                    (item.Barcode != null &&
                     item.Barcode.Contains(
                         searchValue)));

                ViewBag.Search =
                    searchValue;
            }

            // LOW STOCK FILTER
            if (lowStockOnly)
            {
                query = query.Where(item =>
                    item.Quantity <=
                    item.MinimumStock);
            }

            ViewBag.LowStockOnly =
                lowStockOnly;

            // DISPLAY OUT-OF-STOCK ITEMS FIRST,
            // FOLLOWED BY LOW-STOCK ITEMS
            var items = await query
                .OrderBy(item =>
                    item.Quantity == 0
                        ? 0
                        : item.Quantity <=
                            item.MinimumStock
                            ? 1
                            : 2)
                .ThenBy(item =>
                    item.ItemName)
                .ToListAsync();

            return View(items);
        }

        // ADD ITEM PAGE
        [HttpGet]
        public IActionResult Create(
            string? barcode)
        {
            var model =
                new InventoryCreateViewModel();

            if (!string.IsNullOrWhiteSpace(
                    barcode))
            {
                model.Barcode =
                    barcode
                        .Trim()
                        .ToUpperInvariant();

                model.Quantity =
                    1;

                ViewBag.CreatedFromScanner =
                    true;
            }

            return View(model);
        }

        // SAVE NEW ITEM
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            InventoryCreateViewModel model,
            bool returnToScanner = false)
        {
            string itemCode =
                model.ItemCode
                    .Trim()
                    .ToUpperInvariant();

            string? barcode =
                string.IsNullOrWhiteSpace(
                    model.Barcode)
                    ? null
                    : model.Barcode
                        .Trim()
                        .ToUpperInvariant();

            bool itemCodeExists =
                await _context.InventoryItems
                    .AnyAsync(item =>
                        item.ItemCode ==
                        itemCode);

            if (itemCodeExists)
            {
                ModelState.AddModelError(
                    nameof(model.ItemCode),
                    "Item code is already in use.");
            }

            if (barcode != null)
            {
                bool barcodeExists =
                    await _context.InventoryItems
                        .AnyAsync(item =>
                            item.Barcode ==
                            barcode);

                if (barcodeExists)
                {
                    ModelState.AddModelError(
                        nameof(model.Barcode),
                        "Barcode is already in use.");
                }
            }

            if (!ModelState.IsValid)
            {
                ViewBag.CreatedFromScanner =
                    returnToScanner;

                return View(model);
            }

            if (!TryGetCurrentUserId(
                    out int userId))
            {
                return Forbid();
            }

            DateTime currentTime =
                DateTime.Now;

            var item =
                new InventoryItem
                {
                    ItemCode =
                        itemCode,

                    ItemName =
                        model.ItemName.Trim(),

                    Category =
                        model.Category?.Trim(),

                    Description =
                        model.Description?.Trim(),

                    Barcode =
                        barcode,

                    Quantity =
                        model.Quantity,

                    MinimumStock =
                        model.MinimumStock,

                    UnitPrice =
                        model.UnitPrice,

                    IsActive =
                        true,

                    CreatedAt =
                        currentTime,

                    UpdatedAt =
                        currentTime
                };

            _context.InventoryItems.Add(
                item);

            // RECORD INITIAL STOCK
            if (model.Quantity > 0)
            {
                var initialTransaction =
                    new InventoryTransaction
                    {
                        InventoryItem =
                            item,

                        UserId =
                            userId,

                        TransactionType =
                            "Initial Stock",

                        QuantityChanged =
                            model.Quantity,

                        BalanceAfter =
                            model.Quantity,

                        ScannedCode =
                            barcode,

                        TransactionNotes =
                            "Initial inventory stock",

                        CreatedAt =
                            currentTime
                    };

                _context.InventoryTransactions.Add(
                    initialTransaction);
            }

            await _context.SaveChangesAsync();

            // AUDIT LOG CREATE ITEM
            await _auditLog.LogAsync(
                "CREATE",
                "inventory_items",
                item.InventoryId.ToString(),
                $"Inventory item {item.ItemCode} " +
                $"created with initial quantity " +
                $"{item.Quantity}.");

            if (returnToScanner)
            {
                TempData["ScanSuccess"] =
                    $"{item.ItemName} was registered " +
                    $"with an initial quantity of " +
                    $"{item.Quantity}.";

                TempData["ScannedItemName"] =
                    item.ItemName;

                TempData["ScannedItemCode"] =
                    item.ItemCode;

                TempData["ScannedBarcode"] =
                    item.Barcode ?? "-";

                TempData["ScannedBalance"] =
                    item.Quantity;

                return RedirectToAction(
                    nameof(Scan));
            }

            TempData["Success"] =
                "Inventory item added successfully.";

            return RedirectToAction(
                nameof(Index));
        }

        // STOCK IN / STOCK OUT PAGE
        [HttpGet]
        public async Task<IActionResult> Stock(
            int id,
            string? scannedCode)
        {
            var item =
                await _context.InventoryItems
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item =>
                        item.InventoryId == id &&
                        item.IsActive);

            if (item == null)
                return NotFound();

            var model =
                new InventoryStockViewModel
                {
                    InventoryId =
                        item.InventoryId,

                    ItemCode =
                        item.ItemCode,

                    ItemName =
                        item.ItemName,

                    CurrentQuantity =
                        item.Quantity,

                    ScannedCode =
                        scannedCode ??
                        item.Barcode
                };

            return View(model);
        }

        // SAVE STOCK TRANSACTION
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Stock(
            InventoryStockViewModel model)
        {
            var item =
                await _context.InventoryItems
                    .FirstOrDefaultAsync(item =>
                        item.InventoryId ==
                            model.InventoryId &&
                        item.IsActive);

            if (item == null)
                return NotFound();

            string[] allowedTypes =
            {
                "Stock In",
                "Stock Out"
            };

            if (!allowedTypes.Contains(
                    model.TransactionType))
            {
                ModelState.AddModelError(
                    nameof(model.TransactionType),
                    "The transaction type is invalid.");
            }

            if (model.TransactionType ==
                    "Stock Out" &&
                model.Quantity >
                    item.Quantity)
            {
                ModelState.AddModelError(
                    nameof(model.Quantity),
                    "There is insufficient stock.");
            }

            if (!ModelState.IsValid)
            {
                model.ItemCode =
                    item.ItemCode;

                model.ItemName =
                    item.ItemName;

                model.CurrentQuantity =
                    item.Quantity;

                return View(model);
            }

            if (!TryGetCurrentUserId(
                    out int userId))
            {
                return Forbid();
            }

            int quantityChanged =
                model.TransactionType ==
                    "Stock In"
                    ? model.Quantity
                    : -model.Quantity;

            item.Quantity +=
                quantityChanged;

            item.UpdatedAt =
                DateTime.Now;

            var transaction =
                new InventoryTransaction
                {
                    InventoryId =
                        item.InventoryId,

                    UserId =
                        userId,

                    TransactionType =
                        model.TransactionType,

                    QuantityChanged =
                        quantityChanged,

                    BalanceAfter =
                        item.Quantity,

                    ScannedCode =
                        model.ScannedCode?.Trim(),

                    TransactionNotes =
                        model.TransactionNotes?.Trim(),

                    CreatedAt =
                        DateTime.Now
                };

            _context.InventoryTransactions.Add(
                transaction);

            await _context.SaveChangesAsync();

            string auditAction =
                model.TransactionType ==
                    "Stock In"
                    ? "STOCK_IN"
                    : "STOCK_OUT";

            // AUDIT LOG STOCK TRANSACTION
            await _auditLog.LogAsync(
                auditAction,
                "inventory_items",
                item.InventoryId.ToString(),
                $"{model.TransactionType} of " +
                $"{model.Quantity} units for item " +
                $"{item.ItemCode}. New balance: " +
                $"{item.Quantity}.");

            if (item.Quantity == 0)
            {
                TempData["Warning"] =
                    $"{item.ItemName} is now out of stock.";
            }
            else if (item.Quantity <=
                     item.MinimumStock)
            {
                TempData["Warning"] =
                    $"{item.ItemName} has reached " +
                    "the minimum stock level.";
            }

            TempData["Success"] =
                "Stock updated successfully.";

            return RedirectToAction(
                nameof(Index));
        }

        // SCANNER PAGE
        [HttpGet]
        public IActionResult Scan()
        {
            return View(
                new InventoryScanViewModel());
        }

        // QUICK STOCK-IN USING BARCODE SCANNER
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Scan(
            InventoryScanViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (!TryGetCurrentUserId(
                    out int userId))
            {
                return Forbid();
            }

            string scannedCode =
                model.ScannedCode
                    .Trim()
                    .ToUpperInvariant();

            await using var databaseTransaction =
                await _context.Database
                    .BeginTransactionAsync(
                        IsolationLevel.Serializable);

            var item =
                await _context.InventoryItems
                    .FirstOrDefaultAsync(item =>
                        item.Barcode ==
                            scannedCode ||
                        item.ItemCode ==
                            scannedCode);

            if (item == null)
            {
                await databaseTransaction
                    .RollbackAsync();

                TempData["Info"] =
                    "This barcode is not registered. " +
                    "Complete the item information " +
                    "to create a new inventory item.";

                return RedirectToAction(
                    nameof(Create),
                    new
                    {
                        barcode =
                            scannedCode
                    });
            }

            if (!item.IsActive)
            {
                await databaseTransaction
                    .RollbackAsync();

                TempData["Error"] =
                    "This barcode belongs to an " +
                    "archived inventory item. An " +
                    "administrator must restore the " +
                    "item before it can be scanned.";

                return RedirectToAction(
                    nameof(Scan));
            }

            DateTime currentTime =
                DateTime.Now;

            item.Quantity +=
                1;

            item.UpdatedAt =
                currentTime;

            var inventoryTransaction =
                new InventoryTransaction
                {
                    InventoryId =
                        item.InventoryId,

                    UserId =
                        userId,

                    TransactionType =
                        "Stock In",

                    QuantityChanged =
                        1,

                    BalanceAfter =
                        item.Quantity,

                    ScannedCode =
                        scannedCode,

                    TransactionNotes =
                        "Quick barcode stock-in",

                    CreatedAt =
                        currentTime
                };

            _context.InventoryTransactions.Add(
                inventoryTransaction);

            await _context.SaveChangesAsync();

            await databaseTransaction
                .CommitAsync();

            try
            {
                await _auditLog.LogAsync(
                    "QUICK_STOCK_IN",
                    "inventory_items",
                    item.InventoryId.ToString(),
                    $"Barcode {scannedCode} was " +
                    $"scanned. Added 1 unit to " +
                    $"{item.ItemCode}. New balance: " +
                    $"{item.Quantity}.");
            }
            catch
            {
                TempData["Warning"] =
                    "Stock was updated, but the " +
                    "audit log could not be recorded.";
            }

            TempData["ScanSuccess"] =
                $"{item.ItemName} stock increased " +
                "by 1 unit.";

            TempData["ScannedItemName"] =
                item.ItemName;

            TempData["ScannedItemCode"] =
                item.ItemCode;

            TempData["ScannedBarcode"] =
                scannedCode;

            TempData["ScannedBalance"] =
                item.Quantity;

            return RedirectToAction(
                nameof(Scan));
        }

        // TRANSACTION HISTORY
        public async Task<IActionResult> History(
            int id)
        {
            var item =
                await _context.InventoryItems
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item =>
                        item.InventoryId == id);

            if (item == null)
                return NotFound();

            var transactions =
                await _context
                    .InventoryTransactions
                    .AsNoTracking()
                    .Include(transaction =>
                        transaction.User)
                    .Where(transaction =>
                        transaction.InventoryId ==
                        id)
                    .OrderByDescending(transaction =>
                        transaction.CreatedAt)
                    .ToListAsync();

            ViewBag.ItemName =
                item.ItemName;

            ViewBag.ItemCode =
                item.ItemCode;

            return View(transactions);
        }

        // ALL STOCK MOVEMENT REPORT
        [HttpGet]
        public async Task<IActionResult> Transactions(
            string? search,
            string? transactionType,
            DateTime? dateFrom,
            DateTime? dateTo)
        {
            string[] allowedTypes =
            {
        "Initial Stock",
        "Stock In",
        "Stock Out"
    };

            var query =
                _context.InventoryTransactions
                    .AsNoTracking()
                    .Include(transaction =>
                        transaction.InventoryItem)
                    .Include(transaction =>
                        transaction.User)
                    .AsQueryable();

            // SEARCH BY ITEM OR USER
            if (!string.IsNullOrWhiteSpace(
                    search))
            {
                string searchValue =
                    search.Trim();

                query = query.Where(transaction =>
                    transaction.InventoryItem
                        .ItemCode.Contains(
                            searchValue) ||

                    transaction.InventoryItem
                        .ItemName.Contains(
                            searchValue) ||

                    (transaction.User != null &&
                     transaction.User.FullName.Contains(
                         searchValue)));

                ViewBag.Search =
                    searchValue;
            }

            // FILTER TRANSACTION TYPE
            if (!string.IsNullOrWhiteSpace(
                    transactionType) &&
                allowedTypes.Contains(
                    transactionType))
            {
                query = query.Where(transaction =>
                    transaction.TransactionType ==
                    transactionType);

                ViewBag.TransactionType =
                    transactionType;
            }

            // FILTER START DATE
            if (dateFrom.HasValue)
            {
                DateTime startDate =
                    dateFrom.Value.Date;

                query = query.Where(transaction =>
                    transaction.CreatedAt >=
                    startDate);

                ViewBag.DateFrom =
                    startDate.ToString(
                        "yyyy-MM-dd");
            }

            // FILTER END DATE
            if (dateTo.HasValue)
            {
                DateTime endDateExclusive =
                    dateTo.Value.Date.AddDays(1);

                query = query.Where(transaction =>
                    transaction.CreatedAt <
                    endDateExclusive);

                ViewBag.DateTo =
                    dateTo.Value.Date.ToString(
                        "yyyy-MM-dd");
            }

            // REPORT SUMMARY
            ViewBag.TotalTransactions =
                await query.CountAsync();

            ViewBag.TotalStockIn =
                await query
                    .Where(transaction =>
                        transaction.QuantityChanged > 0)
                    .SumAsync(transaction =>
                        (int?)transaction
                            .QuantityChanged) ?? 0;

            int stockOutValue =
                await query
                    .Where(transaction =>
                        transaction.QuantityChanged < 0)
                    .SumAsync(transaction =>
                        (int?)transaction
                            .QuantityChanged) ?? 0;

            ViewBag.TotalStockOut =
                Math.Abs(stockOutValue);

            var transactions =
                await query
                    .OrderByDescending(transaction =>
                        transaction.CreatedAt)
                    .ToListAsync();

            return View(transactions);
        }

        // EDIT ITEM PAGE
        [HttpGet]
        public async Task<IActionResult> Edit(
            int id)
        {
            var item =
                await _context.InventoryItems
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item =>
                        item.InventoryId ==
                            id &&
                        item.IsActive);

            if (item == null)
                return NotFound();

            var model =
                new InventoryEditViewModel
                {
                    InventoryId =
                        item.InventoryId,

                    ItemCode =
                        item.ItemCode,

                    ItemName =
                        item.ItemName,

                    Category =
                        item.Category,

                    Description =
                        item.Description,

                    Barcode =
                        item.Barcode,

                    MinimumStock =
                        item.MinimumStock,

                    UnitPrice =
                        item.UnitPrice
                };

            return View(model);
        }

        // SAVE EDITED ITEM
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            InventoryEditViewModel model)
        {
            var item =
                await _context.InventoryItems
                    .FirstOrDefaultAsync(item =>
                        item.InventoryId ==
                            model.InventoryId &&
                        item.IsActive);

            if (item == null)
                return NotFound();

            string itemCode =
                model.ItemCode
                    .Trim()
                    .ToUpperInvariant();

            string? barcode =
                string.IsNullOrWhiteSpace(
                    model.Barcode)
                    ? null
                    : model.Barcode
                        .Trim()
                        .ToUpperInvariant();

            bool itemCodeExists =
                await _context.InventoryItems
                    .AnyAsync(existingItem =>
                        existingItem.InventoryId !=
                            model.InventoryId &&
                        existingItem.ItemCode ==
                            itemCode);

            if (itemCodeExists)
            {
                ModelState.AddModelError(
                    nameof(model.ItemCode),
                    "Item code is already in use.");
            }

            if (barcode != null)
            {
                bool barcodeExists =
                    await _context.InventoryItems
                        .AnyAsync(existingItem =>
                            existingItem.InventoryId !=
                                model.InventoryId &&
                            existingItem.Barcode ==
                                barcode);

                if (barcodeExists)
                {
                    ModelState.AddModelError(
                        nameof(model.Barcode),
                        "Barcode is already in use.");
                }
            }

            if (!ModelState.IsValid)
                return View(model);

            item.ItemCode =
                itemCode;

            item.ItemName =
                model.ItemName.Trim();

            item.Category =
                model.Category?.Trim();

            item.Description =
                model.Description?.Trim();

            item.Barcode =
                barcode;

            item.MinimumStock =
                model.MinimumStock;

            item.UnitPrice =
                model.UnitPrice;

            item.UpdatedAt =
                DateTime.Now;

            await _context.SaveChangesAsync();

            // AUDIT LOG UPDATE ITEM
            await _auditLog.LogAsync(
                "UPDATE",
                "inventory_items",
                item.InventoryId.ToString(),
                $"Inventory item {item.ItemCode} " +
                "information updated.");

            TempData["Success"] =
                "Inventory item updated successfully.";

            return RedirectToAction(
                nameof(Index));
        }

        // ARCHIVED INVENTORY PAGE
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Archived(
            string? search)
        {
            var query =
                _context.InventoryItems
                    .AsNoTracking()
                    .Where(item =>
                        !item.IsActive);

            if (!string.IsNullOrWhiteSpace(
                    search))
            {
                string searchValue =
                    search.Trim();

                query = query.Where(item =>
                    item.ItemCode.Contains(
                        searchValue) ||

                    item.ItemName.Contains(
                        searchValue) ||

                    (item.Barcode != null &&
                     item.Barcode.Contains(
                         searchValue)));

                ViewBag.Search =
                    searchValue;
            }

            var archivedItems =
                await query
                    .OrderByDescending(item =>
                        item.UpdatedAt)
                    .ThenBy(item =>
                        item.ItemName)
                    .ToListAsync();

            return View(archivedItems);
        }

        // RESTORE ARCHIVED INVENTORY ITEM
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(
            int id)
        {
            var item =
                await _context.InventoryItems
                    .FirstOrDefaultAsync(item =>
                        item.InventoryId == id &&
                        !item.IsActive);

            if (item == null)
                return NotFound();

            item.IsActive =
                true;

            item.UpdatedAt =
                DateTime.Now;

            await _context.SaveChangesAsync();

            // AUDIT LOG RESTORE ITEM
            await _auditLog.LogAsync(
                "RESTORE",
                "inventory_items",
                item.InventoryId.ToString(),
                $"Inventory item {item.ItemCode} " +
                $"({item.ItemName}) was restored.");

            TempData["Success"] =
                "Inventory item restored successfully.";

            return RedirectToAction(
                nameof(Archived));
        }

        // DEACTIVATE INVENTORY ITEM
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(
            int id)
        {
            var item =
                await _context.InventoryItems
                    .FirstOrDefaultAsync(item =>
                        item.InventoryId == id &&
                        item.IsActive);

            if (item == null)
                return NotFound();

            // AN ITEM MUST HAVE ZERO STOCK
            if (item.Quantity > 0)
            {
                TempData["Error"] =
                    $"{item.ItemName} still has " +
                    $"{item.Quantity} unit(s) in stock. " +
                    "Complete a Stock Out transaction " +
                    "until the quantity reaches zero " +
                    "before deactivating this item.";

                return RedirectToAction(
                    nameof(Index));
            }

            item.IsActive =
                false;

            item.UpdatedAt =
                DateTime.Now;

            await _context.SaveChangesAsync();

            // AUDIT LOG DEACTIVATE ITEM
            await _auditLog.LogAsync(
                "DEACTIVATE",
                "inventory_items",
                item.InventoryId.ToString(),
                $"Inventory item {item.ItemCode} " +
                $"({item.ItemName}) was deactivated.");

            TempData["Success"] =
                "Inventory item deactivated successfully.";

            return RedirectToAction(
                nameof(Index));
        }

        // GET CURRENT USER ID
        private bool TryGetCurrentUserId(
            out int userId)
        {
            string? userIdValue =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            return int.TryParse(
                userIdValue,
                out userId);
        }
    }
}