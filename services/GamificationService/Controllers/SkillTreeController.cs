using System.Text.Json;
using GamificationService.Application.Interfaces;
using GamificationService.Contracts.Requests;
using GamificationService.Contracts.Responses;
using GamificationService.Domain.Constants;
using GamificationService.Domain.Entities;
using GamificationService.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GamificationService.Controllers;

[ApiController]
[Route("skill-tree")]
[Authorize]
/// <summary>
/// Публичный API skill tree.
/// Пользователь читает дерево по проекту и фиксирует выбор узла по tier,
/// а owner/admin проекта может редактировать project-scoped конфигурацию дерева.
/// </summary>
public class SkillTreeController : AuthenticatedGamificationController
{
    private static readonly HashSet<string> AllowedBranches = new(StringComparer.OrdinalIgnoreCase) { "A", "B" };

    private readonly GamificationDbContext _db;
    private readonly IProjectAuthorizationClient _projectAuthorizationClient;

    public SkillTreeController(
        GamificationDbContext db,
        IProjectAuthorizationClient projectAuthorizationClient)
    {
        _db = db;
        _projectAuthorizationClient = projectAuthorizationClient;
    }

    [HttpGet("{projectId:guid}")]
    public async Task<IActionResult> GetSkillTree(Guid projectId)
    {
        var userIdResult = GetUserIdOrUnauthorized();
        if (userIdResult.Result is not null)
        {
            return userIdResult.Result;
        }

        var userId = userIdResult.Value;
        var nodes = await GetEffectiveNodesAsync(projectId, includeInactive: false);
        var choices = await _db.UserSkillTreeChoices
            .Where(choice => choice.UserId == userId && choice.ProjectId == projectId)
            .ToListAsync();

        var projectXp = await _db.UserProjectXp
            .FirstOrDefaultAsync(progress => progress.UserId == userId && progress.ProjectId == projectId);

        var tasksCompleted = projectXp?.TasksCompleted ?? 0;
        var result = nodes.Select(node =>
        {
            var isChosen = choices.Any(choice => choice.NodeId == node.Id);
            var tierUnlocked = tasksCompleted >= node.Tier * 10;
            var previousTierChosen = node.Tier == 1 || choices.Any(choice => choice.Tier == node.Tier - 1);

            return new SkillTreeNodeResponse
            {
                Id = node.Id,
                Tier = node.Tier,
                Branch = node.Branch,
                Title = node.Title,
                Description = node.Description,
                RewardJson = node.RewardJson,
                IsChosen = isChosen,
                IsAvailable = node.IsActive &&
                    tierUnlocked &&
                    previousTierChosen &&
                    !isChosen &&
                    !choices.Any(choice => choice.Tier == node.Tier)
            };
        });

        return Ok(result);
    }

    [HttpGet("{projectId:guid}/config")]
    public async Task<IActionResult> GetSkillTreeConfig(Guid projectId)
    {
        var userIdResult = GetUserIdOrUnauthorized();
        if (userIdResult.Result is not null)
        {
            return userIdResult.Result;
        }

        var authorization = await _projectAuthorizationClient.GetProjectAuthorizationAsync(projectId, userIdResult.Value);
        if (!authorization.IsOwnerOrAdmin)
        {
            return Forbid();
        }

        var nodes = await GetEffectiveNodesAsync(projectId, includeInactive: true);
        return Ok(nodes.Select(MapConfigNodeResponse));
    }

