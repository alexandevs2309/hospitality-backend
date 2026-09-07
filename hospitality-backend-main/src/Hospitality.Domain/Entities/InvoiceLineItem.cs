namespace Hospitality.Domain.Entities;

public class InvoiceLineItem : BaseEntity
{
    public string Description { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal Total { get; set; }
    public string? TaxCode { get; set; } // IVA16, IVA8, Exento, etc.
    public decimal TaxRate { get; set; } = 16.00m; // Porcentaje
    public decimal TaxAmount { get; set; }
    
    // Categorización
    public string Category { get; set; } = "Room"; // Room, Food, Beverage, Service, Other
    public string? ItemCode { get; set; }
    
    // Foreign keys
    public Guid InvoiceId { get; set; }
    public Guid? ReservationId { get; set; }
    
    // Navigation properties
    public Invoice Invoice { get; set; } = null!;
    public Reservation? Reservation { get; set; }
    
    // Métodos de negocio
    public void CalculateTotal()
    {
        Total = UnitPrice * Quantity;
        TaxAmount = Total * (TaxRate / 100);
    }
    
    public void ApplyTax(string taxCode, decimal taxRate)
    {
        TaxCode = taxCode;
        TaxRate = taxRate;
        CalculateTotal();
    }
    
    public string GetCategoryDisplayName()
    {
        return Category switch
        {
            "Room" => "Habitación",
            "Food" => "Alimentos",
            "Beverage" => "Bebidas",
            "Service" => "Servicio",
            "Other" => "Otros",
            _ => Category
        };
    }
    
    public bool IsTaxable => TaxCode != "Exento" && TaxRate > 0;
    
    public decimal GetNetAmount()
    {
        return Total - TaxAmount;
    }
    
    public void UpdateQuantity(int newQuantity)
    {
        if (newQuantity > 0)
        {
            Quantity = newQuantity;
            CalculateTotal();
        }
    }
    
    public void UpdatePrice(decimal newUnitPrice)
    {
        if (newUnitPrice >= 0)
        {
            UnitPrice = newUnitPrice;
            CalculateTotal();
        }
    }
    
    public string GetTaxDescription()
    {
        return IsTaxable ? $"{TaxCode} ({TaxRate}%)" : "Exento";
    }
    
    public void CloneForInvoice(Guid newInvoiceId)
    {
        var clone = new InvoiceLineItem
        {
            Description = Description,
            UnitPrice = UnitPrice,
            Quantity = Quantity,
            TaxCode = TaxCode,
            TaxRate = TaxRate,
            Category = Category,
            ItemCode = ItemCode,
            InvoiceId = newInvoiceId,
            ReservationId = ReservationId
        };
        
        clone.CalculateTotal();
    }
}