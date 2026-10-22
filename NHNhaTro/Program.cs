using System.Globalization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using NHNhaTro.Components;
using NHNhaTro.Components.Account;
using NHNhaTro.Data;
using NHNhaTro.Data.Entities;
using NHNhaTro.Services;

var builder = WebApplication.CreateBuilder(args);

// Định dạng ngày/số theo tiếng Việt
var vi = CultureInfo.GetCultureInfo("vi-VN");
CultureInfo.DefaultThreadCurrentCulture = vi;
CultureInfo.DefaultThreadCurrentUICulture = vi;

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();
builder.Services.AddAuthorization();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
// Factory cho các trang Blazor (mỗi thao tác một DbContext), đồng thời đăng ký DbContext scoped cho Identity.
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString), ServiceLifetime.Scoped);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        // Không có máy gửi email thật nên không bắt buộc xác nhận email.
        options.SignIn.RequireConfirmedAccount = false;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

// Nghiệp vụ nhà trọ
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
// Từ QuestPDF 2026.9 font hệ thống không được dùng mặc định: bật lại để dùng Times New Roman của Windows.
QuestPDF.Settings.UseSystemFonts = true;
builder.Services.AddHttpClient("ai", c => c.Timeout = TimeSpan.FromSeconds(90));
builder.Services.AddSingleton<FileStorage>();
builder.Services.AddScoped<SetupService>();
builder.Services.AddScoped<RoomService>();
builder.Services.AddScoped<LeaseService>();
builder.Services.AddScoped<DocumentScanService>();
builder.Services.AddSingleton<ChatHub>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<CompanyService>();
builder.Services.AddScoped<UtilityService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<ExpenseService>();
builder.Services.AddScoped<AttachmentService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<Documents>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<RegistrationPolicy>();

var app = builder.Build();

if (!app.Environment.IsDevelopment() && connectionString.Contains("(localdb)", StringComparison.OrdinalIgnoreCase))
{
    // Trên IIS app chạy ở Production: User Secrets không được nạp, LocalDB không chạy được dưới app pool.
    app.Logger.LogCritical("Chuỗi kết nối đang trỏ tới LocalDB. Trên máy chủ hãy tạo appsettings.Production.json " +
        "hoặc đặt biến môi trường ConnectionStrings__DefaultConnection (xem docs/TRIEN-KHAI-IIS.md).");
}

// Tạo/cập nhật bảng, dữ liệu ban đầu (64 phòng, đồ đạc, tài khoản) và vai trò người dùng.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await SetupService.SeedAsync(db);
    await UserService.EnsureRolesAsync(scope.ServiceProvider);
}

// Lệnh quản trị chạy một lần rồi thoát (không mở web):
//   NHNhaTro.exe --seed-demo   nạp dữ liệu nghiệm thu
//   NHNhaTro.exe --clear-data  xóa toàn bộ dữ liệu nghiệp vụ, giữ cấu hình và người dùng
//   NHNhaTro.exe --import-tenants <file.xlsx>  xóa dữ liệu thử, đặt cấu hình dãy/đồ đạc thật, nhập khách đang ở từ Excel
//   NHNhaTro.exe --seed-billing-test  thêm 2 khách thử (vào ngày 01 và ngày 20 tháng trước) có phiếu kỳ chốt đầu và kỳ sau để đối chiếu tiền
if (args.Contains("--seed-demo") || args.Contains("--clear-data") || args.Contains("--seed-billing-test") || args.Contains("--import-tenants"))
{
    Console.OutputEncoding = System.Text.Encoding.UTF8;
    using var scope = app.Services.CreateScope();
    try
    {
        var importIdx = Array.IndexOf(args, "--import-tenants");
        var message = importIdx >= 0 ? await TenantImporter.RunAsync(scope.ServiceProvider, importIdx + 1 < args.Length ? args[importIdx + 1] : "")
            : args.Contains("--clear-data") ? await DemoDataSeeder.ClearAsync(scope.ServiceProvider)
            : args.Contains("--seed-billing-test") ? await DemoDataSeeder.SeedBillingTestAsync(scope.ServiceProvider)
            : await DemoDataSeeder.SeedAsync(scope.ServiceProvider);
        Console.WriteLine(message);
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("LỖI: " + ex);
        return 1;
    }
}

