namespace Hospitality.Domain.Entities;

public class HotelMetrics : BaseEntity
{
    public DateTime Period { get; set; } // Fecha del período (diario, semanal, mensual)
    public string PeriodType { get; set; } = "Daily"; // Daily, Weekly, Monthly
    
    // Métricas de ocupación
    public decimal OccupancyRate { get; set; } // Porcentaje
    public decimal AverageDailyRate { get; set; } // ADR
    public decimal RevenuePerAvailableRoom { get; set; } // RevPAR
    
    // Reservaciones
    public int TotalBookings { get; set; }
    public int NewBookings { get; set; }
    public int Cancellations { get; set; }
    public int NoShows { get; set; }
    public int CheckIns { get; set; }
    public int CheckOuts { get; set; }
    
    // Habitaciones
    public int AvailableRooms { get; set; }
    public int OccupiedRooms { get; set; }
    public int OutOfOrderRooms { get; set; }
    public int HousekeepingRooms { get; set; }
    
    // Ingresos
    public decimal TotalRevenue { get; set; }
    public decimal RoomRevenue { get; set; }
    public decimal FoodBeverageRevenue { get; set; }
    public decimal OtherRevenue { get; set; }
    
    // Métricas de huéspedes
    public int TotalGuests { get; set; }
    public int NewGuests { get; set; }
    public int ReturningGuests { get; set; }
    public decimal AverageLengthOfStay { get; set; }
    
    // Métricas de housekeeping
    public int CleanRooms { get; set; }
    public int DirtyRooms { get; set; }
    public int InspectionRooms { get; set; }
    public int MaintenanceRooms { get; set; }
    public decimal HousekeepingEfficiency { get; set; } // Porcentaje
    
    // Métricas de mantenimiento
    public int OpenTickets { get; set; }
    public int InProgressTickets { get; set; }
    public int OverdueTickets { get; set; }
    public decimal AverageResolutionTime { get; set; } // Horas
    
    // Foreign keys
    public Guid HotelId { get; set; }
    
    // Navigation properties
    public Hotel Hotel { get; set; } = null!;
    
    // Métodos de negocio
    public void CalculateDerivedMetrics()
    {
        // Calcular RevPAR si no está establecido
        if (RevenuePerAvailableRoom == 0 && AvailableRooms > 0)
        {
            RevenuePerAvailableRoom = TotalRevenue / AvailableRooms;
        }
        
        // Calcular ocupación si no está establecida
        if (OccupancyRate == 0 && AvailableRooms > 0 && OccupiedRooms > 0)
        {
            OccupancyRate = (decimal)OccupiedRooms / (AvailableRooms + OccupiedRooms) * 100;
        }
        
        // Calcular ADR si no está establecido
        if (AverageDailyRate == 0 && OccupiedRooms > 0)
        {
            AverageDailyRate = RoomRevenue / OccupiedRooms;
        }
        
        // Calcular eficiencia de housekeeping
        if (CleanRooms + DirtyRooms > 0)
        {
            HousekeepingEfficiency = (decimal)CleanRooms / (CleanRooms + DirtyRooms) * 100;
        }
    }
    
    public decimal GetBookingConversionRate()
    {
        if (NewBookings + Cancellations == 0) return 0;
        return (decimal)NewBookings / (NewBookings + Cancellations) * 100;
    }
    
    public decimal GetGuestSatisfactionScore()
    {
        // Esto normalmente vendría de encuestas, pero podemos calcular un proxy
        var factors = new List<decimal>
        {
            OccupancyRate / 100,
            GetBookingConversionRate() / 100,
            HousekeepingEfficiency / 100,
            1 - (decimal)OverdueTickets / Math.Max(OpenTickets + InProgressTickets, 1)
        };
        
        return factors.Average() * 100;
    }
    
    public Dictionary<string, decimal> GetKpis()
    {
        return new Dictionary<string, decimal>
        {
            ["OccupancyRate"] = OccupancyRate,
            ["AverageDailyRate"] = AverageDailyRate,
            ["RevenuePerAvailableRoom"] = RevenuePerAvailableRoom,
            ["TotalRevenue"] = TotalRevenue,
            ["BookingConversionRate"] = GetBookingConversionRate(),
            ["GuestSatisfactionScore"] = GetGuestSatisfactionScore(),
            ["HousekeepingEfficiency"] = HousekeepingEfficiency,
            ["AverageResolutionTime"] = AverageResolutionTime
        };
    }
    
    public bool IsSignificantChange(HotelMetrics previousMetrics, decimal thresholdPercent = 5)
    {
        if (previousMetrics == null) return true;
        
        var changes = new Dictionary<string, decimal>
        {
            ["OccupancyRate"] = Math.Abs(OccupancyRate - previousMetrics.OccupancyRate),
            ["AverageDailyRate"] = Math.Abs(AverageDailyRate - previousMetrics.AverageDailyRate),
            ["TotalRevenue"] = Math.Abs(TotalRevenue - previousMetrics.TotalRevenue)
        };
        
        return changes.Any(c => c.Value > thresholdPercent);
    }
    
    public string GetPeriodDisplay()
    {
        return PeriodType switch
        {
            "Daily" => Period.ToString("dd MMM yyyy"),
            "Weekly" => $"Semana {GetWeekNumber(Period)} - {Period.ToString("MMM yyyy")}",
            "Monthly" => Period.ToString("MMMM yyyy"),
            _ => Period.ToString("dd MMM yyyy")
        };
    }
    
    private static int GetWeekNumber(DateTime date)
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        return culture.Calendar.GetWeekOfYear(date, 
            System.Globalization.CalendarWeekRule.FirstFourDayWeek, 
            DayOfWeek.Monday);
    }
}