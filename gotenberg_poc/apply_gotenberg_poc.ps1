$sourceRoot = 'C:\Users\edggom\source\repos\malditokoala\VannaLight\gotenberg_poc'
$targetRoot = 'E:\Repositorios\QrPrensas\QrPrensas'

Copy-Item (Join-Path $sourceRoot 'GotenbergDocumentPdfConverter.cs') (Join-Path $targetRoot 'Services\GotenbergDocumentPdfConverter.cs') -Force
Copy-Item (Join-Path $sourceRoot 'CompositeOfficeDocumentPdfConverter.cs') (Join-Path $targetRoot 'Services\CompositeOfficeDocumentPdfConverter.cs') -Force

$programPath = Join-Path $targetRoot 'Program.cs'
$program = Get-Content $programPath -Raw
$program = $program.Replace(
@"
builder.Services.AddScoped<IPressStatusService, PressStatusService>();
builder.Services.AddSingleton<IOfficeDocumentPdfConverter, LibreOfficeDocumentPdfConverter>();
builder.Services.AddScoped<IGraphVisualAidService, GraphVisualAidService>();
"@,
@"
builder.Services.AddScoped<IPressStatusService, PressStatusService>();
builder.Services.AddSingleton<LibreOfficeDocumentPdfConverter>();
builder.Services.AddSingleton<GotenbergDocumentPdfConverter>();
builder.Services.AddSingleton<IOfficeDocumentPdfConverter, CompositeOfficeDocumentPdfConverter>();
builder.Services.AddScoped<IGraphVisualAidService, GraphVisualAidService>();
"@)
Set-Content -Path $programPath -Value $program -Encoding UTF8

$optionsPath = Join-Path $targetRoot 'Models\GraphVisualAidOptions.cs'
$options = Get-Content $optionsPath -Raw
$options = $options.Replace(
@"
    public bool PreferLocalOfficeConversion { get; set; } = true;
    public string LibreOfficeExecutablePath { get; set; } = @"C:\Program Files\LibreOffice\program\soffice.com";
"@,
@"
    public bool PreferGotenbergConversion { get; set; }
    public string GotenbergBaseUrl { get; set; } = "http://localhost:3000";
    public int GotenbergTimeoutSeconds { get; set; } = 120;
    public bool GotenbergSinglePageSheets { get; set; } = true;
    public string GotenbergTraceHeader { get; set; } = "qrprensas-preview";
    public bool PreferLocalOfficeConversion { get; set; } = true;
    public string LibreOfficeExecutablePath { get; set; } = @"C:\Program Files\LibreOffice\program\soffice.com";
"@)
Set-Content -Path $optionsPath -Value $options -Encoding UTF8

$appSettingsPath = Join-Path $targetRoot 'appsettings.json'
$appSettings = Get-Content $appSettingsPath -Raw
$appSettings = $appSettings.Replace(
@'
    "PreferLocalOfficeConversion": true,
'@,
@'
    "PreferGotenbergConversion": true,
    "GotenbergBaseUrl": "http://localhost:3000",
    "GotenbergTimeoutSeconds": 120,
    "GotenbergSinglePageSheets": true,
    "GotenbergTraceHeader": "qrprensas-preview",
    "PreferLocalOfficeConversion": true,
'@)
Set-Content -Path $appSettingsPath -Value $appSettings -Encoding UTF8

$graphPath = Join-Path $targetRoot 'Services\GraphVisualAidService.cs'
$graph = Get-Content $graphPath -Raw
$graph = $graph.Replace(
    'Preview PDF generado localmente con LibreOffice. ItemId={ItemId}, FileName={FileName}, PartNumber={PartNumber}, Bytes={Bytes}',
    'Preview PDF generado por convertidor local. ItemId={ItemId}, FileName={FileName}, PartNumber={PartNumber}, Bytes={Bytes}')
Set-Content -Path $graphPath -Value $graph -Encoding UTF8
