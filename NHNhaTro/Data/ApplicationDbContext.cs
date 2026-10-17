using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NHNhaTro.Data.Entities;

namespace NHNhaTro.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        public DbSet<Room> Rooms => Set<Room>();
        public DbSet<Company> Companies => Set<Company>();
        public DbSet<Contract> Contracts => Set<Contract>();
        public DbSet<ContractRoom> ContractRooms => Set<ContractRoom>();
        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<ContractTenant> ContractTenants => Set<ContractTenant>();
        public DbSet<ContractExtensionHistory> ContractExtensionHistories => Set<ContractExtensionHistory>();
        public DbSet<UtilityBill> UtilityBills => Set<UtilityBill>();
        public DbSet<IncomeExpenseCategory> IncomeExpenseCategories => Set<IncomeExpenseCategory>();
        public DbSet<IncomeExpense> IncomeExpenses => Set<IncomeExpense>();
        public DbSet<FptReaderApi> FptReaderApis => Set<FptReaderApi>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Database cũ không dùng cascade delete: mọi xóa dây chuyền do code xử lý.
            builder.Entity<Room>(e =>
            {
                e.ToTable("Rooms");
                e.Property(x => x.Name).HasMaxLength(10).IsRequired();
                e.Property(x => x.Area).HasColumnType("char(1)").HasMaxLength(1).IsFixedLength().IsUnicode(false).IsRequired();
                e.Property(x => x.DefaultPrice).HasPrecision(18, 2);
            });

            builder.Entity<Company>(e =>
            {
                e.ToTable("Companies");
                e.Property(x => x.CompanyName).HasMaxLength(200).IsRequired();
                e.Property(x => x.TaxCode).HasMaxLength(50);
                e.Property(x => x.Address).HasMaxLength(500);
                e.Property(x => x.Phone).HasMaxLength(20);
                e.Property(x => x.Email).HasMaxLength(100);
                e.Property(x => x.Representative).HasMaxLength(100);
                e.Property(x => x.RepresentativePhone).HasMaxLength(20);
                e.Property(x => x.RepresentativeEmail).HasMaxLength(100);
                e.Property(x => x.BankAccount).HasMaxLength(50);
                e.Property(x => x.BankName).HasMaxLength(200);
                e.Property(x => x.CreatedAt).HasColumnType("datetime");
                e.Property(x => x.UpdatedAt).HasColumnType("datetime");
            });

            builder.Entity<Contract>(e =>
            {
                e.ToTable("Contracts");
                e.Property(x => x.PriceAgreed).HasPrecision(18, 2);
                e.Property(x => x.ElectricityPrice).HasPrecision(18, 2);
                e.Property(x => x.WaterPrice).HasPrecision(18, 2);
                e.Property(x => x.StartDate).HasColumnType("date");
                e.Property(x => x.EndDate).HasColumnType("date");
                e.Property(x => x.MoveInDate).HasColumnType("date");
                e.Property(x => x.Status).HasMaxLength(20).IsRequired();
                e.Property(x => x.Note).HasMaxLength(500);
                e.Property(x => x.ContractScanFilePath).HasMaxLength(250);
                e.Property(x => x.ContractType).HasMaxLength(20);
                e.HasOne(x => x.Company).WithMany(x => x.Contracts).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<ContractRoom>(e =>
            {
                e.ToTable("ContractRooms");
                e.Property(x => x.PriceAgreed).HasPrecision(18, 2);
                e.HasOne(x => x.Contract).WithMany(x => x.ContractRooms).HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.Room).WithMany(x => x.ContractRooms).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<Tenant>(e =>
            {
                e.ToTable("Tenants");
                e.Property(x => x.FullName).HasMaxLength(100).IsRequired();
                e.Property(x => x.IdentityCard).HasMaxLength(20).IsRequired();
                e.Property(x => x.PhoneNumber).HasMaxLength(20);
                e.Property(x => x.BirthDate).HasColumnType("date");
                e.Property(x => x.Gender).HasMaxLength(10);
                e.Property(x => x.PermanentAddress).HasMaxLength(255);
                e.Property(x => x.Ethnicity).HasMaxLength(50);
                e.Property(x => x.VehiclePlate).HasMaxLength(20);
                e.HasOne(x => x.Company).WithMany(x => x.Tenants).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<ContractTenant>(e =>
            {
                e.ToTable("ContractTenants");
                e.Property(x => x.CreatedAt).HasColumnType("datetime");
                e.HasOne(x => x.Contract).WithMany(x => x.ContractTenants).HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.Room).WithMany(x => x.ContractTenants).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.Tenant).WithMany(x => x.ContractTenants).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<ContractExtensionHistory>(e =>
            {
                e.ToTable("ContractExtensionHistory");
                e.Property(x => x.ExtendedAt).HasColumnType("datetime");
                e.Property(x => x.OldEndDate).HasColumnType("datetime");
                e.Property(x => x.NewEndDate).HasColumnType("datetime");
                e.Property(x => x.Note).HasMaxLength(255);
                e.HasOne(x => x.Contract).WithMany(x => x.ContractExtensionHistories).HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<UtilityBill>(e =>
            {
                e.ToTable("UtilityBills");
                e.Property(x => x.ElectricityAmount).HasPrecision(18, 2);
                e.Property(x => x.WaterAmount).HasPrecision(18, 2);
                e.Property(x => x.RentAmount).HasPrecision(18, 2);
                e.Property(x => x.ExtraCharge).HasPrecision(18, 2);
                e.Property(x => x.Discount).HasPrecision(18, 2);
                e.Property(x => x.TotalAmount).HasPrecision(18, 2);
                e.Property(x => x.BillNote).HasMaxLength(250);
                e.Property(x => x.BillStatus).HasMaxLength(50);
                e.Property(x => x.CreatedAt).HasColumnType("datetime");
                e.Property(x => x.UpdatedAt).HasColumnType("datetime");
                e.HasOne(x => x.Contract).WithMany(x => x.UtilityBills).HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.Room).WithMany(x => x.UtilityBills).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<IncomeExpenseCategory>(e =>
            {
                e.ToTable("IncomeExpenseCategories");
                e.Property(x => x.Name).HasMaxLength(100).IsRequired();
                e.Property(x => x.Type).HasMaxLength(10).IsRequired();
                e.Property(x => x.CreatedAt).HasColumnType("datetime");
                e.Property(x => x.UpdatedAt).HasColumnType("datetime");
            });

            builder.Entity<IncomeExpense>(e =>
            {
                e.ToTable("IncomeExpenses");
                e.Property(x => x.Amount).HasPrecision(18, 2);
                e.Property(x => x.TransactionDate).HasColumnType("date");
                e.Property(x => x.Description).HasMaxLength(500);
                e.Property(x => x.ReferenceNumber).HasMaxLength(50);
                e.Property(x => x.CreatedBy).HasMaxLength(128);
                e.Property(x => x.CreatedAt).HasColumnType("datetime");
                e.Property(x => x.UpdatedAt).HasColumnType("datetime");
                e.HasOne(x => x.Category).WithMany(x => x.IncomeExpenses).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.NoAction);
                e.HasOne(x => x.Contract).WithMany(x => x.IncomeExpenses).HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<FptReaderApi>(e =>
            {
                e.ToTable("FPTReaderAPI");
                e.HasKey(x => new { x.ApiKey, x.Email, x.Status });
                e.Property(x => x.ApiKey).HasMaxLength(150);
                e.Property(x => x.Email).HasMaxLength(150);
            });
        }
    }
}
