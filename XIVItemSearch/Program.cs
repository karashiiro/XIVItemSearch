using Lumina;
using Lumina.Excel.GeneratedSheets;
using Meilisearch;
using Microsoft.AspNetCore.Mvc;
using XIVItemSearch;

async Task<Delegate> BuildSearch()
{
    const string defaultGamePath = @"C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack";
    var lumina = new GameData(Environment.GetEnvironmentVariable("XIV_SQPACK") ?? defaultGamePath,
        new LuminaOptions { PanicOnSheetChecksumMismatch = false });
    var meili = new MeilisearchClient("http://localhost:7700");

    // Build the index
    var items = lumina.GetExcelSheet<Item>() ?? throw new InvalidOperationException("Failed to get item sheet");
    var itemsIndex = meili.Index("xiv_items");
    await itemsIndex.AddDocumentsInBatchesAsync(items.Select(item => new QueryableItem
    {
        Id = Convert.ToInt32(item.RowId),
        Name = item.Name.ToString(),
    }));

    // Declare the search endpoint handler itself
    async Task<IActionResult> Search(HttpContext ctx)
    {
        if (!ctx.Request.Query.TryGetValue("q", out var query))
        {
            return new BadRequestObjectResult("Parameter \"q\" was not provided");
        }

        var q = query.FirstOrDefault() ?? "";

        var page = 1;
        if (ctx.Request.Query.TryGetValue("page", out var queryPage) &&
            int.TryParse(queryPage.FirstOrDefault() ?? "", out var p))
        {
            page = p;
        }

        const int hitsPerPage = 20;
        var results = await itemsIndex.SearchAsync<QueryableItem>(q, new SearchQuery
        {
            Page = page,
            HitsPerPage = hitsPerPage,
        });
        var paged = results as PaginatedSearchResult<QueryableItem>;
        return new OkObjectResult(paged);
    }

    return Search;
}

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Welcome to the XIVItemSearch demo. Navigate to /search?q=... to search for items.");

app.MapGet("/search", await BuildSearch());

app.Run();