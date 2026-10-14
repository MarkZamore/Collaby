using Microsoft.EntityFrameworkCore;
using GamificationService.Application.Interfaces;
using GamificationService.Contracts.Responses;
using GamificationService.Domain.Entities;
using GamificationService.Infrastructure.Data;

namespace GamificationService.Application.Services;

/// <summary>
/// Обслуживает игровой магазин и инвентарь.
/// Сервис следит за списанием монет, инвариантами владения предметами и состоянием equip/unequip.
/// </summary>
public class ShopService : IShopService
{
    private readonly GamificationDbContext _db;
    private readonly ILogger<ShopService> _logger;

    public ShopService(GamificationDbContext db, ILogger<ShopService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IEnumerable<ShopItemResponse>> GetItemsAsync(Guid userId, string? category = null)
    {
        var query = _db.ShopItems.Where(i => i.IsActive).AsQueryable();
        if (!string.IsNullOrEmpty(category))
            query = query.Where(i => i.Category == category);

        var items = await query.OrderBy(i => i.Category).ThenBy(i => i.Price).ToListAsync();
        var ownedIds = await _db.UserInventory
            .Where(inv => inv.UserId == userId)
            .Select(inv => new { inv.ItemId, inv.IsEquipped })
            .ToListAsync();

        return items.Select(i =>
        {
            var owned = ownedIds.FirstOrDefault(o => o.ItemId == i.Id);
            return new ShopItemResponse
            {
                Id = i.Id,
                Name = i.Name,
                Category = i.Category,
                Price = i.Price,
                Description = i.Description,
                ImageUrl = i.ImageUrl,
                IsPurchasable = i.IsPurchasable,
                IsOwned = owned != null,
                IsEquipped = owned?.IsEquipped ?? false
            };
        });
    }

    public async Task<bool> PurchaseAsync(Guid userId, Guid itemId)
    {
        var item = await _db.ShopItems.FirstOrDefaultAsync(i => i.Id == itemId && i.IsActive);
        if (item == null) return false;

        if (!item.IsPurchasable)
        {
            _logger.LogWarning("User {UserId} attempted to purchase award-only item {ItemId} ({ItemName})",
                userId, itemId, item.Name);
            return false;
        }

        // Нерасходуемые предметы нельзя купить дважды.
        // Для consumables дубликаты допустимы, потому что они должны расходоваться поштучно.
        if (item.Category != "consumables")
        {
            var alreadyOwned = await _db.UserInventory.AnyAsync(inv => inv.UserId == userId && inv.ItemId == itemId);
            if (alreadyOwned) return false;
        }

        var user = await _db.UserGlobalXp.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null || user.Coins < item.Price) return false;

        // Списание монет и добавление в инвентарь выполняются в одной транзакционной операции SaveChanges,
        // чтобы пользователь не мог потерять валюту без получения предмета.
        user.Coins -= item.Price;
        user.UpdatedAt = DateTime.UtcNow;

        _db.UserInventory.Add(new UserInventory
        {
            UserId = userId,
            ItemId = itemId
        });

        await _db.SaveChangesAsync();
        _logger.LogInformation("User {UserId} purchased item {ItemId} ({ItemName}) for {Price} coins",
            userId, itemId, item.Name, item.Price);
        return true;
    }

    public async Task<IEnumerable<InventoryItemResponse>> GetInventoryAsync(Guid userId)
    {
        return await _db.UserInventory
            .Include(inv => inv.Item)
            .Where(inv => inv.UserId == userId && inv.Item != null)
            .OrderByDescending(inv => inv.PurchasedAt)
            .Select(inv => new InventoryItemResponse
            {
                ItemId = inv.ItemId,
                Name = inv.Item!.Name,
                Category = inv.Item.Category,
                IsEquipped = inv.IsEquipped,
                PurchasedAt = inv.PurchasedAt
            })
            .ToListAsync();
    }

    public async Task<bool> EquipItemAsync(Guid userId, Guid itemId)
    {
        // Пока сервис допускает одновременное equip нескольких предметов.
        // Если появятся категории с взаимоисключением, ограничение должно добавляться здесь.
        var inv = await _db.UserInventory.FirstOrDefaultAsync(i => i.UserId == userId && i.ItemId == itemId);
        if (inv == null) return false;
        inv.IsEquipped = true;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UnequipItemAsync(Guid userId, Guid itemId)
    {
        var inv = await _db.UserInventory.FirstOrDefaultAsync(i => i.UserId == userId && i.ItemId == itemId);
        if (inv == null) return false;
        inv.IsEquipped = false;
        await _db.SaveChangesAsync();
        return true;
    }
}
