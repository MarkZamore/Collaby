namespace GamificationService.Domain.Entities;

public class UserInventory
{
    public Guid UserId { get; set; }
    public Guid ItemId { get; set; }
    public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;
    public bool IsEquipped { get; set; }

    public virtual ShopItem? Item { get; set; }
}
