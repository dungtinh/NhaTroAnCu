using NhaTroAnCu.Models;
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
        /// Lấy chỉ số nước cao nhất của hợp đồng
        /// </summary>
        public int GetHighestWaterIndexEndForContract(int contractId)
        {
            if (contractId == 0) return 0;

            var lastBill = db.UtilityBills
                .Where(b => b.ContractId == contractId)
                .OrderByDescending(b => b.Year)
                .ThenByDescending(b => b.Month)
                .FirstOrDefault();

            return lastBill?.WaterIndexEnd ?? 0;
        }

        /// <summary>
        /// Lấy chỉ số nước cao nhất của phòng (dùng cho trường hợp tương thích cũ)
        /// </summary>
        public int GetHighestWaterIndexEnd(int roomId)
        {
            // Tìm hợp đồng active của phòng
            var activeContract = db.Contracts
                .Where(c => c.Status == "Active" && c.ContractRooms.Any(cr => cr.RoomId == roomId))
                .FirstOrDefault();

            if (activeContract != null)
            {
                return GetHighestWaterIndexEndForContract(activeContract.Id);
            }

            return 0;
        }
    }
}