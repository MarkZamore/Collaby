using GamificationService.Contracts.Responses;

namespace GamificationService.Application.Interfaces;

public interface IShopService
{
    Task<IEnumerable<ShopItemResponse>> GetItemsAsync(Guid userId, string? category = null);
    Task<bool> PurchaseAsync(Guid userId, Guid itemId);
    Task<IEnumerable<InventoryItemResponse>> GetInventoryAsync(Guid userId);
    Task<bool> EquipItemAsync(Guid userId, Guid itemId);
    Task<bool> UnequipItemAsync(Guid userId, Guid itemId);
}
