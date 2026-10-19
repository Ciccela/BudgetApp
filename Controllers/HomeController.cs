using System.Diagnostics;
using System.Security.Claims;
using BudgetApp.Data;
using BudgetApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly BudgetContext _context;

    public HomeController(ILogger<HomeController> logger, BudgetContext context)
    {
        _logger = logger;
        _context = context;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<IActionResult> Index(int? month, int? year)
    {
        var vm = await BuildDashboardVm(month, year);
        return View(vm);
    }

    // NY: Rapportvy för utskrift/PDF
    public async Task<IActionResult> Report(int? month, int? year)
    {
        var vm = await BuildDashboardVm(month, year);
        return View(vm);
    }

    private async Task<DashboardVm> BuildDashboardVm(int? month, int? year)
    {
        var userId = CurrentUserId;
        var now = DateTime.Now;
        int selectedMonth = (month is >= 1 and <= 12) ? month.Value : now.Month;
        int selectedYear = (year is >= 2000 and <= 2100) ? year.Value : now.Year;

        var monthTransactions = await _context.Transactions
            .Include(t => t.Category)
            .Where(t => t.UserId == userId &&
                        t.Date.Month == selectedMonth &&
                        t.Date.Year == selectedYear)
            .OrderByDescending(t => t.Date)
            .ToListAsync();

        var totalIncome = monthTransactions.Where(t => t.IsIncome).Sum(t => t.Amount);
        var totalExpense = monthTransactions.Where(t => !t.IsIncome).Sum(t => t.Amount);
        var balance = totalIncome - totalExpense;

        var expenseByCategory = monthTransactions
            .Where(t => !t.IsIncome)
            .GroupBy(t => new
            {
                CategoryId = t.CategoryId,
                CategoryName = t.Category != null ? t.Category.Name : "Okänd"
            })
            .Select(g => new ExpenseByCategoryVm
            {
                CategoryId = g.Key.CategoryId,
                CategoryName = g.Key.CategoryName,
                Spent = g.Sum(x => x.Amount)
            })
            .OrderByDescending(x => x.Spent)
            .ToList();

        var topExpenses = monthTransactions
            .Where(t => !t.IsIncome)
            .OrderByDescending(t => t.Amount)
            .Take(3)
            .ToList();

        int daysInMonth = DateTime.DaysInMonth(selectedYear, selectedMonth);
        var trendLabels = Enumerable.Range(1, daysInMonth)
            .Select(d => $"{selectedYear}-{selectedMonth:D2}-{d:D2}")
            .ToList();

        var trendData = new List<decimal>();
        for (int d = 1; d <= daysInMonth; d++)
        {
            var day = new DateTime(selectedYear, selectedMonth, d);
            var dayItems = monthTransactions.Where(t => t.Date.Date == day.Date).ToList();

            var dayIncome = dayItems.Where(t => t.IsIncome).Sum(t => t.Amount);
            var dayExpense = dayItems.Where(t => !t.IsIncome).Sum(t => t.Amount);
            trendData.Add(dayIncome - dayExpense);
        }

        var budgets = await _context.CategoryBudgets
            .Where(b => b.UserId == userId && b.Month == selectedMonth && b.Year == selectedYear)
            .ToListAsync();

        var budgetRows = expenseByCategory
            .Select(e =>
            {
                var budget = budgets.FirstOrDefault(b => b.CategoryId == e.CategoryId)?.Amount ?? 0m;
                return new DashboardBudgetRowVm
                {
                    CategoryName = e.CategoryName,
                    Budget = budget,
                    Spent = e.Spent
                };
            })
            .OrderByDescending(r => r.Spent)
            .ToList();

        var categoriesWithSpendIds = expenseByCategory.Select(x => x.CategoryId).ToHashSet();
        var missingBudgetOnlyRows = await _context.Categories
            .Where(c => c.UserId == userId && budgets.Select(b => b.CategoryId).Contains(c.Id) && !categoriesWithSpendIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToListAsync();

        foreach (var c in missingBudgetOnlyRows)
        {
            var budget = budgets.First(b => b.CategoryId == c.Id).Amount;
            budgetRows.Add(new DashboardBudgetRowVm
            {
                CategoryName = c.Name,
                Budget = budget,
                Spent = 0m
            });
        }

        var prev = new DateTime(selectedYear, selectedMonth, 1).AddMonths(-1);
        int prevMonth = prev.Month;
        int prevYear = prev.Year;

        var prevTransactions = await _context.Transactions
            .Where(t => t.UserId == userId &&
                        t.Date.Month == prevMonth &&
                        t.Date.Year == prevYear)
            .ToListAsync();

        var prevIncome = prevTransactions.Where(t => t.IsIncome).Sum(t => t.Amount);
        var prevExpense = prevTransactions.Where(t => !t.IsIncome).Sum(t => t.Amount);
        var prevBalance = prevIncome - prevExpense;

        var comparison = new MonthComparisonVm
        {
            CurrentMonth = selectedMonth,
            CurrentYear = selectedYear,
            PreviousMonth = prevMonth,
            PreviousYear = prevYear,
            CurrentIncome = totalIncome,
            CurrentExpense = totalExpense,
            CurrentBalance = balance,
            PreviousIncome = prevIncome,
            PreviousExpense = prevExpense,
            PreviousBalance = prevBalance
        };

        return new DashboardVm
        {
            Month = selectedMonth,
            Year = selectedYear,
            TotalIncome = totalIncome,
            TotalExpense = totalExpense,
            Balance = balance,

            Latest = monthTransactions.Take(10).ToList(),
            TopExpenses = topExpenses,

            ChartLabels = expenseByCategory.Select(x => x.CategoryName).ToList(),
            ChartData = expenseByCategory.Select(x => x.Spent).ToList(),

            TrendLabels = trendLabels,
            TrendData = trendData,

            BudgetRows = budgetRows,
            AnyOverBudget = budgetRows.Any(r => r.Budget > 0 && r.Spent > r.Budget),

            Comparison = comparison
        };
    }

    [AllowAnonymous]
    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

public class DashboardVm
{
    public int Month { get; set; }
    public int Year { get; set; }

    public decimal TotalIncome { get; set; }
    public decimal TotalExpense { get; set; }
    public decimal Balance { get; set; }

    public List<Transaction> Latest { get; set; } = new();
    public List<Transaction> TopExpenses { get; set; } = new();

    public List<string> ChartLabels { get; set; } = new();
    public List<decimal> ChartData { get; set; } = new();

    public List<string> TrendLabels { get; set; } = new();
    public List<decimal> TrendData { get; set; } = new();

    public List<DashboardBudgetRowVm> BudgetRows { get; set; } = new();
    public bool AnyOverBudget { get; set; }

    public MonthComparisonVm Comparison { get; set; } = new();
}

public class ExpenseByCategoryVm
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public decimal Spent { get; set; }
}

public class DashboardBudgetRowVm
{
    public string CategoryName { get; set; } = "";
    public decimal Budget { get; set; }
    public decimal Spent { get; set; }
    public decimal Remaining => Budget - Spent;
    public bool OverBudget => Budget > 0 && Spent > Budget;
}

public class MonthComparisonVm
{
    public int CurrentMonth { get; set; }
    public int CurrentYear { get; set; }
    public int PreviousMonth { get; set; }
    public int PreviousYear { get; set; }

    public decimal CurrentIncome { get; set; }
    public decimal CurrentExpense { get; set; }
    public decimal CurrentBalance { get; set; }

    public decimal PreviousIncome { get; set; }
    public decimal PreviousExpense { get; set; }
    public decimal PreviousBalance { get; set; }

    public decimal IncomeDiff => CurrentIncome - PreviousIncome;
    public decimal ExpenseDiff => CurrentExpense - PreviousExpense;
    public decimal BalanceDiff => CurrentBalance - PreviousBalance;
}