using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using NHNhaTro.Data;
using NHNhaTro.Data.Entities;

namespace NHNhaTro.Services;

public static class ScanProvider
{
    public const string None = "";
    public const string Gemini = "Gemini";
    public const string Claude = "Claude";
    public const string DefaultGeminiModel = "gemini-3.8-flash";
    public const string DefaultClaudeModel = "claude-haiku-5-5";
}

/// <summary>Kết quả đọc chứng từ (biên lai chuyển khoản, hóa đơn, phiếu thu).</summary>
public record ReceiptScanResult(decimal? Amount, DateTime? Date, string? Description, string? Payer, string? Payee, string? Reference)
{
    public string Summary => string.Join(", ", new[]
    {
        Amount is decimal a ? Fmt.Vnd(a) : null,
        Date is DateTime d ? d.ToString("dd/MM/yyyy") : null,
        Payee, Description
    }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

/// <summary>
/// Đọc ảnh CCCD và ảnh chứng từ bằng mô hình AI: Google Gemini (có gói miễn phí) hoặc Claude (Anthropic).
/// Chọn nhà cung cấp và nhập API key ở trang Cấu hình; key lưu trong database, không nằm trong mã nguồn.
/// </summary>
public class DocumentScanService(IDbContextFactory<ApplicationDbContext> dbFactory, FileStorage files, IHttpClientFactory httpFactory, ILogger<DocumentScanService> logger)
{
    private const string CardPrompt = """
        Đây là ảnh căn cước công dân / CCCD / CMND / hộ chiếu (có thể gồm mặt trước và mặt sau).
        Đọc thông tin và trả về DUY NHẤT một đối tượng JSON, không giải thích:
        {"id": "số CCCD/CMND/hộ chiếu", "name": "họ và tên, viết hoa chữ cái đầu mỗi từ", "dob": "dd/MM/yyyy",
         "sex": "Nam hoặc Nữ", "nationality": "quốc tịch, ví dụ Việt Nam", "ethnicity": "dân tộc nếu có",
         "address": "nơi thường trú / nơi cư trú"}
        Trường nào không đọc được thì để null. Giữ nguyên dấu tiếng Việt.
        """;

    private const string ReceiptPrompt = """
        Đây là ảnh chứng từ tiền: biên lai/ảnh chụp màn hình chuyển khoản ngân hàng, hóa đơn mua hàng hoặc phiếu thu.
        Trả về DUY NHẤT một đối tượng JSON, không giải thích:
        {"amount": số tiền VNĐ dạng số nguyên (tổng tiền thanh toán, không có dấu chấm phẩy),
         "date": "dd/MM/yyyy" ngày giao dịch hoặc ngày hóa đơn,
         "description": "nội dung chuyển khoản hoặc tóm tắt hàng hóa/dịch vụ, ngắn gọn",
         "payer": "người/tài khoản chuyển tiền nếu có", "payee": "người nhận hoặc cửa hàng/người bán",
         "reference": "mã giao dịch/số hóa đơn nếu có"}
        Trường nào không đọc được thì để null.
        """;

    public async Task<bool> IsConfiguredAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var s = await db.Settings.AsNoTracking().FirstAsync();
        return Configured(s);
    }

    private static bool Configured(AppSetting s) => s.ScanProvider switch
    {
        ScanProvider.Gemini => !string.IsNullOrWhiteSpace(s.GeminiApiKey),
        ScanProvider.Claude => !string.IsNullOrWhiteSpace(s.ClaudeApiKey),
        _ => false
    };

    public async Task<CardScanResult> ScanIdentityCardAsync(IEnumerable<string> photoWebPaths)
    {
        var json = await AskAsync(CardPrompt, photoWebPaths.Take(4).ToList());
        string? S(string n) => Str(json, n);
        DateTime? dob = ParseDate(S("dob"));
        var rawSex = S("sex");
        var sex = rawSex?.ToUpperInvariant() switch { "NAM" or "MALE" or "M" => "Nam", "NỮ" or "NU" or "FEMALE" or "F" => "Nữ", _ => rawSex };
        return new CardScanResult(S("id")?.Replace(" ", ""), S("name"), dob, sex, S("address"), S("ethnicity"), S("nationality"));
    }

    public async Task<ReceiptScanResult> ScanReceiptAsync(string webPath)
    {
        var json = await AskAsync(ReceiptPrompt, [webPath]);
        decimal? amount = null;
        if (json.TryGetPropertyValue("amount", out var a) && a != null)
        {
            var text = a.ToString();
            var digits = new string(text.Where(char.IsDigit).ToArray());
            if (digits.Length is > 0 and < 16) amount = decimal.Parse(digits, CultureInfo.InvariantCulture);
        }
        return new ReceiptScanResult(amount > 0 ? amount : null, ParseDate(Str(json, "date")), Str(json, "description"), Str(json, "payer"), Str(json, "payee"), Str(json, "reference"));
    }

    /// <summary>Gọi thử nhà cung cấp đang chọn bằng một câu hỏi chữ, để kiểm tra API key.</summary>
    public async Task<string> TestAsync()
    {
        var json = await AskAsync("""Trả về đúng JSON {"ok": true}""", []);
        return json.ToJsonString();
    }

    private static string? Str(JsonObject o, string name) =>
        o.TryGetPropertyValue(name, out var v) && v is JsonValue jv && jv.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s)
            && !s.Equals("null", StringComparison.OrdinalIgnoreCase) ? s.Trim() : null;

