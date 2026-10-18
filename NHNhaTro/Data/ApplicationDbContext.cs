using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NHNhaTro.Data.Entities;

namespace NHNhaTro.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        public DbSet<AppSetting> Settings => Set<AppSetting>();
        public DbSet<Block> Blocks => Set<Block>();
        public DbSet<FurnitureItem> FurnitureItems => Set<FurnitureItem>();
        public DbSet<BlockFurniture> BlockFurniture => Set<BlockFurniture>();
        public DbSet<Room> Rooms => Set<Room>();
        public DbSet<RoomFurniture> RoomFurniture => Set<RoomFurniture>();
        public DbSet<Lease> Leases => Set<Lease>();
        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<LeaseTenant> LeaseTenants => Set<LeaseTenant>();
        public DbSet<LeasePenalty> LeasePenalties => Set<LeasePenalty>();
        public DbSet<Bill> Bills => Set<Bill>();
        public DbSet<MoneyAccount> Accounts => Set<MoneyAccount>();
        public DbSet<MoneyTransaction> Transactions => Set<MoneyTransaction>();
        public DbSet<Repair> Repairs => Set<Repair>();
        public DbSet<Purchase> Purchases => Set<Purchase>();
        public DbSet<Attachment> Attachments => Set<Attachment>();
        public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
        public DbSet<ChatRead> ChatReads => Set<ChatRead>();

        protected override void ConfigureConventions(ModelConfigurationBuilder b)
        {
            b.Properties<decimal>().HavePrecision(18, 0);
            b.Properties<decimal?>().HavePrecision(18, 0);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<AppSetting>(e =>
            {
                e.Property(x => x.Id).ValueGeneratedNever();
                e.Property(x => x.Name).HasMaxLength(200);
                e.Property(x => x.Address).HasMaxLength(500);
                e.Property(x => x.Owner).HasMaxLength(100);
                e.Property(x => x.Phone).HasMaxLength(30);
                e.Property(x => x.ZaloPhone).HasMaxLength(30);
                e.Property(x => x.RegisterArea).HasMaxLength(200);
                e.Property(x => x.AdTitle).HasMaxLength(200);
                e.Property(x => x.MapUrl).HasMaxLength(1000);
                e.Property(x => x.ScanProvider).HasMaxLength(20);
                e.Property(x => x.GeminiApiKey).HasMaxLength(300);
                e.Property(x => x.ClaudeApiKey).HasMaxLength(300);
                e.Property(x => x.GeminiModel).HasMaxLength(100);
                e.Property(x => x.ClaudeModel).HasMaxLength(100);
            });

            builder.Entity<Block>(e =>
            {
                e.Property(x => x.Code).HasMaxLength(5).IsRequired();
                e.Property(x => x.Name).HasMaxLength(100);
                e.HasIndex(x => x.Code).IsUnique();
            });

            builder.Entity<FurnitureItem>(e => e.Property(x => x.Name).HasMaxLength(100).IsRequired());

            builder.Entity<BlockFurniture>(e =>
            {
                e.HasIndex(x => new { x.BlockId, x.FurnitureItemId }).IsUnique();
                e.HasOne(x => x.Block).WithMany(x => x.DefaultFurniture).HasForeignKey(x => x.BlockId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Room>(e =>
            {
                e.Property(x => x.Code).HasMaxLength(10).IsRequired();
                e.HasIndex(x => x.Code).IsUnique();
                e.Property(x => x.Status).HasMaxLength(20);
                e.Property(x => x.Note).HasMaxLength(500);
                e.HasOne(x => x.Block).WithMany(x => x.Rooms).HasForeignKey(x => x.BlockId);
            });

            builder.Entity<RoomFurniture>(e =>
            {
                e.HasIndex(x => new { x.RoomId, x.FurnitureItemId }).IsUnique();
                e.HasOne(x => x.Room).WithMany(x => x.Furniture).HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Lease>(e =>
            {
                e.Property(x => x.StartDate).HasColumnType("date");
                e.Property(x => x.EndDate).HasColumnType("date");
                e.Property(x => x.Status).HasMaxLength(20);
                e.Property(x => x.ContractScanPath).HasMaxLength(300);
                e.Property(x => x.Note).HasMaxLength(1000);
                e.Property(x => x.CheckoutNote).HasMaxLength(1000);
                e.Property(x => x.FurnitureWarnings).HasMaxLength(1000);
                e.Property(x => x.CreatedBy).HasMaxLength(256);
                e.HasOne(x => x.Room).WithMany(x => x.Leases).HasForeignKey(x => x.RoomId);
                e.HasIndex(x => new { x.RoomId, x.Status });
            });

            builder.Entity<Tenant>(e =>
            {
                e.Property(x => x.FullName).HasMaxLength(100).IsRequired();
                e.Property(x => x.IdentityCard).HasMaxLength(30).IsRequired();
                e.HasIndex(x => x.IdentityCard).IsUnique();
                e.Property(x => x.BirthDate).HasColumnType("date");
                e.Property(x => x.Gender).HasMaxLength(10);
                e.Property(x => x.Phone).HasMaxLength(20);
                e.Property(x => x.PermanentAddress).HasMaxLength(300);
                e.Property(x => x.Ethnicity).HasMaxLength(50);
                e.Property(x => x.Nationality).HasMaxLength(50);
                e.Property(x => x.VehiclePlate).HasMaxLength(20);
            });

            builder.Entity<LeaseTenant>(e =>
            {
                e.Property(x => x.MoveInDate).HasColumnType("date");
                e.Property(x => x.MoveOutDate).HasColumnType("date");
                e.HasOne(x => x.Lease).WithMany(x => x.Tenants).HasForeignKey(x => x.LeaseId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Tenant).WithMany(x => x.Leases).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<LeasePenalty>(e =>
            {
                e.Property(x => x.Description).HasMaxLength(300);
                e.HasOne(x => x.Lease).WithMany(x => x.Penalties).HasForeignKey(x => x.LeaseId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Bill>(e =>
            {
                e.Property(x => x.Kind).HasMaxLength(20);
                e.Property(x => x.PeriodFrom).HasColumnType("date");
                e.Property(x => x.PeriodTo).HasColumnType("date");
                e.Property(x => x.Note).HasMaxLength(500);
                e.Property(x => x.CreatedBy).HasMaxLength(256);
                e.HasOne(x => x.Lease).WithMany(x => x.Bills).HasForeignKey(x => x.LeaseId);
                e.HasIndex(x => new { x.LeaseId, x.Year, x.Month, x.Kind });
            });

            builder.Entity<MoneyAccount>(e =>
            {
                e.Property(x => x.Name).HasMaxLength(100).IsRequired();
                e.Property(x => x.Kind).HasMaxLength(20);
                e.Property(x => x.OpeningDate).HasColumnType("date");
                e.Property(x => x.BankName).HasMaxLength(100);
                e.Property(x => x.AccountNumber).HasMaxLength(50);
                e.Property(x => x.AccountHolder).HasMaxLength(100);
            });

            builder.Entity<MoneyTransaction>(e =>
            {
                e.Property(x => x.Date).HasColumnType("date");
                e.Property(x => x.Category).HasMaxLength(30);
                e.Property(x => x.Description).HasMaxLength(500);
                e.Property(x => x.CreatedBy).HasMaxLength(256);
                e.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId);
                e.HasOne(x => x.Room).WithMany().HasForeignKey(x => x.RoomId);
                e.HasOne(x => x.Lease).WithMany().HasForeignKey(x => x.LeaseId);
                e.HasOne(x => x.Bill).WithMany(x => x.Payments).HasForeignKey(x => x.BillId);
                e.HasOne(x => x.Repair).WithMany().HasForeignKey(x => x.RepairId);
                e.HasOne(x => x.Purchase).WithMany().HasForeignKey(x => x.PurchaseId);
                e.HasIndex(x => new { x.AccountId, x.Date });
            });

            builder.Entity<Repair>(e =>
            {
                e.Property(x => x.Date).HasColumnType("date");
                e.Property(x => x.Location).HasMaxLength(200);
                e.Property(x => x.Description).HasMaxLength(1000).IsRequired();
                e.Property(x => x.PerformedBy).HasMaxLength(100);
                e.Property(x => x.CreatedBy).HasMaxLength(256);
                e.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId);
            });

            builder.Entity<Purchase>(e =>
            {
                e.Property(x => x.Date).HasColumnType("date");
                e.Property(x => x.Description).HasMaxLength(1000).IsRequired();
                e.Property(x => x.Supplier).HasMaxLength(200);
                e.Property(x => x.CreatedBy).HasMaxLength(256);
                e.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId);
            });

            builder.Entity<ChatMessage>(e =>
            {
                e.Property(x => x.Sender).HasMaxLength(256).IsRequired();
                e.Property(x => x.Text).HasMaxLength(4000).IsRequired();
                e.HasIndex(x => x.SentAt);
            });
            builder.Entity<ChatRead>(e =>
            {
                e.HasKey(x => x.UserName);
                e.Property(x => x.UserName).HasMaxLength(256);
            });

            builder.Entity<Attachment>(e =>
            {
                e.Property(x => x.EntityType).HasMaxLength(30).IsRequired();
                e.Property(x => x.Kind).HasMaxLength(30).IsRequired();
                e.Property(x => x.Path).HasMaxLength(300).IsRequired();
                e.Property(x => x.FileName).HasMaxLength(200);
                e.Property(x => x.Caption).HasMaxLength(300);
                e.Property(x => x.CreatedBy).HasMaxLength(256);
                e.HasIndex(x => new { x.EntityType, x.EntityId });
            });

            // Không xóa dây chuyền (trừ các bảng con thuần túy): mọi xóa do nghiệp vụ xử lý có kiểm soát.
            Type[] cascadeChildren = [typeof(BlockFurniture), typeof(RoomFurniture), typeof(LeaseTenant), typeof(LeasePenalty)];
            foreach (var fk in builder.Model.GetEntityTypes()
                         .Where(t => t.ClrType.Namespace == typeof(Room).Namespace && !cascadeChildren.Contains(t.ClrType))
                         .SelectMany(t => t.GetForeignKeys()))
                fk.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }
}
