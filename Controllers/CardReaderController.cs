using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using NhaTroAnCu.Models;

namespace NhaTroAnCu.Controllers
{
    public class CardReaderController : Controller
    {

        private NhaTroAnCuEntities db = new NhaTroAnCuEntities();
        private const string ApiEndpoint = "https://api.fpt.ai/vision/idr/vnm";

        [HttpPost]
        public async Task<ActionResult> ReadCCCD(HttpPostedFileBase inputFile)
        {
            string apiKey = db.FPTReaderAPIs.Single(x => x.Status).ApiKey;
            if (inputFile == null || inputFile.ContentLength == 0)
            {
                return Json(new { success = false, message = "Yêu cầu chọn file." });
            }
            try
            {
                if (inputFile.ContentType.StartsWith("image/"))
                {
                    // Đặt lại vị trí stream
                    inputFile.InputStream.Position = 0;
                    using (Image originalImage = Image.FromStream(inputFile.InputStream))
                    {
                        return await ProcessImages(originalImage, inputFile.FileName, apiKey);
                    }
                }
                else
                {
                    return Json(new { success = false, message = "Invalid file type." });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An unexpected error occurred: " + ex.Message });
            }
        }
        private async Task<ActionResult> ProcessImages(Image img, string originalFileName, string apiKey)
        {
            CardData combinedData = new CardData(); // KHÔNG khởi tạo giá trị mặc định
            bool hasFront = false;
            bool hasBack = false;
            bool hasError = false;
            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    img.Save(ms, ImageFormat.Jpeg);
                    ms.Position = 0;
                    string fileName = Path.GetFileNameWithoutExtension(originalFileName) + "_" + Guid.NewGuid().ToString() + ".jpg";
                    ApiResponse currentResponse = await ReadCCCDFromStreamAsync(ms, fileName, apiKey);

                    if (currentResponse.errorCode == 0 && currentResponse.data != null && currentResponse.data.Count > 0)
                    {
                        CardData currentData = currentResponse.data[0];

                        if (currentData.type == "new" || currentData.type == "old" || currentData.type.Contains("front"))
                        {
                            MergeFrontData(combinedData, currentData);
                            hasFront = true;
                        }
                        else if (currentData.type == "new_back" || currentData.type == "old_back" || currentData.type.Contains("back"))
                        {
                            MergeBackData(combinedData, currentData);
                            hasBack = true;
                        }
                    }
                    else
                    {
                        hasError = true;
                    }
                }
            }
            catch (Exception ex)
            {
                hasError = true;
            }
            finally
            {
                img.Dispose();
            }

            ApiResponse finalResponse = new ApiResponse
            {
                errorCode = hasError ? 1 : 0,  // Nếu có lỗi nào thì errorCode != 0
                errorMessage = hasError ? "Error processing one or more images." : "",
                data = new List<CardData> { combinedData } // Trả về MỘT CardData
            };
            if (!string.IsNullOrEmpty(finalResponse.errorMessage))
            {
                finalResponse.errorCode = 1;
            }
            if (!hasFront)
            {
                finalResponse.errorMessage += " Front side of ID card is missing.";
            }
            if (!hasBack)
            {
                finalResponse.errorMessage += " Back side of ID card is missing.";
            }
            return Json(new { success = !hasError, data = finalResponse });
        }