    private static DateTime? ParseDate(string? s) =>
        DateTime.TryParseExact(s?.Trim(), ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "dd.MM.yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
        && d.Year is > 1900 and < 2100 ? d : null;

    /// <summary>Hỏi đáp bằng chữ (trợ lý trong chat). Trả về câu trả lời thuần văn bản.</summary>
    public async Task<string> AskTextAsync(string prompt)
    {
        AppSetting s;
        await using (var db = await dbFactory.CreateDbContextAsync()) s = await db.Settings.AsNoTracking().FirstAsync();
        if (!Configured(s)) throw new InvalidOperationException("Chưa cấu hình AI (Cấu hình > Quét CCCD và chứng từ) nên trợ lý chưa trả lời được.");
        var text = s.ScanProvider == ScanProvider.Claude ? await CallClaudeAsync(s, prompt, [], json: false) : await CallGeminiAsync(s, prompt, [], json: false);
        return text.Trim();
    }

    private async Task<JsonObject> AskAsync(string prompt, List<string> webPaths)
    {
        AppSetting s;
        await using (var db = await dbFactory.CreateDbContextAsync()) s = await db.Settings.AsNoTracking().FirstAsync();
        if (!Configured(s)) throw new InvalidOperationException("Chưa cấu hình quét bằng AI (Cấu hình > Quét CCCD và chứng từ).");

        var images = new List<(string Mime, string Base64)>();
        foreach (var p in webPaths)
        {
            var physical = files.ResolvePhysicalPath(p);
            if (physical == null || !File.Exists(physical)) continue;
            var mime = Path.GetExtension(physical).ToLowerInvariant() switch
            {
                ".png" => "image/png", ".gif" => "image/gif", ".webp" => "image/webp", ".pdf" => "application/pdf", _ => "image/jpeg"
            };
            images.Add((mime, Convert.ToBase64String(await File.ReadAllBytesAsync(physical))));
        }
        if (webPaths.Count > 0 && images.Count == 0) throw new InvalidOperationException("Không tìm thấy file để quét.");

        var text = s.ScanProvider == ScanProvider.Claude
            ? await CallClaudeAsync(s, prompt, images)
            : await CallGeminiAsync(s, prompt, images);
        return ParseJsonObject(text);
    }

    private async Task<string> CallGeminiAsync(AppSetting s, string prompt, List<(string Mime, string Base64)> images, bool json = true)
    {
        var parts = new JsonArray { new JsonObject { ["text"] = prompt } };
        foreach (var (mime, data) in images)
            parts.Add(new JsonObject { ["inline_data"] = new JsonObject { ["mime_type"] = mime, ["data"] = data } });
        var body = new JsonObject
        {
            ["contents"] = new JsonArray { new JsonObject { ["role"] = "user", ["parts"] = parts } },
            ["generationConfig"] = json ? new JsonObject { ["responseMimeType"] = "application/json", ["temperature"] = 0 } : new JsonObject { ["temperature"] = 0.3 }
        };
        var model = string.IsNullOrWhiteSpace(s.GeminiModel) ? ScanProvider.DefaultGeminiModel : s.GeminiModel.Trim();
        using var req = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        req.Headers.Add("x-goog-api-key", s.GeminiApiKey!.Trim());
        using var doc = await SendAsync(req, "Gemini");
        var root = doc.RootElement;
        if (root.TryGetProperty("candidates", out var c) && c.ValueKind == JsonValueKind.Array && c.GetArrayLength() > 0
            && c[0].TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var ps))
            return string.Concat(ps.EnumerateArray().Select(p => p.TryGetProperty("text", out var t) ? t.GetString() : ""));
        throw new InvalidOperationException("Gemini không trả về kết quả (ảnh có thể bị chặn hoặc không đọc được).");
    }

