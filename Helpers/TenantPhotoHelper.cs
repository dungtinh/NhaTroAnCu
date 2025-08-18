using System;
using System.IO;
using System.Web;

namespace NhaTroAnCu.Helpers
{
    /// <summary>
    /// Helper class để xử lý upload và lưu ảnh của tenant
    /// </summary>
    public static class TenantPhotoHelper
    {
        // Các định dạng file ảnh được chấp nhận
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };

        // Kích thước file tối đa (5MB)
        private const int MaxFileSize = 5 * 1024 * 1024; // 5MB in bytes

        /// <summary>
        /// Lưu ảnh tenant và trả về đường dẫn relative
        /// </summary>
        /// <param name="photoFile">File ảnh từ request</param>
        /// <param name="tenantIdentityCard">Số CCCD của tenant (optional, dùng để đặt tên file)</param>
        /// <returns>Đường dẫn relative của file đã lưu</returns>
        public static string SaveTenantPhoto(HttpPostedFileBase photoFile, string tenantIdentityCard = null)
        {
            if (photoFile == null || photoFile.ContentLength == 0)
            {
                return null;
            }

            // Validate file size
            if (photoFile.ContentLength > MaxFileSize)
            {
                throw new InvalidOperationException($"File ảnh không được vượt quá {MaxFileSize / 1024 / 1024}MB");
            }

            // Validate file extension
            var extension = Path.GetExtension(photoFile.FileName).ToLower();
            if (!IsValidImageExtension(extension))
            {
                throw new InvalidOperationException("Định dạng file không được hỗ trợ. Chỉ chấp nhận: " + string.Join(", ", AllowedExtensions));
            }

            try
            {
                // Tạo tên file unique
                var fileName = GenerateUniqueFileName(tenantIdentityCard, extension);

                // Đường dẫn thư mục lưu file
                var uploadDir = GetUploadDirectory();

                // Tạo thư mục nếu chưa tồn tại
                if (!Directory.Exists(uploadDir))
                {
                    Directory.CreateDirectory(uploadDir);
                }

                // Đường dẫn đầy đủ của file
                var filePath = Path.Combine(uploadDir, fileName);

                // Lưu file
                photoFile.SaveAs(filePath);

                // Trả về đường dẫn relative để lưu vào database
                return $"/Uploads/TenantPhotos/{fileName}";
            }
            catch (Exception ex)
            {
                // Log error nếu cần
                throw new InvalidOperationException("Lỗi khi lưu file ảnh: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Lưu nhiều ảnh tenant cùng lúc
        /// </summary>
        /// <param name="photoFiles">Danh sách file ảnh</param>
        /// <param name="tenantIdentityCards">Danh sách số CCCD tương ứng</param>
        /// <returns>Danh sách đường dẫn của các file đã lưu</returns>
        public static string[] SaveMultipleTenantPhotos(HttpPostedFileBase[] photoFiles, string[] tenantIdentityCards = null)
        {
            if (photoFiles == null || photoFiles.Length == 0)
            {
                return new string[0];
            }

            var savedPaths = new string[photoFiles.Length];

            for (int i = 0; i < photoFiles.Length; i++)
            {
                var identityCard = tenantIdentityCards != null && i < tenantIdentityCards.Length
                    ? tenantIdentityCards[i]
                    : null;

                savedPaths[i] = SaveTenantPhoto(photoFiles[i], identityCard);
            }

            return savedPaths;
        }

        /// <summary>
        /// Xóa ảnh tenant
        /// </summary>
        /// <param name="photoPath">Đường dẫn relative của ảnh</param>
        /// <returns>True nếu xóa thành công</returns>
        public static bool DeleteTenantPhoto(string photoPath)
        {
            if (string.IsNullOrEmpty(photoPath))
            {
                return false;
            }

            try
            {
                // Chuyển từ relative path sang physical path
                var physicalPath = HttpContext.Current.Server.MapPath(photoPath);

                if (File.Exists(physicalPath))
                {
                    File.Delete(physicalPath);
                    return true;
                }

                return false;
            }
            catch
            {
                // Log error nếu cần
                return false;
            }
        }

        /// <summary>
        /// Di chuyển ảnh tạm sang thư mục chính thức
        /// </summary>
        /// <param name="tempPath">Đường dẫn file tạm</param>
        /// <param name="tenantIdentityCard">Số CCCD của tenant</param>
        /// <returns>Đường dẫn mới của file</returns>
        public static string MoveTempPhotoToPermanent(string tempPath, string tenantIdentityCard = null)
        {
            if (string.IsNullOrEmpty(tempPath))
            {
                return null;
            }

            try
            {
                var physicalTempPath = HttpContext.Current.Server.MapPath(tempPath);

                if (!File.Exists(physicalTempPath))
                {
                    return null;
                }

                var extension = Path.GetExtension(tempPath);
                var newFileName = GenerateUniqueFileName(tenantIdentityCard, extension);
                var uploadDir = GetUploadDirectory();
                var newFilePath = Path.Combine(uploadDir, newFileName);

                // Di chuyển file
                File.Move(physicalTempPath, newFilePath);

                return $"/Uploads/TenantPhotos/{newFileName}";
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Validate xem file có phải là ảnh hợp lệ không
        /// </summary>
        /// <param name="photoFile">File cần kiểm tra</param>
        /// <returns>True nếu file hợp lệ</returns>
        public static bool IsValidImageFile(HttpPostedFileBase photoFile)
        {
            if (photoFile == null || photoFile.ContentLength == 0)
            {
                return false;
            }

            if (photoFile.ContentLength > MaxFileSize)
            {
                return false;
            }

            var extension = Path.GetExtension(photoFile.FileName).ToLower();
            return IsValidImageExtension(extension);
        }

        /// <summary>
        /// Lấy thông tin chi tiết về file ảnh
        /// </summary>
        /// <param name="photoPath">Đường dẫn relative của ảnh</param>
        /// <returns>Object chứa thông tin file</returns>
        public static PhotoInfo GetPhotoInfo(string photoPath)
        {
            if (string.IsNullOrEmpty(photoPath))
            {
                return null;
            }

            try
            {
                var physicalPath = HttpContext.Current.Server.MapPath(photoPath);

                if (!File.Exists(physicalPath))
                {
                    return null;
                }

                var fileInfo = new FileInfo(physicalPath);

                return new PhotoInfo
                {
                    FileName = fileInfo.Name,
                    FilePath = photoPath,
                    FileSize = fileInfo.Length,
                    Extension = fileInfo.Extension,
                    CreatedDate = fileInfo.CreationTime,
                    ModifiedDate = fileInfo.LastWriteTime
                };
            }
            catch
            {
                return null;
            }
        }

        #region Private Methods

        /// <summary>
        /// Kiểm tra extension có hợp lệ không
        /// </summary>
        private static bool IsValidImageExtension(string extension)
        {
            return Array.Exists(AllowedExtensions, ext => ext.Equals(extension, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Tạo tên file unique
        /// </summary>
        private static string GenerateUniqueFileName(string tenantIdentityCard, string extension)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var random = Guid.NewGuid().ToString("N").Substring(0, 6);

            if (!string.IsNullOrEmpty(tenantIdentityCard))
            {
                // Loại bỏ ký tự không hợp lệ trong tên file
                var safeIdentityCard = tenantIdentityCard.Replace(" ", "").Replace("-", "");
                return $"tenant_{safeIdentityCard}_{timestamp}_{random}{extension}";
            }

            return $"tenant_{timestamp}_{random}{extension}";
        }

        /// <summary>
        /// Lấy đường dẫn thư mục upload
        /// </summary>
        private static string GetUploadDirectory()
        {
            var uploadDir = HttpContext.Current.Server.MapPath("~/Uploads/TenantPhotos");

            // Tạo thư mục theo năm/tháng để dễ quản lý
            var yearMonth = DateTime.Now.ToString("yyyy-MM");
            uploadDir = Path.Combine(uploadDir, yearMonth);

            return uploadDir;
        }

        #endregion
    }

    /// <summary>
    /// Class chứa thông tin về file ảnh
    /// </summary>
    public class PhotoInfo
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public string Extension { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }

        /// <summary>
        /// Kích thước file dạng đọc được (KB, MB)
        /// </summary>
        public string FileSizeFormatted
        {
            get
            {
                if (FileSize < 1024)
                    return $"{FileSize} B";
                else if (FileSize < 1024 * 1024)
                    return $"{FileSize / 1024.0:F2} KB";
                else
                    return $"{FileSize / (1024.0 * 1024.0):F2} MB";
            }
        }
    }
}