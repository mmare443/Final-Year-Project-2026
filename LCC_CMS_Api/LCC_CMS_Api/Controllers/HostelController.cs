using LCC_CMS_Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LCC_CMS_Api.Controllers;

/// <summary>
/// M10 Phase 1 — hostel and room inventory. Occupancy is counted from
/// existing <c>accommodation_records</c> with status Active. Allocation
/// and welfare are later phases.
/// </summary>
[ApiController]
[Route("api")]
public class HostelController : ControllerBase
{
    public const string ActiveStatus = "Active";

    private readonly LccCmsDbContext _dbContext;

    public HostelController(LccCmsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("hostels")]
    public async Task<ActionResult<IEnumerable<HostelRecord>>> GetHostels(
        CancellationToken cancellationToken)
    {
        var hostels = await _dbContext.Hostels
            .AsNoTracking()
            .OrderBy(h => h.HostelName)
            .Select(h => new HostelRecord
            {
                HostelId = h.HostelId,
                HostelName = h.HostelName,
                RoomCount = h.Rooms.Count,
            })
            .ToListAsync(cancellationToken);

        return Ok(hostels);
    }

    [Authorize(Policy = "RegistrarAdminOnly")]
    [HttpPost("hostels")]
    public async Task<ActionResult<HostelRecord>> CreateHostel(
        [FromBody] HostelWriteRequest request,
        CancellationToken cancellationToken)
    {
        var name = request.HostelName?.Trim() ?? "";
        if (name.Length == 0) return BadRequest("Hostel name is required.");
        if (name.Length > 100) return BadRequest("Hostel name must be 100 characters or fewer.");

        if (await _dbContext.Hostels.AnyAsync(h => h.HostelName == name, cancellationToken))
        {
            return Conflict("A hostel with this name already exists.");
        }

        var hostel = new Hostel { HostelName = name };
        _dbContext.Hostels.Add(hostel);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/hostels/{hostel.HostelId}",
            new HostelRecord
            {
                HostelId = hostel.HostelId,
                HostelName = hostel.HostelName,
                RoomCount = 0,
            });
    }

    [Authorize(Policy = "RegistrarAdminOnly")]
    [HttpPut("hostels/{id:int}")]
    public async Task<ActionResult<HostelRecord>> UpdateHostel(
        int id,
        [FromBody] HostelWriteRequest request,
        CancellationToken cancellationToken)
    {
        var name = request.HostelName?.Trim() ?? "";
        if (name.Length == 0) return BadRequest("Hostel name is required.");
        if (name.Length > 100) return BadRequest("Hostel name must be 100 characters or fewer.");

        var hostel = await _dbContext.Hostels.FirstOrDefaultAsync(h => h.HostelId == id, cancellationToken);
        if (hostel is null) return NotFound();

        if (await _dbContext.Hostels.AnyAsync(
                h => h.HostelName == name && h.HostelId != id, cancellationToken))
        {
            return Conflict("A hostel with this name already exists.");
        }

        hostel.HostelName = name;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var roomCount = await _dbContext.Rooms.CountAsync(r => r.HostelId == id, cancellationToken);
        return Ok(new HostelRecord
        {
            HostelId = hostel.HostelId,
            HostelName = hostel.HostelName,
            RoomCount = roomCount,
        });
    }

    [HttpGet("rooms")]
    public async Task<ActionResult<IEnumerable<RoomRecord>>> GetRooms(
        [FromQuery] int? hostelId,
        CancellationToken cancellationToken)
    {
        var query = RoomOccupancyQuery();
        if (hostelId is int hid)
        {
            query = query.Where(r => r.HostelId == hid);
        }

        var rooms = await query
            .OrderBy(r => r.HostelName)
            .ThenBy(r => r.RoomNumber)
            .ToListAsync(cancellationToken);

        return Ok(rooms);
    }

    [Authorize(Policy = "RegistrarAdminOnly")]
    [HttpPost("rooms")]
    public async Task<ActionResult<RoomRecord>> CreateRoom(
        [FromBody] RoomWriteRequest request,
        CancellationToken cancellationToken)
    {
        var error = ValidateRoom(request);
        if (error is not null) return error;

        var hostel = await _dbContext.Hostels
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.HostelId == request.HostelId, cancellationToken);
        if (hostel is null) return BadRequest("Hostel not found.");

        var number = request.RoomNumber.Trim();
        if (await _dbContext.Rooms.AnyAsync(
                r => r.HostelId == request.HostelId && r.RoomNumber == number,
                cancellationToken))
        {
            return Conflict("That room number already exists in this hostel.");
        }

        var room = new Room
        {
            HostelId = request.HostelId,
            RoomNumber = number,
            Capacity = request.Capacity,
        };
        _dbContext.Rooms.Add(room);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/rooms/{room.RoomId}",
            ToRoomRecord(room, hostel.HostelName, 0));
    }

    [Authorize(Policy = "RegistrarAdminOnly")]
    [HttpPut("rooms/{id:int}")]
    public async Task<ActionResult<RoomRecord>> UpdateRoom(
        int id,
        [FromBody] RoomWriteRequest request,
        CancellationToken cancellationToken)
    {
        var error = ValidateRoom(request);
        if (error is not null) return error;

        var room = await _dbContext.Rooms.FirstOrDefaultAsync(r => r.RoomId == id, cancellationToken);
        if (room is null) return NotFound();

        var hostel = await _dbContext.Hostels
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.HostelId == request.HostelId, cancellationToken);
        if (hostel is null) return BadRequest("Hostel not found.");

        var number = request.RoomNumber.Trim();
        if (await _dbContext.Rooms.AnyAsync(
                r => r.HostelId == request.HostelId
                    && r.RoomNumber == number
                    && r.RoomId != id,
                cancellationToken))
        {
            return Conflict("That room number already exists in this hostel.");
        }

        var activeOccupants = await _dbContext.AccommodationRecords
            .AsNoTracking()
            .CountAsync(
                a => a.RoomId == id && a.Status == ActiveStatus,
                cancellationToken);
        if (request.Capacity < activeOccupants)
        {
            return BadRequest(
                $"Capacity cannot be less than current occupancy ({activeOccupants}).");
        }

        room.HostelId = request.HostelId;
        room.RoomNumber = number;
        room.Capacity = request.Capacity;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToRoomRecord(room, hostel.HostelName, activeOccupants));
    }

    private IQueryable<RoomRecord> RoomOccupancyQuery()
    {
        return from r in _dbContext.Rooms.AsNoTracking()
               let occupants = r.AccommodationRecords.Count(a => a.Status == ActiveStatus)
               select new RoomRecord
               {
                   RoomId = r.RoomId,
                   HostelId = r.HostelId,
                   HostelName = r.Hostel.HostelName,
                   RoomNumber = r.RoomNumber,
                   Capacity = r.Capacity,
                   ActiveOccupants = occupants,
                   AvailableSpaces = occupants > r.Capacity ? 0 : r.Capacity - occupants,
               };
    }

    private ActionResult? ValidateRoom(RoomWriteRequest request)
    {
        if (request.HostelId <= 0) return BadRequest("Hostel is required.");
        if (string.IsNullOrWhiteSpace(request.RoomNumber)) return BadRequest("Room number is required.");
        if (request.RoomNumber.Trim().Length > 20)
        {
            return BadRequest("Room number must be 20 characters or fewer.");
        }

        if (request.Capacity <= 0) return BadRequest("Capacity must be greater than 0.");
        return null;
    }

    private static RoomRecord ToRoomRecord(Room room, string hostelName, int activeOccupants)
    {
        var available = room.Capacity - activeOccupants;
        return new RoomRecord
        {
            RoomId = room.RoomId,
            HostelId = room.HostelId,
            HostelName = hostelName,
            RoomNumber = room.RoomNumber,
            Capacity = room.Capacity,
            ActiveOccupants = activeOccupants,
            AvailableSpaces = available < 0 ? 0 : available,
        };
    }
}

public class HostelRecord
{
    public int HostelId { get; set; }
    public string HostelName { get; set; } = "";
    public int RoomCount { get; set; }
}

public class HostelWriteRequest
{
    public string HostelName { get; set; } = "";
}

public class RoomRecord
{
    public int RoomId { get; set; }
    public int HostelId { get; set; }
    public string HostelName { get; set; } = "";
    public string RoomNumber { get; set; } = "";
    public int Capacity { get; set; }
    public int ActiveOccupants { get; set; }
    public int AvailableSpaces { get; set; }
}

public class RoomWriteRequest
{
    public int HostelId { get; set; }
    public string RoomNumber { get; set; } = "";
    public int Capacity { get; set; }
}