        // Hàm merge dữ liệu mặt TRƯỚC (CHỈ kiểm tra null hoặc rỗng)
        private void MergeFrontData(CardData combined, CardData front)
        {
            if (!string.IsNullOrEmpty(front.id)) combined.id = front.id;
            if (!string.IsNullOrEmpty(front.id_prob)) combined.id_prob = front.id_prob;
            if (!string.IsNullOrEmpty(front.name)) combined.name = front.name;
            if (!string.IsNullOrEmpty(front.name_prob)) combined.name_prob = front.name_prob;
            if (!string.IsNullOrEmpty(front.dob)) combined.dob = front.dob;
            if (!string.IsNullOrEmpty(front.dob_prob)) combined.dob_prob = front.dob_prob;
            if (!string.IsNullOrEmpty(front.sex)) combined.sex = front.sex;
            if (!string.IsNullOrEmpty(front.sex_prob)) combined.sex_prob = front.sex_prob;
            if (!string.IsNullOrEmpty(front.nationality)) combined.nationality = front.nationality;
            if (!string.IsNullOrEmpty(front.nationality_prob)) combined.nationality_prob = front.nationality_prob;
            if (!string.IsNullOrEmpty(front.home)) combined.home = front.home;
            if (!string.IsNullOrEmpty(front.home_prob)) combined.home_prob = front.home_prob;
            if (!string.IsNullOrEmpty(front.address)) combined.address = front.address;
            if (!string.IsNullOrEmpty(front.address_prob)) combined.address_prob = front.address_prob;

            if (front.address_entities != null)
            {
                if (combined.address_entities == null) combined.address_entities = new AddressEntities(); // Khởi tạo nếu chưa có
                if (!string.IsNullOrEmpty(front.address_entities.province)) combined.address_entities.province = front.address_entities.province;
                if (!string.IsNullOrEmpty(front.address_entities.district)) combined.address_entities.district = front.address_entities.district;
                if (!string.IsNullOrEmpty(front.address_entities.ward)) combined.address_entities.ward = front.address_entities.ward;
                if (!string.IsNullOrEmpty(front.address_entities.street)) combined.address_entities.street = front.address_entities.street;
            }

            if (!string.IsNullOrEmpty(front.doe)) combined.doe = front.doe;
            if (!string.IsNullOrEmpty(front.doe_prob)) combined.doe_prob = front.doe_prob;
            if (!string.IsNullOrEmpty(front.type)) combined.type = front.type;
            if (!string.IsNullOrEmpty(front.type_new)) combined.type_new = front.type_new;
        }

        // Hàm merge dữ liệu mặt SAU (CHỈ kiểm tra null hoặc rỗng)
        private void MergeBackData(CardData combined, CardData back)
        {
            if (!string.IsNullOrEmpty(back.ethnicity)) combined.ethnicity = back.ethnicity;
            if (!string.IsNullOrEmpty(back.ethnicity_prob)) combined.ethnicity_prob = back.ethnicity_prob;
            if (!string.IsNullOrEmpty(back.religion)) combined.religion = back.religion;
            if (!string.IsNullOrEmpty(back.religion_prob)) combined.religion_prob = back.religion_prob;
            if (!string.IsNullOrEmpty(back.features)) combined.features = back.features;
            if (!string.IsNullOrEmpty(back.features_prob)) combined.features_prob = back.features_prob;
            if (!string.IsNullOrEmpty(back.issue_date)) combined.issue_date = back.issue_date;
            if (!string.IsNullOrEmpty(back.issue_date_prob)) combined.issue_date_prob = back.issue_date_prob;
            if (!string.IsNullOrEmpty(back.issue_loc)) combined.issue_loc = back.issue_loc;
            if (!string.IsNullOrEmpty(back.issue_loc_prob)) combined.issue_loc_prob = back.issue_loc_prob;
            if (!string.IsNullOrEmpty(back.type)) combined.type = back.type;
            if (!string.IsNullOrEmpty(back.type_new)) combined.type_new = back.type_new;
        }

        private async Task<ApiResponse> ReadCCCDFromStreamAsync(Stream imageStream, string fileName, string apiKey)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("api-key", apiKey);

                using (var content = new MultipartFormDataContent())
                {
                    var imageContent = new StreamContent(imageStream);
                    imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
                    content.Add(imageContent, "image", fileName);

                    HttpResponseMessage response = await client.PostAsync("https://api.fpt.ai/vision/idr/vnm/", content);

                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    ApiResponse apiResponse = JsonConvert.DeserializeObject<ApiResponse>(jsonResponse);
                    apiResponse.filename = fileName;
                    return apiResponse;

                }
            }
        }
    }
}
