using Microsoft.EntityFrameworkCore;
using GamificationService.Application.Interfaces;
using GamificationService.Contracts.Responses;
using GamificationService.Domain.Constants;
using GamificationService.Infrastructure.Data;

namespace GamificationService.Application.Services;

/// <summary>
/// Управляет streak-механикой.
/// Сервис считает последовательность активных дней, применяет freeze и вычисляет multiplier,
/// который затем используется при начислении наград.
/// </summary>
public class StreakService : IStreakService
{
    private readonly GamificationDbContext _db;

    public StreakService(GamificationDbContext db)
    {
        _db = db;
    }

    public async Task RecordActivityAsync(Guid userId)
    {
        var user = await _db.UserGlobalXp.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null) return;

        var today = DateTime.UtcNow.Date;
        if (user.LastActiveDate?.Date == today) return; // Пользователь уже засчитан активным сегодня.

        var yesterday = today.AddDays(-1);

        if (user.LastActiveDate?.Date == yesterday)
        {
            // Активность идёт без разрыва, поэтому streak просто продлевается.
            user.StreakDays++;
        }
        else if (user.LastActiveDate?.Date < yesterday)
        {
            // Есть пропуск хотя бы одного дня. Сначала проверяем, может ли его закрыть накопленный freeze.
            var missedDays = (today - (user.LastActiveDate?.Date ?? today)).Days - 1;
            if (missedDays <= user.FreezeCount)
            {
                user.FreezeCount -= missedDays;
                user.StreakDays++; // Предмет freeze сохраняет текущую серию.
            }
            else
            {
                user.StreakDays = 1; // Если freeze не хватает, streak начинается заново.
            }
        }
        else
        {
            user.StreakDays = 1; // Первая зафиксированная активность создаёт серию из одного дня.
        }

        user.LastActiveDate = today;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<StreakResponse> GetStreakAsync(Guid userId)
    {
        var user = await _db.UserGlobalXp.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null)
            return new StreakResponse { Multiplier = 1.0 };

        var today = DateTime.UtcNow.Date;
        return new StreakResponse
        {
            StreakDays = user.StreakDays,
            FreezeCount = user.FreezeCount,
            Multiplier = GetStreakMultiplier(user.StreakDays),
            LastActiveDate = user.LastActiveDate,
            IsActiveToday = user.LastActiveDate?.Date == today
        };
    }

    public async Task<bool> UseFreezeAsync(Guid userId)
    {
        var user = await _db.UserGlobalXp.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null) return false;

        // Freeze — это обычный расходуемый предмет в инвентаре пользователя.
        // Покупка и применение специально разделены на два шага.
        var freezeItem = await _db.UserInventory
            .FirstOrDefaultAsync(i => i.UserId == userId &&
                i.ItemId == GamificationCatalog.StreakFreezeItemId);

        if (freezeItem != null)
        {
            _db.UserInventory.Remove(freezeItem); // Предмет расходуется в момент активации.
            user.FreezeCount++;
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return true;
        }

        return false;
    }

    public double GetStreakMultiplier(int streakDays)
    {
        if (streakDays < 7) return 1.0;
        var bonus = Math.Min((streakDays / 7) * 0.1, 0.5);
        return 1.0 + bonus; // Максимум 1.5x достигается на streak длиной 35+ дней.
    }
}
