using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json;
using NhaTroAnCu.Models;

namespace NhaTroAnCu.Controllers
{
    public class FPTReaderController : Controller
    {
        private NhaTroAnCuEntities db = new NhaTroAnCuEntities();
        private const string ApiEndpoint = "https://api.fpt.ai/vision/idr/vnm";

        [HttpPost]
        public async Task<ActionResult> ScanCCCD()
        {
            try
            {
                // Lấy API key từ database
                var apiConfig = db.FPTReaderAPIs.FirstOrDefault(x => x.Status);
                if (apiConfig == null)
                {
                    return Json(new { success = false, message = "Chưa cấu hình API key FPT AI" });
                }

                if (Request.Files.Count == 0)
                {
                    return Json(new { success = false, message = "Không có file được upload" });
                }

                var file = Request.Files["image"];
                if (file == null || file.ContentLength == 0)
                {
                    return Json(new { success = false, message = "File không hợp lệ" });
                }

                // Chỉ chấp nhận file ảnh
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
                var fileExtension = Path.GetExtension(file.FileName).ToLower();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    return Json(new { success = false, message = "Chỉ chấp nhận file ảnh (jpg, jpeg, png, gif, bmp)" });
                }

                // Gọi API FPT
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("api-key", apiConfig.ApiKey);

                    using (var content = new MultipartFormDataContent())
                    {
                        // Convert file thành stream
                        var fileContent = new StreamContent(file.InputStream);
                        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(file.ContentType);
                        content.Add(fileContent, "image", file.FileName);

                        // Gọi API
                        var response = await client.PostAsync(ApiEndpoint, content);
                        var responseContent = await response.Content.ReadAsStringAsync();

                        if (!response.IsSuccessStatusCode)
                        {
                            return Json(new
                            {
                                success = false,
                                message = $"Lỗi từ FPT API: {response.StatusCode}"
                            });
                        }

                        // Parse kết quả
                        dynamic result = JsonConvert.DeserializeObject(responseContent);

                        if (result.errorCode != 0)
                        {
                            return Json(new
                            {
                                success = false,
                                message = result.errorMessage?.ToString() ?? "Không thể nhận diện CCCD"
                            });
                        }

                        // Lấy thông tin từ kết quả
                        if (result.data != null && result.data.Count > 0)
                        {
                            var cardData = result.data[0];

                            // Chuẩn bị dữ liệu trả về
                            var extractedData = new
                            {
                                id = cardData.id?.ToString(),
                                name = cardData.name?.ToString(),
                                dob = cardData.dob?.ToString(),
                                sex = cardData.sex?.ToString(),
                                home = cardData.home?.ToString() ?? cardData.address?.ToString(),
                                ethnicity = cardData.ethnicity?.ToString(),
                                religion = cardData.religion?.ToString(),
                                nationality = cardData.nationality?.ToString(),
                                expiry = cardData.expiry?.ToString(),
                                issue_date = cardData.issue_date?.ToString(),
                                issue_loc = cardData.issue_loc?.ToString()
                            };

                            return Json(new
                            {
                                success = true,
                                data = extractedData,
                                message = "Quét CCCD thành công"
                            });
                        }
                        else
                        {
                            return Json(new
                            {
                                success = false,
                                message = "Không tìm thấy thông tin CCCD trong ảnh"
                            });
                        }
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                return Json(new
                {
                    success = false,
                    message = $"Lỗi kết nối: {ex.Message}"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = $"Lỗi xử lý: {ex.Message}"
                });
            }
        }

        // Action để kiểm tra API key
        public ActionResult CheckAPIKey()
        {
            var apiConfig = db.FPTReaderAPIs.FirstOrDefault(x => x.Status);
            if (apiConfig != null)
            {
                return Json(new
                {
                    configured = true,
                    email = apiConfig.Email
                }, JsonRequestBehavior.AllowGet);
            }

            return Json(new
            {
                configured = false,
                message = "Chưa cấu hình API key FPT AI"
            }, JsonRequestBehavior.AllowGet);
        }
    }
}