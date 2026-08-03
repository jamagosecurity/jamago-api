using Jama.Application.Options;
using Jama.Application.VipClients.Commands.UploadVipDocument;
using Microsoft.Extensions.Options;

namespace Jama.Application.Tests;

/// <summary>
/// These rules used to live inline in the handler, so nothing covered them.
/// They guard what a VIP client's folder will accept, which makes them worth a
/// test now that they sit in the normal validation pipeline.
/// </summary>
public sealed class VipUploadValidatorTests
{
    // Fully qualified: Jama.Application.Options collides with the
    // Microsoft.Extensions.Options.Options static class.
    private static UploadVipDocumentCommandValidator CreateValidator() =>
        new(Microsoft.Extensions.Options.Options.Create(new FileStorageSettings
        {
            MaxFileSizeMb = 2,
            AllowedExtensions = [".pdf", ".xlsx"],
        }));

    private static UploadVipDocumentCommand Command(string fileName, long sizeBytes) => new()
    {
        FolderId = Guid.CreateVersion7(),
        FileName = fileName,
        SizeBytes = sizeBytes,
    };

    [Theory]
    [InlineData("quote.pdf")]
    [InlineData("Quote.PDF")]
    [InlineData("sheet.xlsx")]
    public async Task Accepts_allowed_extensions_regardless_of_case(string fileName)
    {
        var result = await CreateValidator().ValidateAsync(Command(fileName, 1024));
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("payload.exe")]
    [InlineData("script.sh")]
    [InlineData("noextension")]
    public async Task Rejects_extensions_outside_the_allow_list(string fileName)
    {
        var result = await CreateValidator().ValidateAsync(Command(fileName, 1024));
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Rejects_a_file_larger_than_the_configured_limit()
    {
        // 2 MB configured above, so 2 MB exactly passes and one byte more does not.
        var limit = 2L * 1024 * 1024;
        Assert.True((await CreateValidator().ValidateAsync(Command("quote.pdf", limit))).IsValid);
        Assert.False((await CreateValidator().ValidateAsync(Command("quote.pdf", limit + 1))).IsValid);
    }

    [Fact]
    public async Task Rejects_an_empty_file()
    {
        var result = await CreateValidator().ValidateAsync(Command("quote.pdf", 0));
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("C:\\Windows\\system32\\config.pdf")]
    public async Task Reduces_a_path_to_its_file_name_before_judging_it(string fileName)
    {
        // The traversal attempt is not itself the defence — the storage key is
        // built from server-side ids — but the name must still be judged on the
        // leaf, or "../../x.pdf" would pass an extension check it should fail.
        var result = await CreateValidator().ValidateAsync(Command(fileName, 1024));
        var leaf = Path.GetFileName(fileName);
        Assert.Equal(leaf.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase), result.IsValid);
    }
}
