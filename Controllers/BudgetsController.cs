using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BudgetApp.Data;
using BudgetApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.Controllers
{
    [Authorize]
    public class BudgetsController : Controller
    {
        private readonly BudgetContext _context;

        public BudgetsController(BudgetContext context)
        {
            _context = context;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        public async Task<IActionResult> Index(int? month, int? year)
        {
            var now = DateTime.Now;
            int selectedMonth = (month is >= 1 and <= 12) ? month.Value : now.Month;
            int selectedYear = (year is >= 2000 and <= 2100) ? year.Value : now.Year;
            var userId = CurrentUserId;

            var categories = await _context.Categories
                .Where(c => c.UserId == userId)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var budgets = await _context.CategoryBudgets
                .Include(b => b.Category)
                .Where(b => b.UserId == userId && b.Month == selectedMonth && b.Year == selectedYear)
                .ToListAsync();

            var expensesByCategory = await _context.Transactions
                .Where(t => t.UserId == userId && !t.IsIncome && t.Date.Month == selectedMonth && t.Date.Year == selectedYear)
                .GroupBy(t => t.CategoryId)
                .Select(g => new { CategoryId = g.Key, Spent = g.Sum(x => x.Amount) })
                .ToListAsync();

            var rows = categories.Select(c =>
            {
                var budget = budgets.FirstOrDefault(b => b.CategoryId == c.Id)?.Amount ?? 0m;
                var spent = expensesByCategory.FirstOrDefault(x => x.CategoryId == c.Id)?.Spent ?? 0m;
                return new BudgetRowVm
                {
                    CategoryId = c.Id,
                    CategoryName = c.Name,
                    BudgetAmount = budget,
                    SpentAmount = spent
                };
            }).ToList();

            ViewBag.Month = selectedMonth;
            ViewBag.Year = selectedYear;
            return View(rows);
        }

        public async Task<IActionResult> Edit(int categoryId, int? month, int? year)
        {
            var now = DateTime.Now;
            int selectedMonth = (month is >= 1 and <= 12) ? month.Value : now.Month;
            int selectedYear = (year is >= 2000 and <= 2100) ? year.Value : now.Year;
            var userId = CurrentUserId;

            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId);
            if (category == null) return NotFound();

            var budget = await _context.CategoryBudgets.FirstOrDefaultAsync(b =>
                b.UserId == userId && b.CategoryId == categoryId && b.Month == selectedMonth && b.Year == selectedYear);

            var vm = new EditBudgetVm
            {
                CategoryId = category.Id,
                CategoryName = category.Name,
                Month = selectedMonth,
                Year = selectedYear,
                Amount = budget?.Amount ?? 0m
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditBudgetVm vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var userId = CurrentUserId;
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == vm.CategoryId && c.UserId == userId);
            if (category == null) return NotFound();

            var existing = await _context.CategoryBudgets.FirstOrDefaultAsync(b =>
                b.UserId == userId && b.CategoryId == vm.CategoryId && b.Month == vm.Month && b.Year == vm.Year);

            if (existing == null)
            {
                existing = new CategoryBudget
                {
                    UserId = userId,
                    CategoryId = vm.CategoryId,
                    Month = vm.Month,
                    Year = vm.Year,
                    Amount = vm.Amount
                };
                _context.CategoryBudgets.Add(existing);
            }
            else
            {
                existing.Amount = vm.Amount;
            }

            await _context.SaveChangesAsync();
            TempData["StatusMessage"] = "Budget sparad.";
            return RedirectToAction(nameof(Index), new { month = vm.Month, year = vm.Year });
        }

        public async Task<IActionResult> QuickSet(int? month, int? year)
        {
            var now = DateTime.Now;
            int selectedMonth = (month is >= 1 and <= 12) ? month.Value : now.Month;
            int selectedYear = (year is >= 2000 and <= 2100) ? year.Value : now.Year;
            var userId = CurrentUserId;

            var categories = await _context.Categories
                .Where(c => c.UserId == userId)
                .OrderBy(c => c.Name)
                .ToListAsync();

            var vm = new QuickSetBudgetVm
            {
                Month = selectedMonth,
                Year = selectedYear,
                Rows = categories.Select(c => new QuickSetBudgetRowVm
                {
                    CategoryId = c.Id,
                    CategoryName = c.Name,
                    Amount = 0m
                }).ToList()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickSet(QuickSetBudgetVm vm)
        {
            var userId = CurrentUserId;

            foreach (var row in vm.Rows)
            {
                var categoryExists = await _context.Categories.AnyAsync(c => c.Id == row.CategoryId && c.UserId == userId);
                if (!categoryExists) continue;

                var existing = await _context.CategoryBudgets.FirstOrDefaultAsync(b =>
                    b.UserId == userId && b.CategoryId == row.CategoryId && b.Month == vm.Month && b.Year == vm.Year);

                if (existing == null)
                {
                    _context.CategoryBudgets.Add(new CategoryBudget
                    {
                        UserId = userId,
                        CategoryId = row.CategoryId,
                        Month = vm.Month,
                        Year = vm.Year,
                        Amount = row.Amount
                    });
                }
                else
                {
                    existing.Amount = row.Amount;
                }
            }

            await _context.SaveChangesAsync();
            TempData["StatusMessage"] = "Budgetar sparade.";
            return RedirectToAction(nameof(Index), new { month = vm.Month, year = vm.Year });
        }
    }

    public class BudgetRowVm
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = "";
        public decimal BudgetAmount { get; set; }
        public decimal SpentAmount { get; set; }
        public decimal Remaining => BudgetAmount - SpentAmount;
        public bool OverBudget => SpentAmount > BudgetAmount && BudgetAmount > 0;
    }

    public class EditBudgetVm
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = "";

        [Range(1, 12)]
        public int Month { get; set; }

        [Range(2000, 2100)]
        public int Year { get; set; }

        [Range(typeof(decimal), "0", "999999999")]
        public decimal Amount { get; set; }
    }

    public class QuickSetBudgetVm
    {
        [Range(1, 12)]
        public int Month { get; set; }

        [Range(2000, 2100)]
        public int Year { get; set; }

        public List<QuickSetBudgetRowVm> Rows { get; set; } = new();
    }

    public class QuickSetBudgetRowVm
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = "";

        [Range(typeof(decimal), "0", "999999999")]
        public decimal Amount { get; set; }
    }
}
