using System;
using System.Collections.Generic;

namespace LCC_CMS_Api.Models;

public partial class AccommodationRecord
{
    public int AccommodationId { get; set; }

    public int StudentId { get; set; }

    public int RoomId { get; set; }

    public string Status { get; set; } = null!;

    public int AllocatedBy { get; set; }

    public DateOnly DateAllocated { get; set; }

    public DateOnly? DateVacated { get; set; }

    public virtual Staff AllocatedByNavigation { get; set; } = null!;

    public virtual Room Room { get; set; } = null!;

    public virtual Student Student { get; set; } = null!;
}