    private async Task<string> CallClaudeAsync(AppSetting s, string prompt, List<(string Mime, string Base64)> images, bool json = true)
    {
        var content = new JsonArray();
        foreach (var (mime, data) in images)
            content.Add(mime == "application/pdf"
                ? new JsonObject { ["type"] = "document", ["source"] = new JsonObject { ["type"] = "base64", ["media_type"] = mime, ["data"] = data } }
                : new JsonObject { ["type"] = "image", ["source"] = new JsonObject { ["type"] = "base64", ["media_type"] = mime, ["data"] = data } });
        content.Add(new JsonObject { ["type"] = "text", ["text"] = prompt });
        var body = new JsonObject
        {
            ["model"] = string.IsNullOrWhiteSpace(s.ClaudeModel) ? ScanProvider.DefaultClaudeModel : s.ClaudeModel.Trim(),
            ["max_tokens"] = 1024,
            ["messages"] = new JsonArray { new JsonObject { ["role"] = "user", ["content"] = content } }
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        req.Headers.Add("x-api-key", s.ClaudeApiKey!.Trim());
        req.Headers.Add("anthropic-version", "2023-06-01");
        using var doc = await SendAsync(req, "Claude");
        if (doc.RootElement.TryGetProperty("content", out var blocks) && blocks.ValueKind == JsonValueKind.Array)
            return string.Concat(blocks.EnumerateArray().Where(b => b.TryGetProperty("type", out var t) && t.GetString() == "text")
                .Select(b => b.GetProperty("text").GetString()));
        throw new InvalidOperationException("Claude không trả về kết quả.");
    }

    private async Task<JsonDocument> SendAsync(HttpRequestMessage req, string name)
    {
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var client = httpFactory.CreateClient("ai");
        using var res = await client.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
        {
            logger.LogWarning("{Name} trả về {Status}: {Body}", name, (int)res.StatusCode, body.Length > 1000 ? body[..1000] : body);
            string? detail = null;
            try { detail = JsonNode.Parse(body)?["error"]?["message"]?.GetValue<string>(); } catch (Exception) { }
            bool badKey = body.Contains("API_KEY_INVALID") || body.Contains("API key not valid") || body.Contains("invalid x-api-key");
            var msg = badKey ? "API key sai" : (int)res.StatusCode switch
            {
                400 => "yêu cầu không hợp lệ" + (detail != null ? $" ({detail})" : ""),
                401 or 403 => "API key sai hoặc không có quyền",
                404 => "không tìm thấy mô hình",
                429 => "vượt hạn mức miễn phí, thử lại sau ít phút",
                _ => $"mã lỗi {(int)res.StatusCode}"
            };
            throw new InvalidOperationException($"Lỗi {name}: {msg}.");
        }
        return JsonDocument.Parse(body);
    }

    /// <summary>Lấy đối tượng JSON trong câu trả lời (bỏ khối ```json nếu có).</summary>
    private static JsonObject ParseJsonObject(string text)
    {
        int start = text.IndexOf('{'), end = text.LastIndexOf('}');
        if (start < 0 || end <= start) throw new InvalidOperationException("Không đọc được nội dung trong ảnh.");
        try
        {
            return JsonNode.Parse(text[start..(end + 1)]) as JsonObject ?? throw new InvalidOperationException("Không đọc được nội dung trong ảnh.");
        }
        catch (JsonException) { throw new InvalidOperationException("Kết quả quét không hợp lệ, hãy thử lại."); }
    }
}
