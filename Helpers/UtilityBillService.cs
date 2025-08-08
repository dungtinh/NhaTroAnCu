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

        public int GetHighestWaterIndexEnd(int roomId, int month, int year)
        {
            var prevBills = db.UtilityBills
                .Where(b => b.RoomId == roomId && (b.Year < year || (b.Year == year && b.Month < month)) && b.WaterIndexEnd.HasValue)
                .ToList();
            
            if (prevBills.Count > 0)
            {
                return prevBills.Max(b => b.WaterIndexEnd.Value);
            }
            
            return 0;
        }
    }
}