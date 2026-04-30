using System.Globalization;
using System.Reflection;
using RandomizerCore.Controllers;
using RandomizerCore.Random;
using RandomizerCore.Randomizer.Logic.Options;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 32 * 1024 * 1024); // 32 MB for ROM

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/hashicons.png", () =>
{
    var stream = Assembly.GetAssembly(typeof(ShufflerController))
        ?.GetManifestResourceStream("RandomizerCore.Resources.hashicons.png");
    return stream is null ? Results.NotFound() : Results.Stream(stream, "image/png");
});

// Prevent concurrent access to the static Rom singleton
var romSemaphore = new SemaphoreSlim(1, 1);

app.MapGet("/api/options", () =>
{
    var controller = new ShufflerController();
    controller.LoadLogicFile();
    var options = controller.GetSelectedOptions().Select(MapOption).ToList();
    return Results.Ok(options);
});

app.MapGet("/api/presets", () =>
{
    var presetRoot = Path.Combine(AppContext.BaseDirectory, "Presets");
    var categories = new[] { "Settings", "Cosmetics", "Mystery Settings", "Mystery Cosmetics" };
    var result = new Dictionary<string, List<object>>();
    foreach (var cat in categories)
    {
        var dir = Path.Combine(presetRoot, cat);
        if (!Directory.Exists(dir)) { result[cat] = []; continue; }
        result[cat] = Directory.GetFiles(dir, "*.yaml")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .Select(n => { var idx = n.LastIndexOf('_'); return idx >= 0 ? (name: n[..idx], sort: int.TryParse(n[(idx+1)..], out var s) ? s : 99, file: n) : (name: n, sort: 99, file: n); })
            .OrderBy(x => x.sort)
            .Select(x => (object)new { name = x.name, file = x.file })
            .ToList();
    }
    return Results.Ok(result);
});