// Font cho PDF: nạp mọi .ttf trong thư mục Fonts (nếu có).
var fontDir = Path.Combine(app.Environment.ContentRootPath, "Fonts");
if (Directory.Exists(fontDir))
    foreach (var font in Directory.EnumerateFiles(fontDir, "*.ttf"))
    {
        using var fs = File.OpenRead(font);
        QuestPDF.Drawing.FontManager.RegisterFontFromStream(fs);
    }

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapAdditionalIdentityEndpoints();

// ===== Tải chứng từ: chỉ người đã đăng nhập =====
var files = app.MapGroup("/files").RequireAuthorization();

files.MapGet("/bills/{id:int}", async (int id, Documents docs) =>
    await docs.BillPdfAsync(id) is { } r ? Results.File(r.Bytes, "application/pdf", r.FileName) : Results.NotFound());

files.MapGet("/checkout/{leaseId:int}", async (int leaseId, Documents docs) =>
    await docs.CheckoutPdfAsync(leaseId) is { } r ? Results.File(r.Bytes, "application/pdf", r.FileName) : Results.NotFound());

files.MapGet("/contracts/{leaseId:int}", async (int leaseId, Documents docs) =>
{
    try
    {
        return await docs.ContractDocxAsync(leaseId) is { } r ? Results.File(r.Bytes, Documents.DocxMime, r.FileName) : Results.NotFound();
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 500);
    }
});

files.MapGet("/residents.xlsx", async (bool? all, string? q, LeaseService leases, Documents docs) =>
{
    var rows = await leases.ListResidentsAsync(all != true, q);
    return Results.File(await docs.ResidentRegisterAsync(rows), Documents.XlsxMime, $"SoLuuTru_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
});

// Mẫu Excel ghi số điện các phòng của kỳ (điền cột "Số điện (kWh)" rồi nhập lại ở trang Ghi số điện)
files.MapGet("/so-dien.xlsx", async (int month, int year, BillingService billing) =>
{
    var sheet = await billing.GetSheetAsync(month, year);
    return Results.File(Documents.MeterTemplate(sheet), Documents.XlsxMime, $"SoDien_{year}{month:00}.xlsx");
});

files.MapGet("/ledger.xlsx", async ([AsParameters] LedgerQuery q, Documents docs) =>
    Results.File(await docs.LedgerAsync(q.ToFilter()), Documents.XlsxMime, $"SoThuChi_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"));

// Mã QR dẫn tới trang giới thiệu (in lên phướn quảng cáo)
app.MapGet("/qr.png", (HttpContext ctx, int? size, string? to) =>
{
    // to = "app" cho đường link cài ứng dụng; mặc định trang giới thiệu
    var path = to == "app" ? "app" : "gioi-thieu";
    var url = $"{ctx.Request.Scheme}://{ctx.Request.Host}/{path}";
    return Results.File(Documents.QrPng(url, Math.Clamp(size ?? 12, 4, 40)), "image/png", $"QR-{path}.png");
});

// Ảnh và chứng từ (đường dẫn /Uploads/...). Thư mục Public (ảnh quảng cáo) xem không cần đăng nhập.
var contentTypes = new FileExtensionContentTypeProvider();
app.MapGet("/Uploads/{**path}", (string path, FileStorage storage, HttpContext ctx) =>
{
    if (!FileStorage.IsPublicPath(path) && ctx.User.Identity?.IsAuthenticated != true) return Results.Challenge();
    var physical = storage.ResolvePhysicalPath($"{FileStorage.WebPrefix}/{path}");
    if (physical == null || !File.Exists(physical)) return Results.NotFound();
    if (!contentTypes.TryGetContentType(physical, out var type)) type = "application/octet-stream";
    return Results.File(physical, type, enableRangeProcessing: true);
});

app.Run();
return 0;

public record LedgerQuery(int? AccountId, DateTime? From, DateTime? To, string? Category, string? Direction, string? Search)
{
    public LedgerFilter ToFilter() => new() { AccountId = AccountId, From = From, To = To, Category = Category, Direction = Direction, Search = Search };
}
