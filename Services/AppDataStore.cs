using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using study_hub_reservation_system.Models;

namespace study_hub_reservation_system.Services;

public class AppDataStore
{
    private readonly string _usersPath;
    private readonly string _reservationsPath;
    private readonly JsonSerializerOptions _jsonOptions;

}
