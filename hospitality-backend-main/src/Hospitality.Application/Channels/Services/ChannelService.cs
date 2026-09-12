using Hospitality.Application.Channels.Commands;
using Hospitality.Application.Common.Interfaces;
using Hospitality.Domain.Entities;
using Hospitality.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Hospitality.Application.Channels.Services;

public class ChannelService : IChannelService
{
    private readonly IApplicationDbContext _context;

    public ChannelService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ChannelDto>> GetChannelsAsync(Guid hotelId)
    {
        return await _context.Channels
            .Where(c => c.HotelId == hotelId)
            .OrderBy(c => c.Name)
            .Select(c => new ChannelDto
            {
                Id = c.Id,
                Name = c.Name,
                ChannelType = c.ChannelType,
                CommissionRate = c.CommissionRate,
                IsActive = c.IsActive,
                CredentialsJson = c.CredentialsJson
            })
            .ToListAsync();
    }

    public async Task<ChannelDto> CreateChannelAsync(UpsertChannelCommand command)
    {
        Validate(command);

        var exists = await _context.Channels.AnyAsync(c =>
            c.HotelId == command.HotelId && c.Name.Trim().ToLower() == command.Name.Trim().ToLower());
        if (exists)
        {
            throw new ValidationException("Ya existe un canal con ese nombre.");
        }

        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            HotelId = command.HotelId,
            Name = command.Name.Trim(),
            ChannelType = command.ChannelType,
            CommissionRate = command.CommissionRate,
            IsActive = command.IsActive,
            CredentialsJson = command.CredentialsJson,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Channels.Add(channel);
        await _context.SaveChangesAsync();
        return ToDto(channel);
    }

    public async Task<ChannelDto> UpdateChannelAsync(Guid id, UpsertChannelCommand command)
    {
        var channel = await _context.Channels.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new KeyNotFoundException($"Canal con ID {id} no encontrado.");

        Validate(command);

        var dup = await _context.Channels.AnyAsync(c =>
            c.HotelId == channel.HotelId && c.Id != id && c.Name.Trim().ToLower() == command.Name.Trim().ToLower());
        if (dup)
        {
            throw new ValidationException("Ya existe un canal con ese nombre.");
        }

        channel.Name = command.Name.Trim();
        channel.ChannelType = command.ChannelType;
        channel.CommissionRate = command.CommissionRate;
        channel.IsActive = command.IsActive;
        channel.CredentialsJson = command.CredentialsJson;
        channel.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return ToDto(channel);
    }

    public async Task DeleteChannelAsync(Guid id)
    {
        var channel = await _context.Channels.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new KeyNotFoundException($"Canal con ID {id} no encontrado.");

        _context.Channels.Remove(channel);
        await _context.SaveChangesAsync();
    }

    private static void Validate(UpsertChannelCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ValidationException("El nombre del canal es obligatorio.");
        }

        if (command.CommissionRate < 0 || command.CommissionRate > 100)
        {
            throw new ValidationException("La comisión debe estar entre 0 y 100.");
        }
    }

    private static ChannelDto ToDto(Channel channel)
    {
        return new ChannelDto
        {
            Id = channel.Id,
            Name = channel.Name,
            ChannelType = channel.ChannelType,
            CommissionRate = channel.CommissionRate,
            IsActive = channel.IsActive,
            CredentialsJson = channel.CredentialsJson
        };
    }
}