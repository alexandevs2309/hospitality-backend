namespace Hospitality.Domain.Entities;

public class RoomType : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    public int Capacity { get; set; } = 2;
    public int ExtraBedCapacity { get; set; } = 0;
    public decimal ExtraBedPrice { get; set; }
    public string? Amenities { get; set; }
    public string? ImageUrl { get; set; }
    
    // Foreign keys
    public Guid HotelId { get; set; }
    
    // Navigation properties
    public Hotel Hotel { get; set; } = null!;
    public ICollection<Room> Rooms { get; set; } = new List<Room>();

    // Plan de tarifas asignado (opcional)
    public Guid? RatePlanId { get; set; }
    public RatePlan? RatePlan { get; set; }
    
    // Métodos de negocio
    public decimal CalculatePrice(int numberOfGuests, bool includeExtraBed = false)
    {
        var price = BasePrice;
        
        if (includeExtraBed && numberOfGuests > Capacity)
        {
            var extraBedsNeeded = numberOfGuests - Capacity;
            if (extraBedsNeeded <= ExtraBedCapacity)
            {
                price += extraBedsNeeded * ExtraBedPrice;
            }
        }
        
        return price;
    }

    public bool CanAccommodate(int numberOfGuests)
    {
        return numberOfGuests <= Capacity + ExtraBedCapacity;
    }
}