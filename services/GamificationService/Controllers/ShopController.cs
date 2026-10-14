using GamificationService.Application.Interfaces;
using GamificationService.Contracts.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GamificationService.Controllers;

[ApiController]
[Route("shop")]
[Authorize]
/// <summary>
/// Пользовательский API магазина и инвентаря.
/// Отдельные маршруты разделяют чтение каталога, покупку и управление экипированными предметами.
/// </summary>
public class ShopController : AuthenticatedGamificationController
{
    private readonly IShopService _shopService;

    public ShopController(IShopService shopService) => _shopService = shopService;

    [HttpGet]
    public async Task<IActionResult> GetItems([FromQuery] string? category = null)
    {
        var userIdResult = GetUserIdOrUnauthorized();
        return userIdResult.Result ?? Ok(await _shopService.GetItemsAsync(userIdResult.Value, category));
    }

    [HttpPost("buy")]
    public async Task<IActionResult> Purchase([FromBody] PurchaseRequest request)
    {
        var userIdResult = GetUserIdOrUnauthorized();
        if (userIdResult.Result is not null)
        {
            return userIdResult.Result;
        }

        var success = await _shopService.PurchaseAsync(userIdResult.Value, request.ItemId);
        if (!success)
        {
            return BadRequest(new { error = "Purchase failed due to insufficient coins or item availability." });
        }

        return Ok(new { message = "Purchase successful" });
    }

    [HttpGet("inventory")]
    public async Task<IActionResult> GetInventory()
    {
        var userIdResult = GetUserIdOrUnauthorized();
        return userIdResult.Result ?? Ok(await _shopService.GetInventoryAsync(userIdResult.Value));
    }

    [HttpPost("equip/{itemId:guid}")]
    public async Task<IActionResult> Equip(Guid itemId)
    {
        var userIdResult = GetUserIdOrUnauthorized();
        if (userIdResult.Result is not null)
        {
            return userIdResult.Result;
        }

        var success = await _shopService.EquipItemAsync(userIdResult.Value, itemId);
        return success ? Ok(new { message = "Item equipped" }) : NotFound();
    }

    [HttpPost("unequip/{itemId:guid}")]
    public async Task<IActionResult> Unequip(Guid itemId)
    {
        var userIdResult = GetUserIdOrUnauthorized();
        if (userIdResult.Result is not null)
        {
            return userIdResult.Result;
        }

        var success = await _shopService.UnequipItemAsync(userIdResult.Value, itemId);
        return success ? Ok(new { message = "Item unequipped" }) : NotFound();
    }
}
