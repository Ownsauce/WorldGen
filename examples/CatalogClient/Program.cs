using System.Text.Json;
using WorldGen.Content;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: CatalogClient <catalog.json> <Assets-directory>");
    return 2;
}
try
{
    AssetCatalog catalog = JsonSerializer.Deserialize<AssetCatalog>(File.ReadAllText(args[0]),
        new JsonSerializerOptions { IncludeFields = true }) ?? throw new InvalidDataException("Empty catalog.");
    AssetCatalogValidator.VerifyFiles(catalog, args[1]);
    Console.WriteLine($"Content {catalog.contentVersion}: {catalog.assets.Length} assets");
    foreach (CatalogAsset asset in catalog.assets)
        Console.WriteLine($"{asset.id}: {asset.purpose}, opening {asset.placement.openingWidthMeters} x {asset.placement.openingHeightMeters} m; {asset.triangles} triangles");
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
