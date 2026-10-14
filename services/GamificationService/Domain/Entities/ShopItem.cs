namespace GamificationService.Domain.Entities;

public class ShopItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // Категории каталога: frames, themes, badges, consumables.
    public int Price { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPurchasable { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