app.MapPost("/api/presets/apply", async (HttpRequest request) =>
{
    if (!request.HasFormContentType) return Results.BadRequest(new { error = "Expected multipart/form-data" });
    var form = await request.ReadFormAsync();
    var category = form["category"].ToString();
    var file = form["file"].ToString();
    if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(file))
        return Results.BadRequest(new { error = "Missing category or file" });

    var safeName = Path.GetFileName(file + ".yaml");
    var path = Path.Combine(AppContext.BaseDirectory, "Presets", category, safeName);
    if (!File.Exists(path)) return Results.NotFound(new { error = $"Preset not found: {file}" });

    try
    {
        var controller = new ShufflerController();
        controller.LoadLogicFile();
        var isCosmetic = category.Contains("Cosmetic", StringComparison.OrdinalIgnoreCase);
        var result = isCosmetic ? controller.LoadCosmeticsFromYaml(path) : controller.LoadLogicSettingsFromYaml(path);
        if (!result.WasSuccessful) return Results.BadRequest(new { error = result.ErrorMessage });
        return Results.Ok(new
        {
            settingString = isCosmetic ? null : controller.GetSelectedSettingsString(),
            cosmeticsString = isCosmetic ? controller.GetSelectedCosmeticsString() : null
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/randomize", async (HttpRequest request) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest(new { error = "Expected multipart/form-data" });

    var form = await request.ReadFormAsync();
    var romFile = form.Files["rom"];
    if (romFile == null)
        return Results.BadRequest(new { error = "Missing ROM file" });

    var seedStr = form["seed"].ToString();
    var settingString = form["settingString"].ToString();
    var cosmeticsString = form["cosmeticsString"].ToString();

    var tempFile = Path.GetTempFileName();
    await romSemaphore.WaitAsync();
    try
    {
        using (var fs = File.Create(tempFile))
            await romFile.CopyToAsync(fs);

        var controller = new ShufflerController();
        controller.LoadLogicFile();

        var loadResult = controller.LoadRom(tempFile);
        if (!loadResult.WasSuccessful)
            return Results.BadRequest(new { error = $"ROM invalide : {loadResult.ErrorMessage}" });

        if (!string.IsNullOrWhiteSpace(settingString))
        {
            var settingResult = controller.LoadSettingsFromSettingString(settingString);
            if (!settingResult.WasSuccessful)
                return Results.BadRequest(new { error = $"Setting string invalide : {settingResult.ErrorMessage}" });
        }

        if (!string.IsNullOrWhiteSpace(cosmeticsString))
        {
            var cosResult = controller.LoadCosmeticsFromCosmeticsString(cosmeticsString);
            if (!cosResult.WasSuccessful)
                return Results.BadRequest(new { error = $"Cosmetics string invalide : {cosResult.ErrorMessage}" });
        }

        ulong seed = !string.IsNullOrWhiteSpace(seedStr) &&
                     ulong.TryParse(seedStr, NumberStyles.HexNumber, null, out var parsed)
            ? parsed
            : new SquaresRandomNumberGenerator().Next();

        controller.SetRandomizationSeed(seed);

        var locResult = controller.LoadLocations();
        if (!locResult.WasSuccessful)
            return Results.BadRequest(new { error = $"Chargement logique échoué : {locResult.ErrorMessage}" });

        var randResult = controller.Randomize(retries: 3);
        if (!randResult.WasSuccessful)
            return Results.BadRequest(new { error = $"Randomisation échouée : {randResult.ErrorMessage}" });

        var patch = controller.CreatePatch(out var patchResult);
        if (!patchResult.WasSuccessful)
            return Results.BadRequest(new { error = $"Création du patch échouée : {patchResult.ErrorMessage}" });

        var spoiler = controller.CreateSpoiler();
        var hashIcons = ComputeHashIcons(controller.GetEventWrites());

        return Results.Ok(new
        {
            patch = Convert.ToBase64String(patch.Content!),
            spoiler,
            seed = $"{seed:X}",
            settingString = controller.GetFinalSettingsString(),
            cosmeticsString = controller.GetFinalCosmeticsString(),
            filename = controller.SeedFilename,
            hashIcons
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    finally
    {
        if (File.Exists(tempFile)) File.Delete(tempFile);
        romSemaphore.Release();
    }
});

app.Run();

static int[] ComputeHashIcons(string eventWrites)
{
    const uint mask = 0b111111;
    uint GetDefine(string name)
    {
        var line = eventWrites.Split('\n').FirstOrDefault(l => l.Contains(name));
        if (line is null) return 0;
        var hex = line.Trim().Split(' ').LastOrDefault()?.TrimStart('#');
        if (hex is null || !hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return 0;
        return uint.TryParse(hex[2..], NumberStyles.HexNumber, null, out var v) ? v : 0;
    }
    var s = GetDefine("seedHashed");
    var c = GetDefine("customRNG");
    var h = GetDefine("settingHash");
    return
    [
        (int)((s >> 24) & mask),
        (int)((s >> 16) & mask),
        (int)((s >>  8) & mask),
        (int)( s        & mask),
        (int)((c >>  8) & mask),
        64,
        (int)((h >>  8) & mask),
        (int)((h >> 16) & mask),
    ];
}

static object MapOption(LogicOptionBase option) => option switch
{
    LogicDropdown dd => new
    {
        name = dd.Name,
        niceName = dd.NiceName,
        type = "Dropdown",
        active = dd.Active,
        settingGroup = dd.SettingGroup,
        settingPage = dd.SettingPage,
        descriptionText = dd.DescriptionText,
        optionType = dd.Type.ToString(),
        selections = dd.Selections.Keys.ToList(),
        selection = dd.Selection
    },
    LogicNumberBox nb => new
    {
        name = nb.Name,
        niceName = nb.NiceName,
        type = "NumberBox",
        active = nb.Active,
        settingGroup = nb.SettingGroup,
        settingPage = nb.SettingPage,
        descriptionText = nb.DescriptionText,
        optionType = nb.Type.ToString(),
        minValue = (int)nb.MinValue,
        maxValue = (int)nb.MaxValue,
        value = int.TryParse(nb.Value, out var nbVal) ? nbVal : (int)nb.DefaultValue
    },
    LogicColorPicker cp => new
    {
        name = cp.Name,
        niceName = cp.NiceName,
        type = "ColorPicker",
        active = cp.Active,
        settingGroup = cp.SettingGroup,
        settingPage = cp.SettingPage,
        descriptionText = cp.DescriptionText,
        optionType = cp.Type.ToString(),
        color = $"#{cp.DefinedColor.R:X2}{cp.DefinedColor.G:X2}{cp.DefinedColor.B:X2}"
    },
    LogicFlag flag => new
    {
        name = flag.Name,
        niceName = flag.NiceName,
        type = "Flag",
        active = flag.Active,
        settingGroup = flag.SettingGroup,
        settingPage = flag.SettingPage,
        descriptionText = flag.DescriptionText,
        optionType = flag.Type.ToString()
    },
    _ => new
    {
        name = option.Name,
        niceName = option.NiceName,
        type = "Flag",
        active = option.Active,
        settingGroup = option.SettingGroup,
        settingPage = option.SettingPage,
        descriptionText = option.DescriptionText,
        optionType = option.Type.ToString()
    }
};
