using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using NHNhaTro.Components;
using NHNhaTro.Components.Account;
using NHNhaTro.Data;
using NHNhaTro.Services;

var builder = WebApplication.CreateBuilder(args);

// Định dạng ngày/số theo tiếng Việt
var vi = CultureInfo.GetCultureInfo("vi-VN");
CultureInfo.DefaultThreadCurrentCulture = vi;
CultureInfo.DefaultThreadCurrentUICulture = vi;

// Add services to the container.
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
        // Không có máy gửi email thật và tài khoản của bản MVC cũ đều chưa xác nhận email,
        // nên không bắt buộc xác nhận email (nếu bật, mọi tài khoản cũ sẽ bị chặn đăng nhập).
        options.SignIn.RequireConfirmedAccount = false;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

// Nghiệp vụ nhà trọ
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
// Từ QuestPDF 2026.9 font hệ thống không được dùng mặc định. Bật lại để dùng Times New Roman có sẵn trên Windows
// (giống bản cũ); máy chủ không có font này thì đặt file .ttf vào thư mục Fonts, hoặc PDF sẽ dùng Lato đi kèm QuestPDF.
QuestPDF.Settings.UseSystemFonts = true;
builder.Services.Configure<LandlordOptions>(builder.Configuration.GetSection("NhaTro"));
builder.Services.AddHttpClient("fptai", c => c.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddSingleton<FileStorage>();
builder.Services.AddScoped<RoomService>();
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<ContractService>();
builder.Services.AddScoped<TenantService>();
builder.Services.AddScoped<IncomeExpenseService>();
builder.Services.AddScoped<PdfDocuments>();
builder.Services.AddScoped<OfficeDocuments>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<RegistrationPolicy>();

var app = builder.Build();

// Font cho PDF: nạp mọi .ttf trong thư mục Fonts (nếu có) để máy chủ không có Times New Roman vẫn in được tiếng Việt.
var fontDir = Path.Combine(app.Environment.ContentRootPath, "Fonts");
if (Directory.Exists(fontDir))
{
    foreach (var font in Directory.EnumerateFiles(fontDir, "*.ttf"))
    {
        using var fs = File.OpenRead(font);
        QuestPDF.Drawing.FontManager.RegisterFont(fs);
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

// ===== Tải file: chỉ người đã đăng nhập =====
var files = app.MapGroup("/files").RequireAuthorization();

files.MapGet("/bills/{id:int}", async (int id, PdfDocuments pdf) =>
    await pdf.UtilityBillAsync(id) is { } r ? Results.File(r.Bytes, "application/pdf", r.FileName) : Results.NotFound());

files.MapGet("/receipts/{id:int}", async (int id, PdfDocuments pdf) =>
    await pdf.PaymentReceiptAsync(id) is { } r ? Results.File(r.Bytes, "application/pdf", r.FileName) : Results.NotFound());

files.MapGet("/contracts/{id:int}/print", async (int id, ContractService contracts, OfficeDocuments office) =>
{
    var c = await contracts.GetDetailsAsync(id);
    if (c == null) return Results.NotFound();
    try
    {
        return Results.File(office.PrintContract(c), OfficeDocuments.DocxMime, $"HopDong_{c.Id:D6}_{DateTime.Now:yyyyMMdd}.docx");
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 500);
    }
});

files.MapGet("/contracts.xlsx", async ([AsParameters] ContractExportQuery q, ContractService contracts, OfficeDocuments office) =>
{
    var list = await contracts.ListForExportAsync(q.ToFilter());
    return Results.File(office.ContractList(list), OfficeDocuments.XlsxMime, $"DanhSachHopDong_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
});

files.MapGet("/tenants.xlsx", async ([AsParameters] TenantExportQuery q, TenantService tenants, OfficeDocuments office) =>
{
    var list = await tenants.ListAsync(q.ToFilter());
    return Results.File(office.TenantRegister(list), OfficeDocuments.XlsxMime, $"DanhSachKhachThue_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
});

files.MapGet("/tenants.pdf", async ([AsParameters] TenantExportQuery q, TenantService tenants, PdfDocuments pdf) =>
{
    var list = await tenants.ListAsync(q.ToFilter());
    return Results.File(pdf.TenantList(list, q.Status), "application/pdf", $"DanhSachKhachThue_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
});

// Ảnh CCCD và scan hợp đồng (đường dẫn lưu trong database có dạng /Uploads/...)
var contentTypes = new FileExtensionContentTypeProvider();
app.MapGet("/Uploads/{**path}", (string path, FileStorage storage) =>
{
    var physical = storage.ResolvePhysicalPath($"{FileStorage.WebPrefix}/{path}");
    if (physical == null || !File.Exists(physical)) return Results.NotFound();
    if (!contentTypes.TryGetContentType(physical, out var type)) type = "application/octet-stream";
    return Results.File(physical, type, enableRangeProcessing: true);
}).RequireAuthorization();

app.Run();

public record ContractExportQuery(string? SearchTerm, string? ContractType, string? Status, DateTime? FromDate, DateTime? ToDate, int? CompanyId)
{
    public ContractFilter ToFilter() => new()
    {
        SearchTerm = SearchTerm, ContractType = ContractType, Status = Status, FromDate = FromDate, ToDate = ToDate, CompanyId = CompanyId
    };
}

public record TenantExportQuery(string? SearchName, string? SearchCard, string? SearchRoom, string? Status, int? CompanyId, string? ContractType)
{
    public TenantFilter ToFilter() => new()
    {
        SearchName = SearchName, SearchCard = SearchCard, SearchRoom = SearchRoom, Status = Status, CompanyId = CompanyId, ContractType = ContractType
    };
}
