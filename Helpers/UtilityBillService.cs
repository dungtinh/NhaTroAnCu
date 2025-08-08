using System.Linq;
using NhaTroAnCu.Models;

namespace NhaTroAnCu.Helpers
{
    public class UtilityBillService
    {
        private NhaTroAnCuEntities db;

        public UtilityBillService(NhaTroAnCuEntities dbContext)
        {
            db = dbContext;
        }

        public int GetHighestWaterIndexEnd(int roomId)
        {
            var prevBills = db.UtilityBills
                .Where(b => b.RoomId == roomId)
                .ToList();

            if (prevBills.Count > 0)
            {
                return prevBills.Max(b => b.WaterIndexEnd.Value);
            }

            return 0;
        }
    }
}