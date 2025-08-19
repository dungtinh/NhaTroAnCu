using NhaTroAnCu.Models;
using System.Diagnostics.Contracts;
using System.Linq;

namespace NhaTroAnCu.Helpers
{
    public class UtilityBillService
    {
        private NhaTroAnCuEntities db;

        public UtilityBillService(NhaTroAnCuEntities context)
        {
            db = context;
        }

        /// <summary>
        /// Lấy chỉ số nước cao nhất của phòng (dùng cho trường hợp tương thích cũ)
        /// </summary>
        public int GetHighestWaterIndexEnd(int roomId)
        {
            var maxWaterIndex = db.UtilityBills
            .Where(b => b.RoomId == roomId)
            .Max(b => (int?)b.WaterIndexEnd) ?? 0;
            return maxWaterIndex;
        }
    }
}