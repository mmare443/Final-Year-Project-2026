using System;
using System.Collections.Generic;

namespace LCC_CMS_Api.Models;

public partial class GradeScale
{
    public string GradeLetter { get; set; } = null!;

    public decimal GradeValue { get; set; }
}
