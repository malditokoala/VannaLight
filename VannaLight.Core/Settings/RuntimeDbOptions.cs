using System;
using System.Collections.Generic;
using System.Text;

namespace VannaLight.Core.Settings;


public class RuntimeDbOptions(string dbPath)
{
    public string DbPath { get; } = dbPath;
}
