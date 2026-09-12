namespace Hospitality.Domain.Entities;

public class ChannelMapping : BaseEntity
{
    public Guid ChannelId { get; set; }
    public Channel Channel { get; set; } = null!;

    public Guid RoomTypeId { get; set; }
    public RoomType RoomType { get; set; } = null!;

    public string ChannelRoomCode { get; set; } = string.Empty;
    public string? ChannelRatePlanCode { get; set; }
    public bool IsActive { get; set; } = true;
}