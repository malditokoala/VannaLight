using System;
using System.Collections.Generic;
using System.Text;
using VannaLight.Core.Models;

namespace VannaLight.Core.Abstractions;

public interface ILlmRuntimeProfileProvider
{
    LlmRuntimeProfile GetActiveProfile();
}
