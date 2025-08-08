using System.Linq;
using NhaTroAnCu.Models;

namespace NhaTroAnCu.Data.Seed
{
    public static class IncomeExpenseCategorySeeder
    {
        public static void Seed(NhaTroAnCuDbContext db)
        {
            // Danh mục hệ thống "Thu tiền cọc"
            if (!db.IncomeExpenseCategories.Any(c => c.Name == "Thu tiền cọc" && c.IsSystem))
            {
                db.IncomeExpenseCategories.Add(new IncomeExpenseCategory
                {
                    Name = "Thu tiền cọc",
                    IsSystem = true,
                    Type = "Income"
                });
                db.SaveChanges();
            }
        }
    }
}