    [HttpPut("{projectId:guid}/config")]
    public async Task<IActionResult> UpdateSkillTreeConfig(Guid projectId, [FromBody] UpdateSkillTreeConfigRequest request)
    {
        var userIdResult = GetUserIdOrUnauthorized();
        if (userIdResult.Result is not null)
        {
            return userIdResult.Result;
        }

        var authorization = await _projectAuthorizationClient.GetProjectAuthorizationAsync(projectId, userIdResult.Value);
        if (!authorization.IsOwnerOrAdmin)
        {
            return Forbid();
        }

        var validationError = ValidateConfigRequest(request);
        if (validationError is not null)
        {
            return BadRequest(new { error = validationError });
        }

        var existingNodes = await _db.SkillTreeNodes
            .Where(node => node.ProjectId == projectId)
            .ToListAsync();
        var existingBySlot = existingNodes.ToDictionary(
            node => BuildSlotKey(node.Tier, node.Branch),
            StringComparer.OrdinalIgnoreCase);
        var incomingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var requestNode in request.Nodes)
        {
            var normalizedBranch = NormalizeBranch(requestNode.Branch);
            var slotKey = BuildSlotKey(requestNode.Tier, normalizedBranch);
            incomingKeys.Add(slotKey);

            if (existingBySlot.TryGetValue(slotKey, out var existingNode))
            {
                existingNode.Title = requestNode.Title.Trim();
                existingNode.Description = requestNode.Description.Trim();
                existingNode.RewardJson = requestNode.RewardJson.Trim();
                existingNode.IsActive = requestNode.IsActive;
                continue;
            }

            _db.SkillTreeNodes.Add(new SkillTreeNode
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Tier = requestNode.Tier,
                Branch = normalizedBranch,
                Title = requestNode.Title.Trim(),
                Description = requestNode.Description.Trim(),
                RewardJson = requestNode.RewardJson.Trim(),
                IsActive = requestNode.IsActive
            });
        }

        var removedNodeIds = existingNodes
            .Where(node => !incomingKeys.Contains(BuildSlotKey(node.Tier, node.Branch)))
            .Select(node => node.Id)
            .ToList();
        if (removedNodeIds.Count > 0)
        {
            _db.SkillTreeNodes.RemoveRange(existingNodes.Where(node => removedNodeIds.Contains(node.Id)));
        }

        // При смене project-specific конфигурации лучше сбросить выборы пользователей,
        // чем оставить choices на узлы, которых уже нет или которые больше неактивны.
        var choices = await _db.UserSkillTreeChoices
            .Where(choice => choice.ProjectId == projectId)
            .ToListAsync();
        if (choices.Count > 0)
        {
            _db.UserSkillTreeChoices.RemoveRange(choices);
        }

        await _db.SaveChangesAsync();

        var nodes = await GetEffectiveNodesAsync(projectId, includeInactive: true);
        return Ok(nodes.Select(MapConfigNodeResponse));
    }

    [HttpPost("{projectId:guid}/reset")]
    public async Task<IActionResult> ResetSkillTreeConfig(Guid projectId)
    {
        var userIdResult = GetUserIdOrUnauthorized();
        if (userIdResult.Result is not null)
        {
            return userIdResult.Result;
        }

        var authorization = await _projectAuthorizationClient.GetProjectAuthorizationAsync(projectId, userIdResult.Value);
        if (!authorization.IsOwnerOrAdmin)
        {
            return Forbid();
        }

        var projectNodes = await _db.SkillTreeNodes
            .Where(node => node.ProjectId == projectId)
            .ToListAsync();
        if (projectNodes.Count > 0)
        {
            _db.SkillTreeNodes.RemoveRange(projectNodes);
        }

        var choices = await _db.UserSkillTreeChoices
            .Where(choice => choice.ProjectId == projectId)
            .ToListAsync();
        if (choices.Count > 0)
        {
            _db.UserSkillTreeChoices.RemoveRange(choices);
        }

        await _db.SaveChangesAsync();

        var nodes = await GetEffectiveNodesAsync(projectId, includeInactive: true);
        return Ok(nodes.Select(MapConfigNodeResponse));
    }

    [HttpPost("{projectId:guid}/choose")]
    public async Task<IActionResult> ChooseNode(Guid projectId, [FromBody] SkillTreeChoiceRequest request)
    {
        var userIdResult = GetUserIdOrUnauthorized();
        if (userIdResult.Result is not null)
        {
            return userIdResult.Result;
        }

        var userId = userIdResult.Value;
        var node = await _db.SkillTreeNodes.FirstOrDefaultAsync(node =>
            node.Id == request.NodeId &&
            node.IsActive &&
            (node.ProjectId == projectId || node.ProjectId == GamificationCatalog.SkillTreeTemplateProjectId));

        if (node == null)
        {
            return NotFound(new { error = "Node not found" });
        }

        var alreadyChosen = await _db.UserSkillTreeChoices.AnyAsync(choice =>
            choice.UserId == userId && choice.ProjectId == projectId && choice.Tier == node.Tier);

        if (alreadyChosen)
        {
            return BadRequest(new { error = "Already chose a node for this tier" });
        }

        _db.UserSkillTreeChoices.Add(new UserSkillTreeChoice
        {
            UserId = userId,
            ProjectId = projectId,
            Tier = node.Tier,
            NodeId = node.Id
        });

        await _db.SaveChangesAsync();
        return Ok(new { message = "Node chosen" });
    }

    private async Task<List<SkillTreeNode>> GetEffectiveNodesAsync(Guid projectId, bool includeInactive)
    {
        var nodes = await _db.SkillTreeNodes
            .Where(node => node.ProjectId == projectId && (includeInactive || node.IsActive))
            .OrderBy(node => node.Tier)
            .ThenBy(node => node.Branch)
            .ToListAsync();

        if (nodes.Count > 0)
        {
            return nodes;
        }

        return await _db.SkillTreeNodes
            .Where(node => node.ProjectId == GamificationCatalog.SkillTreeTemplateProjectId && (includeInactive || node.IsActive))
            .OrderBy(node => node.Tier)
            .ThenBy(node => node.Branch)
            .ToListAsync();
    }

    private static string? ValidateConfigRequest(UpdateSkillTreeConfigRequest request)
    {
        if (request.Nodes is null)
        {
            return "Nodes payload is required.";
        }

        if (request.Nodes.Count > 6)
        {
            return "Skill tree config cannot contain more than six nodes.";
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in request.Nodes)
        {
            if (node.Tier is < 1 or > 3)
            {
                return "Tier must be between 1 and 3.";
            }

            if (!AllowedBranches.Contains(node.Branch))
            {
                return "Branch must be A or B.";
            }

            if (string.IsNullOrWhiteSpace(node.Title))
            {
                return "Node title is required.";
            }

            if (string.IsNullOrWhiteSpace(node.Description))
            {
                return "Node description is required.";
            }

            if (string.IsNullOrWhiteSpace(node.RewardJson))
            {
                return "RewardJson is required.";
            }

            try
            {
                JsonDocument.Parse(node.RewardJson);
            }
            catch (JsonException)
            {
                return "RewardJson must be valid JSON.";
            }

            var slotKey = BuildSlotKey(node.Tier, node.Branch);
            if (!seen.Add(slotKey))
            {
                return "Each tier/branch slot can only be configured once.";
            }
        }

        return null;
    }

    private static SkillTreeConfigNodeResponse MapConfigNodeResponse(SkillTreeNode node)
        => new()
        {
            Id = node.Id,
            ProjectId = node.ProjectId,
            Tier = node.Tier,
            Branch = node.Branch,
            Title = node.Title,
            Description = node.Description,
            RewardJson = node.RewardJson,
            IsActive = node.IsActive
        };

    private static string BuildSlotKey(int tier, string branch)
        => $"{tier}:{NormalizeBranch(branch)}";

    private static string NormalizeBranch(string branch)
        => string.IsNullOrWhiteSpace(branch) ? "A" : branch.Trim().ToUpperInvariant();
}
