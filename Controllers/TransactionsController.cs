using System.Security.Claims;
using System.Text;
using BudgetApp.Data;
using BudgetApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.Controllers
{
    [Authorize]
    public class TransactionsController : Controller
    {
        private readonly BudgetContext _context;

        public TransactionsController(BudgetContext context)
        {
            _context = context;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        public async Task<IActionResult> Index(int? month, int? year, int? categoryId)
        {
            var userId = CurrentUserId;
            var now = DateTime.Now;
            int selectedMonth = (month is >= 1 and <= 12) ? month.Value : now.Month;
            int selectedYear = (year is >= 2000 and <= 2100) ? year.Value : now.Year;

            var query = _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId &&
                            t.Date.Month == selectedMonth &&
                            t.Date.Year == selectedYear);

            if (categoryId.HasValue && categoryId.Value > 0)
                query = query.Where(t => t.CategoryId == categoryId.Value);

            var transactions = await query.OrderByDescending(t => t.Date).ToListAsync();

            ViewBag.Categories = new SelectList(
                await _context.Categories
                    .Where(c => c.UserId == userId)
                    .OrderBy(c => c.Name)
                    .ToListAsync(),
                "Id",
                "Name",
                categoryId
            );

            ViewBag.Month = selectedMonth;
            ViewBag.Year = selectedYear;
            ViewBag.CategoryId = categoryId ?? 0;

            ViewBag.TotalIncome = transactions.Where(t => t.IsIncome).Sum(t => t.Amount);
            ViewBag.TotalExpense = transactions.Where(t => !t.IsIncome).Sum(t => t.Amount);
            ViewBag.Balance = (decimal)ViewBag.TotalIncome - (decimal)ViewBag.TotalExpense;

            return View(transactions);
        }

        public async Task<IActionResult> ExportCsv(int? month, int? year, int? categoryId)
        {
            var userId = CurrentUserId;
            var now = DateTime.Now;
            int selectedMonth = (month is >= 1 and <= 12) ? month.Value : now.Month;
            int selectedYear = (year is >= 2000 and <= 2100) ? year.Value : now.Year;

            var query = _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId &&
                            t.Date.Month == selectedMonth &&
                            t.Date.Year == selectedYear);

            if (categoryId.HasValue && categoryId.Value > 0)
                query = query.Where(t => t.CategoryId == categoryId.Value);

            var transactions = await query.OrderBy(t => t.Date).ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("Datum;Beskrivning;Kategori;Typ;Belopp;Återkommande;DagIMånaden");

            foreach (var t in transactions)
            {
                var date = t.Date.ToString("yyyy-MM-dd");
                var description = (t.Description ?? "").Replace(";", ",");
                var category = (t.Category?.Name ?? "").Replace(";", ",");
                var type = t.IsIncome ? "Inkomst" : "Utgift";
                var amount = t.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                var recurring = t.IsRecurring ? "Ja" : "Nej";

                sb.AppendLine($"{date};{description};{category};{type};{amount};{recurring};{t.RecurringDayOfMonth}");
            }

            var utf8WithBom = new UTF8Encoding(true);
            var bytes = utf8WithBom.GetBytes(sb.ToString());

            var fileName = $"transaktioner_{selectedYear}_{selectedMonth:D2}.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateRecurring(int? month, int? year)
        {
            var userId = CurrentUserId;
            var now = DateTime.Now;
            int selectedMonth = (month is >= 1 and <= 12) ? month.Value : now.Month;
            int selectedYear = (year is >= 2000 and <= 2100) ? year.Value : now.Year;

            var recurringTemplates = await _context.Transactions
                .Where(t => t.UserId == userId && t.IsRecurring)
                .ToListAsync();

            int added = 0;

            foreach (var template in recurringTemplates)
            {
                int day = Math.Min(Math.Max(template.RecurringDayOfMonth, 1), DateTime.DaysInMonth(selectedYear, selectedMonth));
                var targetDate = new DateTime(selectedYear, selectedMonth, day);

                bool exists = await _context.Transactions.AnyAsync(t =>
                    t.UserId == userId &&
                    t.Description == template.Description &&
                    t.Amount == template.Amount &&
                    t.CategoryId == template.CategoryId &&
                    t.IsIncome == template.IsIncome &&
                    t.Date.Date == targetDate.Date);

                if (!exists)
                {
                    _context.Transactions.Add(new Transaction
                    {
                        Description = template.Description,
                        Amount = template.Amount,
                        Date = targetDate,
                        IsIncome = template.IsIncome,
                        CategoryId = template.CategoryId,
                        IsRecurring = true,
                        RecurringDayOfMonth = template.RecurringDayOfMonth,
                        UserId = userId
                    });
                    added++;
                }
            }

            await _context.SaveChangesAsync();
            TempData["StatusMessage"] = added > 0
                ? $"{added} återkommande transaktion(er) skapades."
                : "Inga nya återkommande transaktioner behövde skapas.";

            return RedirectToAction(nameof(Index), new { month = selectedMonth, year = selectedYear });
        }

        public async Task<IActionResult> Create()
        {
            await LoadCategories();
            return View(new Transaction { Date = DateTime.Today });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Transaction transaction)
        {
            if (!ModelState.IsValid)
            {
                await LoadCategories(transaction.CategoryId);
                return View(transaction);
            }

            transaction.UserId = CurrentUserId;
            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Transaktionen sparades.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var userId = CurrentUserId;

            var transaction = await _context.Transactions
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (transaction == null) return NotFound();

            await LoadCategories(transaction.CategoryId);
            return View(transaction);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Transaction transaction)
        {
            if (id != transaction.Id) return NotFound();

            var userId = CurrentUserId;
            var existing = await _context.Transactions
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (existing == null) return NotFound();

            if (!ModelState.IsValid)
            {
                await LoadCategories(transaction.CategoryId);
                return View(transaction);
            }

            existing.Description = transaction.Description;
            existing.Amount = transaction.Amount;
            existing.Date = transaction.Date;
            existing.IsIncome = transaction.IsIncome;
            existing.CategoryId = transaction.CategoryId;
            existing.IsRecurring = transaction.IsRecurring;
            existing.RecurringDayOfMonth = transaction.RecurringDayOfMonth;

            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Transaktionen uppdaterades.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var userId = CurrentUserId;

            var transaction = await _context.Transactions
                .Include(t => t.Category)
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

            if (transaction == null) return NotFound();

            return View(transaction);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = CurrentUserId;

            var transaction = await _context.Transactions
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (transaction != null) _context.Transactions.Remove(transaction);

            await _context.SaveChangesAsync();
            TempData["StatusMessage"] = "Transaktionen togs bort.";

            return RedirectToAction(nameof(Index));
        }

        private async Task LoadCategories(int? selectedId = null)
        {
            var userId = CurrentUserId;
            var categories = await _context.Categories
                .Where(c => c.UserId == userId)
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.CategoryId = new SelectList(categories, "Id", "Name", selectedId);
        }
    }
}