using Hospitality.Application.Common.Interfaces;
using Hospitality.Application.RatePlans.Commands;
using Hospitality.Domain.Entities;
using Hospitality.Domain.Enums;
using Hospitality.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Hospitality.Application.RatePlans.Services;

public class RatePlanService : IRatePlanService
{
    private readonly IApplicationDbContext _context;

    public RatePlanService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RatePlanDto>> GetPlansAsync(Guid hotelId)
    {
        return await _context.RatePlans
            .Where(p => p.HotelId == hotelId )
            .OrderByDescending(p => p.IsDefault)
            .ThenBy(p => p.Name)
            .Select(p => new RatePlanDto
            {
                Id = p.Id,
                Name = p.Name,
                Multiplier = p.Multiplier,
                MinStay = p.MinStay,
                Refundability = p.Refundability,
                CancellationDeadlineHours = p.CancellationDeadlineHours,
                IsDefault = p.IsDefault,
                UsageCount = p.RoomTypes.Count
            })
            .ToListAsync();
    }

    public async Task<RatePlanDto> CreatePlanAsync(UpsertRatePlanCommand command)
    {
        Validate(command, hotelId: command.HotelId, excludeId: null);

        if (command.IsDefault)
        {
            await _context.RatePlans
                .Where(p => p.HotelId == command.HotelId  && p.IsDefault)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDefault, false));
        }

        var plan = new RatePlan
        {
            Id = Guid.NewGuid(),
            HotelId = command.HotelId,
            Name = command.Name.Trim(),
            Multiplier = command.Multiplier ?? 1m,
            MinStay = command.MinStay ?? 1,
            Refundability = command.Refundability ?? RefundabilityType.Flexible,
            CancellationDeadlineHours = command.CancellationDeadlineHours ?? 24,
            IsDefault = command.IsDefault,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.RatePlans.Add(plan);
        await _context.SaveChangesAsync();
        return ToDto(plan, 0);
    }

    public async Task<RatePlanDto> UpdatePlanAsync(Guid id, UpsertRatePlanCommand command)
    {
        var plan = await _context.RatePlans.FirstOrDefaultAsync(p => p.Id == id )
            ?? throw new KeyNotFoundException($"Plan de tarifas con ID {id} no encontrado.");

        Validate(command, plan.HotelId, id);

        if (command.IsDefault && !plan.IsDefault)
        {
            await _context.RatePlans
                .Where(p => p.HotelId == plan.HotelId  && p.IsDefault && p.Id != id)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDefault, false));
        }
        else if (!command.IsDefault && plan.IsDefault)
        {
            throw new ValidationException("El plan predeterminado no puede desactivarse sin marcar otro como tal.");
        }

        plan.Name = command.Name.Trim();
        plan.Multiplier = command.Multiplier ?? plan.Multiplier;
        plan.MinStay = command.MinStay ?? plan.MinStay;
        plan.Refundability = command.Refundability ?? plan.Refundability;
        plan.CancellationDeadlineHours = command.CancellationDeadlineHours ?? plan.CancellationDeadlineHours;
        plan.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return ToDto(plan, await CountUsageAsync(id));
    }

    public async Task DeletePlanAsync(Guid id)
    {
        var plan = await _context.RatePlans.FirstOrDefaultAsync(p => p.Id == id )
            ?? throw new KeyNotFoundException($"Plan de tarifas con ID {id} no encontrado.");

        var usage = await _context.RoomTypes.CountAsync(rt => rt.RatePlanId == id );
        if (usage > 0)
        {
            throw new ValidationException($"Hay {usage} tipo(s) de habitación asignados a este plan. Desasigna antes de eliminar.");
        }

        _context.RatePlans.Remove(plan);
        await _context.SaveChangesAsync();
    }

    public async Task<int> AssignPlanAsync(Guid id, List<Guid> roomTypeIds)
    {
        var plan = await _context.RatePlans.FirstOrDefaultAsync(p => p.Id == id )
            ?? throw new KeyNotFoundException($"Plan de tarifas con ID {id} no encontrado.");

        if (roomTypeIds.Count == 0)
        {
            var current = await _context.RoomTypes
                .Where(rt => rt.RatePlanId == id )
                .ToListAsync();
            foreach (var rt in current)
            {
                rt.RatePlanId = null;
            }

            await _context.SaveChangesAsync();
            return current.Count;
        }

        var invalid = await _context.RoomTypes
            .Where(rt => roomTypeIds.Contains(rt.Id) && rt.HotelId != plan.HotelId)
            .Select(rt => rt.Id)
            .ToListAsync();
        if (invalid.Count > 0)
        {
            throw new ValidationException("Algunos tipos de habitación no pertenecen al hotel del plan.");
        }

        var types = await _context.RoomTypes
            .Where(rt => roomTypeIds.Contains(rt.Id))
            .ToListAsync();
        foreach (var rt in types)
        {
            rt.RatePlanId = plan.Id;
            rt.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return types.Count;
    }

    private void Validate(UpsertRatePlanCommand command, Guid hotelId, Guid? excludeId)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ValidationException("El nombre del plan es obligatorio.");
        }

        if (command.Multiplier is <= 0)
        {
            throw new ValidationException("El multiplicador debe ser mayor que cero.");
        }

        if (command.MinStay is < 1)
        {
            throw new ValidationException("La estancia mínima debe ser de al menos 1 noche.");
        }

        if (command.CancellationDeadlineHours is < 0)
        {
            throw new ValidationException("Las horas límite de cancelación no pueden ser negativas.");
        }

        var dup = _context.RatePlans.Any(p =>
            p.HotelId == hotelId  && p.Name.Trim().ToLower() == command.Name.Trim().ToLower()
            && (!excludeId.HasValue || p.Id != excludeId.Value));
        if (dup)
        {
            throw new ValidationException("Ya existe un plan con ese nombre en la propiedad.");
        }
    }

    private async Task<int> CountUsageAsync(Guid id)
    {
        return await _context.RoomTypes.CountAsync(rt => rt.RatePlanId == id );
    }

    private static RatePlanDto ToDto(RatePlan plan, int usage)
    {
        return new RatePlanDto
        {
            Id = plan.Id,
            Name = plan.Name,
            Multiplier = plan.Multiplier,
            MinStay = plan.MinStay,
            Refundability = plan.Refundability,
            CancellationDeadlineHours = plan.CancellationDeadlineHours,
            IsDefault = plan.IsDefault,
            UsageCount = usage
        };
    }
}